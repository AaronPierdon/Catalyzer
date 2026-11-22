using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Catalyzer.Sdk;

namespace Catalyzer.Helpers
{
    /// <summary>
    /// Helpers: each project's own helper scripts (build agents, watchers, local servers...), started hidden from its card.
    ///   * Listed in the project's catalyzer.json under "helpers" (docs/MANIFEST.md). Catalyzer never needs to change.
    ///   * Never a console window: every helper runs with CreateNoWindow, its output goes to a log file.
    ///   * Contained: every helper sits in one Windows job object that kills its members when Catalyzer goes - closed,
    ///     crashed or killed. Nothing is left running behind your back.
    ///   * Each card shows a light (HELPERS 1/2) with start / stop; the header shows what's running; closing asks first.
    /// </summary>
    public sealed class HelpersPlugin : ICatalyzerPlugin, IBackgroundWork
    {
        public string Id => "helpers";
        public string Name => "Helpers";
        public string Version => "1.0.0";
        public string Author => "Aaron Pierdon";
        public string Description => "Starts each project's helper scripts hidden (no console windows), shows their state on the card, and stops them when Catalyzer closes.";
        public int MinApi => 2;

        private ICatalyzerHost _host;
        private Job _job;
        private string _logs;
        private readonly object _gate = new object();
        /// <summary>Every helper this session knows (folder|name), running or not.</summary>
        private readonly Dictionary<string, Helper> _all = new Dictionary<string, Helper>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Auto-start happens once per session per helper: a helper you stopped stays stopped across rescans.</summary>
        private readonly HashSet<string> _autoDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _stopping;

        public event Action Changed;

        public void Start(ICatalyzerHost host)
        {
            _host = host;
            _logs = Path.Combine(host.DataFolder(this), "logs");
            if (!host.IsSnapshot) _job = new Job();
            host.AddBadge(new ProjectBadge
            {
                Id = "helpers", Order = 10,
                AppliesTo = p => Defs(p).Count > 0,
                State = BadgeFor,
                Menu = MenuFor,
            });
            host.AddBackgroundWork(this);
            host.ProjectsChanged += Sync;
            Sync();
        }

        public void Stop()
        {
            _stopping = true;   // nothing restarts from here on
            StopAll();
            _job?.Dispose();   // closing the job kills anything still in it
        }

        // ================================================================== the manifest

        /// <summary>"helpers": [ { "name", "run", "args", "auto", "restart", "tip" } ] in the project's catalyzer.json.</summary>
        private static List<HelperDef> Defs(Project p)
        {
            var list = new List<HelperDef>();
            if (p?.Extra == null || !p.Extra.TryGetValue("helpers", out var raw) || string.IsNullOrWhiteSpace(raw)) return list;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
                foreach (var h in doc.RootElement.EnumerateArray())
                {
                    if (h.ValueKind != JsonValueKind.Object) continue;
                    string S(string k) => h.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    bool B(string k) => h.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
                    var run = S("run"); if (string.IsNullOrWhiteSpace(run)) continue;
                    var name = S("name") ?? Path.GetFileNameWithoutExtension(run);
                    if (list.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;   // names are the key
                    list.Add(new HelperDef { Name = name, Run = run, Args = S("args") ?? "", Auto = B("auto"), Restart = B("restart"), Tip = S("tip") });
                }
            }
            catch (Exception) { }   // a broken manifest is reported by Catalyzer itself
            return list;
        }

        /// <summary>The board was rescanned: learn the helpers, refresh their definitions, auto-start what asks for it.</summary>
        private void Sync()
        {
            var auto = new List<Helper>();
            lock (_gate)
            {
                foreach (var p in _host.Projects)
                    foreach (var d in Defs(p))
                    {
                        var key = p.Folder + "|" + d.Name;
                        if (!_all.TryGetValue(key, out var h)) _all[key] = h = new Helper { Key = key, Folder = p.Folder, Project = p.Name, Log = LogFile(p.Name, d.Name) };
                        h.Project = p.Name;
                        if (!h.IsRunning) h.Def = d; else h.Pending = d;   // a running helper keeps the definition it was started with
                        if (d.Auto && !_host.IsSnapshot && _autoDone.Add(key) && !h.IsRunning) auto.Add(h);
                    }
            }
            foreach (var h in auto) Launch(h, "auto");
            _host.UpdateBadges();
        }

