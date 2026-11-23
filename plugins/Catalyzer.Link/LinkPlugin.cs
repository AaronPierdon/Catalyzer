using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Catalyzer.Sdk;

namespace Catalyzer.Link
{
    /// <summary>
    /// LINK: one click on a project's card gives that repo a hidden Windows terminal for Claude (or any tool that can see
    /// the repo folder). Nothing listens on the network: the channel is a mailbox folder inside the repo,
    /// <c>&lt;repo&gt;\.catalyzer\link\</c> (git-ignored), with a README and a small client (run.sh) so a new chat finds
    /// out by itself how to use it. Requests run hidden in the repo root, one at a time, inside a kill-on-close job;
    /// Aaron sees every command and its output in the feed. A linked project stays linked across restarts until unlinked.
    /// </summary>
    public sealed class LinkPlugin : ICatalyzerPlugin, IBackgroundWork
    {
        public string Id => "link";
        public string Name => "Link";
        public string Version => "1.0.0";
        public string Author => "Aaron Pierdon";
        public string Description => "LINK on a card gives that repo a hidden Windows terminal for Claude: commands run in the repo, no windows, every one shown in the feed.";
        public int MinApi => 3;

        internal ICatalyzerHost Host;
        internal Job Job;
        internal string Wrapper;
        private string _state;
        private readonly object _gate = new object();
        private readonly Dictionary<string, Link> _links = new Dictionary<string, Link>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _remember = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _options;
        /// <summary>Linked projects come back on by themselves when Catalyzer starts (Settings → General).</summary>
        private bool _relinkAtStart = true;
        public event Action Changed;