        // ================================================================== running

        private void Launch(Helper h, string why)
        {
            if (_host.IsSnapshot || _stopping) return;
            lock (_gate)
            {
                if (h.IsRunning) return;
                if (h.Pending != null) { h.Def = h.Pending; h.Pending = null; }
                var d = h.Def;
                ProcessStartInfo psi;
                try { psi = Psi(h.Folder, d); }
                catch (Exception ex) { Fail(h, "can't run " + d.Run + ": " + ex.Message); return; }
                h.Requested = false;
                try
                {
                    var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) Write(h, e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) Write(h, "! " + e.Data); };
                    p.Exited += (s, e) => Exited(h, p);
                    Write(h, "==== " + why + "  //  " + psi.FileName + " " + psi.Arguments, stamp: true);
                    p.Start();
                    try { _job?.Add(p); } catch (Exception ex) { _host.Log(this, "job: " + ex.Message); }
                    try { p.StandardInput.Close(); } catch { }   // a helper asking for input gets end-of-input, never a hang
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    h.Proc = p; h.State = HelperState.Running; h.Since = DateTime.Now; h.ExitCode = null; h.Note = null;
                    _host.Log(this, "started " + h.Project + " / " + d.Name + " (pid " + p.Id + ")");
                }
                catch (Exception ex) { Fail(h, "couldn't start " + d.Name + ": " + ex.Message); return; }
            }
            Poke();
        }

        /// <summary>How a helper is started: scripts through their interpreter, everything hidden, in the project folder.</summary>
        private static ProcessStartInfo Psi(string folder, HelperDef d)
        {
            var path = Path.IsPathRooted(d.Run) ? d.Run : Path.Combine(folder, d.Run);
            bool file = File.Exists(path);
            var ext = file ? Path.GetExtension(path).ToLowerInvariant() : "";
            var (exe, args) = ext switch
            {
                ".ps1" => ("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + path + "\" " + d.Args),
                ".cmd" or ".bat" => ("cmd.exe", "/d /s /c \"\"" + path + "\" " + d.Args + "\""),
                ".py" => ("python.exe", "-u \"" + path + "\" " + d.Args),
                ".js" or ".mjs" => ("node.exe", "\"" + path + "\" " + d.Args),
                _ => (file ? path : d.Run, d.Args),
            };
            var psi = new ProcessStartInfo(exe, args.Trim())
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = folder,
                RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            psi.Environment["CATALYZER_HELPER"] = d.Name;
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["PYTHONUNBUFFERED"] = "1";
            return psi;
        }

        private void Exited(Helper h, Process p)
        {
            bool again = false;
            lock (_gate)
            {
                if (h.Proc != p) return;
                int code = -1; try { code = p.ExitCode; } catch { }
                h.Proc = null; h.ExitCode = code; h.Ended = DateTime.Now;
                try { p.Dispose(); } catch { }
                Write(h, "==== exited " + code + (h.Requested ? " (stopped)" : ""), stamp: true);
                if (h.Requested || _stopping) { h.State = HelperState.Stopped; h.Note = null; }
                else if (code == 0) { h.State = HelperState.Done; h.Note = null; }
                else
                {
                    h.Restarts.RemoveAll(t => (DateTime.Now - t).TotalSeconds > 60);
                    if (h.Def.Restart && h.Restarts.Count < 3) { h.Restarts.Add(DateTime.Now); h.State = HelperState.Waiting; again = true; }
                    else { h.State = HelperState.Failed; h.Note = h.Def.Restart ? "gave up after 3 tries" : null; }
                    _host.Log(this, h.Project + " / " + h.Def.Name + " exited " + code + (again ? ", restarting" : ""), show: !again);
                }
            }
            Poke();
            if (again) Task.Delay(2000).ContinueWith(_ => { lock (_gate) if (h.State != HelperState.Waiting) return; Launch(h, "restart"); });
        }

        private void StopOne(Helper h)
        {
            Process p;
            lock (_gate)
            {
                if (h.State == HelperState.Waiting) { h.State = HelperState.Stopped; Poke(); return; }
                p = h.Proc; if (p == null) return;
                h.Requested = true;
            }
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) { _host.Log(this, "stop " + h.Def.Name + ": " + ex.Message); }
            _host.Log(this, "stopped " + h.Project + " / " + h.Def.Name);
        }

        private void Fail(Helper h, string why)
        {
            h.State = HelperState.Failed; h.Note = why; h.Ended = DateTime.Now;
            Write(h, "==== " + why, stamp: true);
            _host.Log(this, h.Project + ": " + why, show: true);
            Poke();
        }

        private void Poke() { _host.UpdateBadges(); try { Changed?.Invoke(); } catch { } }

        // ================================================================== IBackgroundWork

        string IBackgroundWork.Name => "HELPERS";
        public int Running { get { lock (_gate) return _all.Values.Count(h => h.IsRunning || h.State == HelperState.Waiting); } }

        public IEnumerable<MenuEntry> Items()
        {
            List<Helper> on; lock (_gate) on = _all.Values.Where(h => h.IsRunning || h.State == HelperState.Waiting).OrderBy(h => h.Project).ThenBy(h => h.Def.Name).ToList();
            return on.Select(h => new MenuEntry { Text = h.Project + "  //  " + h.Def.Name, Glyph = "", Hint = "stop  ·  " + Ago(h.Since), Run = () => StopOne(h) }).ToList();
        }

        public void StopAll()
        {
            List<Helper> on; lock (_gate) on = _all.Values.Where(h => h.IsRunning || h.State == HelperState.Waiting).ToList();
            foreach (var h in on) StopOne(h);
        }

        // ================================================================== the card

        private List<Helper> Of(Project p)
        {
            var names = Defs(p).Select(d => d.Name).ToList();
            lock (_gate) return names.Select(n => _all.TryGetValue(p.Folder + "|" + n, out var h) ? h : null).Where(h => h != null).ToList();
        }

        private BadgeState BadgeFor(Project p)
        {
            var hs = Of(p); if (hs.Count == 0) return null;
            int on = hs.Count(h => h.IsRunning), waiting = hs.Count(h => h.State == HelperState.Waiting), failed = hs.Count(h => h.State == HelperState.Failed);
            var tip = string.Join("\n", hs.Select(h => h.Def.Name.ToUpperInvariant() + "  //  " + Describe(h).ToUpperInvariant()));
            string count = hs.Count == 1 ? (on > 0 ? "HELPER ON" : "HELPER") : "HELPERS " + on + "/" + hs.Count;
            if (failed > 0) return new BadgeState { Text = hs.Count == 1 ? "HELPER FAILED" : count + "  ·  " + failed + " FAILED", Tone = Tone.Danger, Tip = tip };
            if (waiting > 0) return new BadgeState { Text = count + "  ·  RESTARTING", Tone = Tone.Warn, Pulse = true, Tip = tip };
            if (on > 0) return new BadgeState { Text = count, Tone = Tone.Ok, Pulse = true, Tip = tip };
            return new BadgeState { Text = hs.Count == 1 ? "HELPER OFF" : count, Tone = Tone.Dim, Tip = tip };
        }

        private IEnumerable<MenuEntry> MenuFor(Project p)
        {
            var hs = Of(p);
            var items = new List<MenuEntry>();
            foreach (var h in hs)
            {
                var x = h;
                if (h.IsRunning || h.State == HelperState.Waiting)
                    items.Add(new MenuEntry { Text = "Stop " + h.Def.Name, Glyph = "", Hint = Describe(h), Run = () => StopOne(x) });
                else
                    items.Add(new MenuEntry { Text = "Start " + h.Def.Name, Glyph = "", Hint = Describe(h), Danger = false, Run = () => Launch(x, "started from the card") });
            }
            if (hs.Count > 1)
            {
                items.Add(new MenuEntry { Separator = true });
                if (hs.Any(h => !h.IsRunning)) items.Add(new MenuEntry { Text = "Start all", Glyph = "", Run = () => { foreach (var h in hs.Where(h => !h.IsRunning)) Launch(h, "start all"); } });
                if (hs.Any(h => h.IsRunning)) items.Add(new MenuEntry { Text = "Stop all", Glyph = "", Danger = true, Run = () => { foreach (var h in hs) StopOne(h); } });
            }
            items.Add(new MenuEntry { Separator = true });
            var last = hs.Where(h => File.Exists(h.Log)).OrderByDescending(h => File.GetLastWriteTimeUtc(h.Log)).FirstOrDefault();
            if (last != null) items.Add(new MenuEntry { Text = "Open log", Glyph = "", Hint = last.Def.Name.ToLowerInvariant(), Run = () => Open(last.Log) });
            items.Add(new MenuEntry { Text = "Log folder", Glyph = "", Run = () => { Directory.CreateDirectory(_logs); Open(_logs); } });
            return items;
        }

        private static string Describe(Helper h) => h.State switch
        {
            HelperState.Running => "running  ·  " + Ago(h.Since),
            HelperState.Waiting => "restarting",
            HelperState.Done => "finished  ·  " + Ago(h.Ended) + " ago",
            HelperState.Failed => (h.Note ?? "exit " + h.ExitCode) + "  ·  see log",
            _ => h.Def.Auto ? "stopped  ·  auto" : "stopped",
        };

        private static string Ago(DateTime? t)
        {
            if (t == null) return "";
            var s = DateTime.Now - t.Value;
            return s.TotalMinutes < 1 ? "now" : s.TotalHours < 1 ? (int)s.TotalMinutes + "m" : s.TotalDays < 1 ? (int)s.TotalHours + "h " + s.Minutes + "m" : (int)s.TotalDays + "d";
        }

        private void Open(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { _host.Log(this, "couldn't open " + path + ": " + ex.Message, show: true); }
        }

        // ================================================================== logs (one per helper, rolling at 1 MB)

        private string LogFile(string project, string name)
        {
            static string Safe(string s) => new string((s ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '-' : c).ToArray());
            return Path.Combine(_logs, Safe(project) + "--" + Safe(name) + ".log");
        }

        private void Write(Helper h, string line, bool stamp = false)
        {
            if (_host.IsSnapshot) return;
            try
            {
                lock (h)
                {
                    Directory.CreateDirectory(_logs);
                    var fi = new FileInfo(h.Log);
                    if (fi.Exists && fi.Length > 1_000_000) File.Move(h.Log, h.Log + ".old", true);
                    File.AppendAllText(h.Log, DateTime.Now.ToString(stamp ? "yyyy-MM-dd HH:mm:ss" : "HH:mm:ss") + "  " + line + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    internal sealed class HelperDef
    {
        public string Name, Run, Args, Tip;
        public bool Auto, Restart;
    }

    internal enum HelperState { Stopped, Running, Waiting, Done, Failed }

    internal sealed class Helper
    {
        public string Key, Folder, Project, Log, Note;
        public HelperDef Def, Pending;
        public Process Proc;
        public HelperState State;
        public DateTime? Since, Ended;
        public int? ExitCode;
        public bool Requested;
        public readonly List<DateTime> Restarts = new List<DateTime>();
        public bool IsRunning => Proc != null;
    }

    /// <summary>A Windows job object that kills every process in it when it closes - which Windows does when Catalyzer goes.</summary>
    internal sealed class Job : IDisposable
    {
        private IntPtr _h;

        public Job()
        {
            _h = CreateJobObject(IntPtr.Zero, null);
            if (_h == IntPtr.Zero) throw new InvalidOperationException("CreateJobObject failed: " + Marshal.GetLastWin32Error());
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION { BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = 0x2000 } };   // KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(_h, 9, ref info, Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
                throw new InvalidOperationException("SetInformationJobObject failed: " + Marshal.GetLastWin32Error());
        }

        public void Add(Process p)
        {
            if (_h != IntPtr.Zero && !AssignProcessToJobObject(_h, p.Handle))
                throw new InvalidOperationException("AssignProcessToJobObject failed: " + Marshal.GetLastWin32Error());
        }

        public void Dispose() { if (_h != IntPtr.Zero) { CloseHandle(_h); _h = IntPtr.Zero; } }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int length);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
    }
}