        public void Start(ICatalyzerHost host)
        {
            Host = host;
            var data = host.DataFolder(this);
            _state = Path.Combine(data, "linked.json");
            _options = Path.Combine(data, "options.json");
            try { if (File.Exists(_options)) _relinkAtStart = JsonDocument.Parse(File.ReadAllText(_options)).RootElement.TryGetProperty("relinkAtStart", out var r) ? r.GetBoolean() : true; }
            catch (Exception ex) { host.Log(this, "options.json: " + ex.Message); }
            host.AddSetting(this, new PluginSetting
            {
                Id = "relink", Label = "Relink linked projects when Catalyzer starts",
                Note = "Off: every link starts OFF after a restart, until you click LINK. (A morning brief needs it on.)",
                Get = () => _relinkAtStart,
                Set = v => { _relinkAtStart = v; try { File.WriteAllText(_options, "{ \"relinkAtStart\": " + (v ? "true" : "false") + " }"); } catch (Exception ex) { Host.Log(this, "options.json: " + ex.Message); } },
            });
            try { if (File.Exists(_state)) _remember = new HashSet<string>(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_state)) ?? new List<string>(), StringComparer.OrdinalIgnoreCase); }
            catch (Exception ex) { host.Log(this, "linked.json: " + ex.Message); }
            if (!host.IsSnapshot)
            {
                Job = new Job();
                Wrapper = Path.Combine(data, "run-request.ps1");
                try { File.WriteAllText(Wrapper, Asset("run-request.ps1"), new UTF8Encoding(true)); } catch (Exception ex) { host.Log(this, "wrapper: " + ex.Message, show: true); }
            }
            host.AddBadge(new ProjectBadge
            {
                Id = "link", Order = 0,
                State = BadgeFor,
                Click = Toggle,
                Menu = MenuFor,
            });
            host.AddBackgroundWork(this);
            host.ProjectsChanged += Sync;
            Sync();
        }

        public void Stop()
        {
            List<Link> all; lock (_gate) all = _links.Values.ToList();
            foreach (var l in all) l.Deactivate("Catalyzer closed");   // stays remembered: it relinks at the next start
            Job?.Dispose();
        }

        internal static string Asset(string name)
        {
            using var s = typeof(LinkPlugin).Assembly.GetManifestResourceStream("Link." + name);
            using var r = new StreamReader(s, Encoding.UTF8);
            return r.ReadToEnd();
        }

        // ================================================================== linking

        private Link Of(Project p) { lock (_gate) return _links.TryGetValue(p.Folder, out var l) ? l : null; }

        private Link Get(Project p)
        {
            lock (_gate)
            {
                if (!_links.TryGetValue(p.Folder, out var l)) _links[p.Folder] = l = new Link(this, p.Folder, p.Name);
                l.Name = p.Name;
                return l;
            }
        }

        /// <summary>Remembered links come back by themselves (Catalyzer starts with Windows; nobody has to click again).</summary>
        private void Sync()
        {
            if (Host.IsSnapshot) { Host.UpdateBadges(); return; }
            // remembered links come back by themselves, unless that's switched off (then only a click links)
            if (!_relinkAtStart) { Host.UpdateBadges(); return; }
            foreach (var p in Host.Projects)
                if (_remember.Contains(p.Folder))
                {
                    var l = Get(p);
                    if (!l.Active) l.Activate("relinked at start");
                }
            Host.UpdateBadges();
        }

        private void Toggle(Project p)
        {
            if (Host.IsSnapshot) return;
            var l = Get(p);
            if (l.Active) { l.Deactivate("unlinked from the card"); Remember(p.Folder, false); }
            else { l.Activate("linked from the card"); Remember(p.Folder, true); }
        }

        private void Remember(string folder, bool on)
        {
            lock (_gate) { if (on) _remember.Add(folder); else _remember.Remove(folder); }
            try
            {
                var tmp = _state + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_remember.OrderBy(x => x).ToList(), new JsonSerializerOptions { WriteIndented = true }));
                File.Move(tmp, _state, true);
            }
            catch (Exception ex) { Host.Log(this, "linked.json: " + ex.Message); }
        }

        /// <summary>Link / Unlink from the feed drawer.</summary>
        internal void HostToggle(Link l)
        {
            if (l.Active) { l.Deactivate("unlinked from the feed"); Remember(l.Folder, false); }
            else { l.Activate("linked from the feed"); Remember(l.Folder, true); }
        }

        internal void Poke() { Host.UpdateBadges(); try { Changed?.Invoke(); } catch { } }

        // ================================================================== the card

        private BadgeState BadgeFor(Project p)
        {
            var l = Of(p);
            bool on = l?.Active ?? (Host.IsSnapshot && _remember.Contains(p.Folder));
            if (!on) return new BadgeState { Text = "LINK", Tone = Tone.Dim, Tip = "CLICK: A HIDDEN WINDOWS TERMINAL FOR CLAUDE IN THIS REPO\nRIGHT-CLICK: FEED · MAILBOX" };
            int busy = l?.Busy ?? 0;
            return busy > 0
                ? new BadgeState { Text = "LINKED  ·  RUNNING", Tone = Tone.Select, Pulse = true, Tip = l.Now + "\nCLICK: UNLINK  //  RIGHT-CLICK: SHOW THE FEED" }
                : new BadgeState { Text = "LINKED", Tone = Tone.Select, Tip = "CLAUDE CAN RUN COMMANDS HERE (HIDDEN)\nCLICK: UNLINK  //  RIGHT-CLICK: SHOW THE FEED" };
        }

        private IEnumerable<MenuEntry> MenuFor(Project p)
        {
            var l = Get(p);
            var items = new List<MenuEntry>
            {
                new MenuEntry { Text = "Show the feed", Glyph = "", Hint = "every command + output", Run = () => Host.ShowFeed(l) },
                l.Active ? new MenuEntry { Text = "Unlink", Glyph = "", Hint = "stops what it's running", Run = () => Toggle(p) }
                         : new MenuEntry { Text = "Link", Glyph = "", Hint = "a hidden terminal for Claude", Run = () => Toggle(p) },
            };
            if (l.Busy > 0) items.Add(new MenuEntry { Text = "Cancel what's running", Glyph = "", Danger = true, Run = () => l.CancelAll() });
            items.Add(new MenuEntry { Separator = true });
            items.Add(new MenuEntry { Text = "Open the mailbox", Glyph = "", Hint = ".catalyzer\\link", Run = () => l.OpenBox() });
            return items;
        }

        // ================================================================== IBackgroundWork (closing asks while commands run)

        string IBackgroundWork.Name => "LINK COMMANDS";
        /// <summary>Survivors (installs) don't count: they're meant to outlive Catalyzer, so closing needn't ask about them.</summary>
        public int Running { get { lock (_gate) return _links.Values.Sum(l => l.Held); } }
        public IEnumerable<MenuEntry> Items()
        {
            List<Link> all; lock (_gate) all = _links.Values.Where(l => l.Held > 0).ToList();
            return all.Select(l => new MenuEntry { Text = l.Name + "  //  " + l.Now, Glyph = "", Hint = "cancel", Run = () => l.CancelAll() }).ToList();
        }
        public void StopAll() { List<Link> all; lock (_gate) all = _links.Values.ToList(); foreach (var l in all) l.CancelAll(); }
    }

    /// <summary>One request: a script in out\, its log, its exit code file, and the process running it.</summary>
    internal sealed class Req
    {
        public string Id, Script, Log, Done, Preview;
        public int Timeout = 900;
        public bool Background, Survive, Normal;
        public Process Proc;
        public DateTime Started;
        public long Offset;
        public readonly StringBuilder Partial = new StringBuilder();
        public string Ended;   // why it was stopped (cancelled / timeout / unlinked)
    }

    /// <summary>A linked project: its mailbox, its queue, its feed.</summary>
    internal sealed class Link : IFeed
    {
        private readonly LinkPlugin _pl;
        public readonly string Folder;
        public string Name;
        private readonly string _box, _in, _out;
        public bool Active { get; private set; }
        private DateTime _since;
        private Timer _poll, _beat;
        private int _ticking;
        private readonly object _gate = new object();
        private readonly Queue<Req> _queue = new Queue<Req>();
        private Req _current;
        private readonly List<Req> _bg = new List<Req>();
        private readonly List<FeedLine> _lines = new List<FeedLine>();
        private static readonly Regex IdOk = new Regex(@"^[A-Za-z0-9_\-]{1,80}$");
        public event Action Changed;

        public Link(LinkPlugin pl, string folder, string name)
        {
            _pl = pl; Folder = folder.TrimEnd('\\'); Name = name;
            _box = Path.Combine(Folder, ".catalyzer", "link"); _in = Path.Combine(_box, "in"); _out = Path.Combine(_box, "out");
        }

        public int Busy { get { lock (_gate) return (_current != null ? 1 : 0) + _queue.Count + _bg.Count(r => r.Proc != null); } }
        public int Held { get { lock (_gate) return (_current != null && !_current.Survive ? 1 : 0) + _queue.Count + _bg.Count(r => r.Proc != null && !r.Survive); } }
        public string Now { get { lock (_gate) return _current?.Preview ?? _bg.FirstOrDefault()?.Preview ?? (_queue.Count > 0 ? _queue.Count + " queued" : "idle"); } }

        // ---------------------------------------------------------------- on / off

        public void Activate(string why)
        {
            if (Active || _pl.Wrapper == null) return;
            try
            {
                Directory.CreateDirectory(_in); Directory.CreateDirectory(_out);
                var ign = Path.Combine(Folder, ".catalyzer", ".gitignore");
                if (!File.Exists(ign)) File.WriteAllText(ign, "# Catalyzer's own folder (link mailbox, logs): never part of the repo\n*\n");
                File.WriteAllText(Path.Combine(_box, "README.md"), LinkPlugin.Asset("README.md").Replace("\r\n", "\n"));
                File.WriteAllText(Path.Combine(_box, "run.sh"), LinkPlugin.Asset("run.sh").Replace("\r\n", "\n"));   // LF: bash reads it
                SetAside();
                Prune();
            }
            catch (Exception ex) { Say("couldn't prepare the mailbox: " + ex.Message, Tone.Danger); _pl.Host.Log(_pl, Name + ": " + ex.Message, show: true); return; }
            lock (_gate) { Active = true; _since = DateTime.UtcNow; }
            Beat();
            _poll = new Timer(_ => Tick(), null, 500, 750);
            _beat = new Timer(_ => Beat(), null, 15000, 15000);
            Say("LINKED  ·  " + why + "  ·  " + Folder, Tone.Accent);
            _pl.Host.Log(_pl, "linked " + Folder + " (" + why + ")");
            _pl.Poke();
        }

        public void Deactivate(string why)
        {
            lock (_gate) { if (!Active) return; Active = false; }
            _poll?.Dispose(); _beat?.Dispose(); _poll = _beat = null;
            CancelAll("unlinked", keepSurvivors: true);
            try { File.WriteAllText(Path.Combine(_box, "STATUS"), "OFF\nsince: " + DateTime.Now.ToString("s") + "\nwhy: " + why + "\nturn it on: click LINK on this project's card in Catalyzer\n"); } catch { }
            Say("UNLINKED  ·  " + why, Tone.Dim);
            _pl.Host.Log(_pl, "unlinked " + Folder + " (" + why + ")");
            _pl.Poke();
        }

        /// <summary>Requests already waiting when the link comes on were not written for this link: set aside, never run.</summary>
        private void SetAside()
        {
            var old = Directory.GetFiles(_in).Where(f => !f.EndsWith(".part")).ToList();
            if (old.Count == 0) return;
            var dir = Path.Combine(_out, "set-aside-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            foreach (var f in old) { try { File.Move(f, Path.Combine(dir, Path.GetFileName(f)), true); } catch { } }
            Say(old.Count + " request(s) were waiting from before the link: set aside in out\\" + Path.GetFileName(dir) + ", not run", Tone.Warn);
        }

        private void Prune()
        {
            var cut = DateTime.UtcNow.AddDays(-3);
            foreach (var f in Directory.GetFiles(_out)) try { if (File.GetLastWriteTimeUtc(f) < cut) File.Delete(f); } catch { }
            foreach (var d in Directory.GetDirectories(_out)) try { if (Directory.GetLastWriteTimeUtc(d) < cut) Directory.Delete(d, true); } catch { }
        }

        private void Beat()
        {
            if (!Active) return;
            try
            {
                string run; int q; lock (_gate) { run = _current?.Id ?? "-"; q = _queue.Count; }
                var s = "ACTIVE\nsince: " + _since.ToLocalTime().ToString("s") + "\nheartbeat: " + DateTime.Now.ToString("s") + "\nrepo: " + Folder +
                        "\nrunning: " + run + "\nqueued: " + q + "\nshell: Windows PowerShell 5.1, hidden, in the repo root\nhost: Catalyzer " + _pl.Host.AppVersion + " / Link " + _pl.Version + "\n";
                var p = Path.Combine(_box, "STATUS");
                File.WriteAllText(p + ".tmp", s); File.Move(p + ".tmp", p, true);
            }
            catch { }
        }

        // ---------------------------------------------------------------- the loop (every 750 ms while linked)

        private void Tick()
        {
            if (!Active || Interlocked.Exchange(ref _ticking, 1) == 1) return;
            try
            {
                PickUp();
                Cancels();
                List<Req> running; lock (_gate) { running = _bg.Where(r => r.Proc != null).ToList(); if (_current != null) running.Add(_current); }
                foreach (var r in running)
                {
                    Tail(r, false);
                    if (r.Timeout > 0 && !r.Background && (DateTime.Now - r.Started).TotalSeconds > r.Timeout) Kill(r, "timeout after " + r.Timeout + " s");
                }
                Next();
            }
            catch (Exception ex) { _pl.Host.Log(_pl, Name + " link: " + ex.Message); }
            finally { Interlocked.Exchange(ref _ticking, 0); }
        }

        /// <summary>Requests git knows about came with the repo (a pull, a clone), not from a chat: never run them.</summary>
        private bool Tracked(string file)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "-C \"" + Folder + "\" ls-files --error-unmatch -- \"" + Path.GetRelativePath(Folder, file).Replace('\\', '/') + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = Process.Start(psi);
                if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return true; }   // can't tell: don't run it
                return p.ExitCode == 0;
            }
            catch { return false; }   // no git: nothing is tracked
        }

        private void PickUp()
        {
            foreach (var f in Directory.GetFiles(_in).OrderBy(x => x, StringComparer.Ordinal))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".ps1" && ext != ".cmd" && ext != ".bat") continue;
                var fi = new FileInfo(f);
                if ((DateTime.UtcNow - fi.LastWriteTimeUtc).TotalMilliseconds < 250) continue;   // still being written
                var id = Path.GetFileNameWithoutExtension(f);
                if (Tracked(f))
                {
                    try { File.Move(f, Path.Combine(_out, "refused-" + Guid.NewGuid().ToString("N")[..8] + ext), true); } catch { }
                    Say("REFUSED " + Path.GetFileName(f) + ": it's part of the repo (it came with a pull or a clone), not a request from a chat", Tone.Danger);
                    _pl.Host.Log(_pl, Name + ": refused a request tracked by git: " + Path.GetFileName(f), show: true);
                    continue;
                }
                if (!IdOk.IsMatch(id)) { try { File.Move(f, Path.Combine(_out, "rejected-" + Guid.NewGuid().ToString("N")[..8] + ext), true); } catch { } Say("rejected a request with a bad name: " + Path.GetFileName(f), Tone.Warn); continue; }
                while (Directory.GetFiles(_out, id + ".*").Length > 0) id += "-2";
                var r = new Req { Id = id, Script = Path.Combine(_out, id + ext), Log = Path.Combine(_out, id + ".log"), Done = Path.Combine(_out, id + ".done") };
                try
                {
                    if (ext == ".ps1")
                    {
                        var text = File.ReadAllText(f, Encoding.UTF8);
                        File.WriteAllText(r.Script, text, new UTF8Encoding(true));   // Windows PowerShell 5.1 needs the BOM to read UTF-8
                        Options(r, text);
                    }
                    else { File.Copy(f, r.Script, true); Options(r, File.ReadAllText(f)); }
                    File.Delete(f);
                }
                catch (Exception ex) { Say("couldn't take " + Path.GetFileName(f) + ": " + ex.Message, Tone.Danger); continue; }
                lock (_gate)
                {
                    if (r.Background) _bg.Add(r); else _queue.Enqueue(r);
                }
                Say("▸ " + r.Id + "  " + r.Preview + (r.Background ? "   [background]" : "") + (r.Survive ? "   [survive]" : ""), Tone.Select);
                if (r.Background) Launch(r);
                _pl.Poke();
            }
        }

        /// <summary>The first line may say "# catalyzer: timeout=600 background survive". Preview = the first real line.</summary>
        private static void Options(Req r, string text)
        {
            var lines = text.Replace("\r", "").Split('\n');
            var head = lines.FirstOrDefault(l => l.TrimStart().StartsWith("# catalyzer:", StringComparison.OrdinalIgnoreCase));
            if (head != null)
            {
                var m = Regex.Match(head, @"timeout\s*=\s*(\d+)"); if (m.Success) r.Timeout = int.Parse(m.Groups[1].Value);
                r.Background = Regex.IsMatch(head, @"\bbackground\b", RegexOptions.IgnoreCase);
                r.Survive = Regex.IsMatch(head, @"\bsurvive\b", RegexOptions.IgnoreCase);
                r.Normal = Regex.IsMatch(head, @"\bpriority\s*=\s*normal\b", RegexOptions.IgnoreCase);
            }
            var first = lines.Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("#")) ?? "(empty)";
            r.Preview = first.Length > 140 ? first[..140] + "…" : first;
        }

        private void Cancels()
        {
            foreach (var f in Directory.GetFiles(_in, "*.cancel"))
            {
                var id = Path.GetFileNameWithoutExtension(f);
                try { File.Delete(f); } catch { }
                Req r; lock (_gate) r = (_current?.Id == id ? _current : null) ?? _bg.FirstOrDefault(x => x.Id == id) ?? _queue.FirstOrDefault(x => x.Id == id);
                if (r == null) continue;
                if (r.Proc == null) { Drop(r, "cancelled before it started"); continue; }
                Kill(r, "cancelled");
            }
        }

        private void Next()
        {
            Req r;
            lock (_gate) { if (_current != null || _queue.Count == 0) return; _current = r = _queue.Dequeue(); }
            Launch(r);
        }

        private void Launch(Req r)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + _pl.Wrapper + "\" -Script \"" + r.Script + "\" -Log \"" + r.Log + "\" -Done \"" + r.Done + "\" -Root \"" + Folder + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Folder };
                psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
                psi.Environment["CATALYZER_LINK"] = r.Id;
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.Exited += (s, e) => Finished(r, p);
                r.Started = DateTime.Now;
                p.Start();
                if (!r.Survive) { try { _pl.Job?.Add(p); } catch (Exception ex) { _pl.Host.Log(_pl, "job: " + ex.Message); } }
                // below normal by default (children inherit it): builds never make games or music stutter
                if (!r.Normal) { try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { } }
                lock (_gate) r.Proc = p;
                Beat();
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(r.Log, "! couldn't start: " + ex.Message + Environment.NewLine); File.WriteAllText(r.Done, "126"); } catch { }
                Say("! couldn't start " + r.Id + ": " + ex.Message, Tone.Danger);
                lock (_gate) { if (_current == r) _current = null; _bg.Remove(r); }
            }
            _pl.Poke();
        }

        private void Finished(Req r, Process p)
        {
            int code = -1; try { code = p.ExitCode; } catch { }
            try { p.Dispose(); } catch { }
            if (r.Ended != null || !File.Exists(r.Done))
            {
                // the runner was killed before it could write its own exit code
                code = r.Ended?.StartsWith("timeout") == true ? 124 : r.Ended != null ? 130 : (code > 0 ? code : 137);
                try { File.AppendAllText(r.Log, "! " + (r.Ended ?? "the runner ended without an exit code") + Environment.NewLine); File.WriteAllText(r.Done, code.ToString()); } catch { }
            }
            else { try { int.TryParse(File.ReadAllText(r.Done).Trim(), out code); } catch { } }
            Tail(r, true);
            var secs = (DateTime.Now - r.Started).TotalSeconds;
            Say("■ " + r.Id + "  exit " + code + "  ·  " + (secs < 90 ? secs.ToString("0.0") + " s" : (secs / 60).ToString("0.0") + " min") + (r.Ended != null ? "  ·  " + r.Ended : ""), code == 0 ? Tone.Ok : Tone.Danger);
            lock (_gate) { r.Proc = null; if (_current == r) _current = null; _bg.Remove(r); }
            Beat();
            _pl.Poke();
            if (Active) ThreadPool.QueueUserWorkItem(_ => Tick());
        }

        private void Kill(Req r, string why)
        {
            Process p; lock (_gate) { p = r.Proc; if (p == null) return; r.Ended = why; }
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) { _pl.Host.Log(_pl, "kill " + r.Id + ": " + ex.Message); }
        }

        private void Drop(Req r, string why)
        {
            lock (_gate) { var keep = _queue.Where(x => x != r).ToList(); _queue.Clear(); foreach (var x in keep) _queue.Enqueue(x); _bg.Remove(r); }
            try { File.AppendAllText(r.Log, "! " + why + Environment.NewLine); File.WriteAllText(r.Done, "130"); } catch { }
            Say("■ " + r.Id + "  " + why, Tone.Dim);
            _pl.Poke();
        }

        public void CancelAll() => CancelAll("cancelled", keepSurvivors: true);

        private void CancelAll(string why, bool keepSurvivors)
        {
            List<Req> queued, running;
            lock (_gate)
            {
                queued = _queue.ToList();
                running = _bg.Where(r => r.Proc != null).ToList(); if (_current != null) running.Add(_current);
            }
            foreach (var r in queued) Drop(r, why + " before it started");
            foreach (var r in running) if (!(keepSurvivors && r.Survive)) Kill(r, why);
        }

        /// <summary>New output from the request's log into the feed (whole lines; the rest waits for its newline).</summary>
        private void Tail(Req r, bool final)
        {
            try
            {
                if (!File.Exists(r.Log)) return;
                string chunk;
                using (var fs = new FileStream(r.Log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (fs.Length <= r.Offset) { if (!final) return; chunk = ""; }
                    else
                    {
                        fs.Seek(r.Offset, SeekOrigin.Begin);
                        var buf = new byte[Math.Min(fs.Length - r.Offset, 4_000_000)];
                        int n = fs.Read(buf, 0, buf.Length); r.Offset += n;
                        chunk = Encoding.UTF8.GetString(buf, 0, n);
                    }
                }
                r.Partial.Append(chunk.Replace("\r", ""));
                var all = r.Partial.ToString();
                int cut = final ? all.Length : all.LastIndexOf('\n') + 1;
                if (cut <= 0) return;
                var lines = all[..cut].Split('\n', StringSplitOptions.RemoveEmptyEntries);
                r.Partial.Clear(); r.Partial.Append(all[cut..]);
                var add = lines.Select(l => new FeedLine { Text = l, Tone = l.StartsWith("! ") ? Tone.Warn : Tone.Dim }).ToList();
                if (add.Count > 0) { lock (_lines) { _lines.AddRange(add); Trim(); } Raise(); }
            }
            catch { }
        }

        // ---------------------------------------------------------------- the feed

        private void Say(string text, Tone tone)
        {
            lock (_lines) { _lines.Add(new FeedLine { Text = text, Tone = tone }); Trim(); }
            Raise();
        }
        private void Trim() { if (_lines.Count > 2500) _lines.RemoveRange(0, _lines.Count - 2000); }
        private void Raise() { try { Changed?.Invoke(); } catch { } }

        public string Title => Name + "  //  Link";
        public BadgeState State => !Active ? new BadgeState { Text = "OFF", Tone = Tone.Dim }
                                 : Busy > 0 ? new BadgeState { Text = "LINKED  ·  RUNNING", Tone = Tone.Select, Pulse = true }
                                 : new BadgeState { Text = "LINKED  ·  IDLE", Tone = Tone.Select };
        public IReadOnlyList<FeedLine> Lines() { lock (_lines) return _lines.ToList(); }
        public IEnumerable<MenuEntry> Actions()
        {
            var a = new List<MenuEntry>();
            a.Add(Active ? new MenuEntry { Text = "Unlink", Glyph = "", Run = () => _pl.HostToggle(this) } : new MenuEntry { Text = "Link", Glyph = "", Run = () => _pl.HostToggle(this) });
            if (Busy > 0) a.Add(new MenuEntry { Text = "Cancel", Glyph = "", Hint = "stop what's running", Run = CancelAll });
            a.Add(new MenuEntry { Text = "Mailbox", Glyph = "", Hint = ".catalyzer\\link", Run = OpenBox });
            a.Add(new MenuEntry { Text = "Clear", Glyph = "", Run = () => { lock (_lines) _lines.Clear(); Raise(); } });
            return a;
        }

        public void OpenBox()
        {
            try { Directory.CreateDirectory(_box); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + _box + "\"") { UseShellExecute = true }); }
            catch (Exception ex) { _pl.Host.Log(_pl, "open " + _box + ": " + ex.Message, show: true); }
        }
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
