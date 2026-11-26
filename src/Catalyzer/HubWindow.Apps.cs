using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// UPDATE on every app's card (principles 07, the install contract): the repo's version (&lt;Version&gt; in the app's
    /// .csproj) against the installed exe's. Newer in the repo → UPDATE a → b; not installed → INSTALL. One click runs
    /// the app's own tools\install.ps1 hidden with -Log, as its own process (it outlives Catalyzer if need be); the chip
    /// pulses while it runs and turns red with the log on failure. Where things are comes from catalyzer.json "app"
    /// ({ "exe", "version", "install" }) or, without it, the template layout. Catalyzer itself uses the header chip.
    /// </summary>
    public sealed partial class HubWindow
    {
        private sealed class AppInfo { public string Exe, VersionFile, Install; public Version Repo, Installed; public bool Exists; }
        private sealed class InstallRun { public Process Proc; public string Log; public int? Code; public DateTime Ended; }

        private readonly Dictionary<string, AppInfo> _appOf = new Dictionary<string, AppInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, InstallRun> _installs = new Dictionary<string, InstallRun>(StringComparer.OrdinalIgnoreCase);
        private static readonly string InstallLogs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Catalyzer", "installs");

        private void InitApps()
        {
            PluginHost.I?.AddBadge(new ProjectBadge
            {
                Id = "app-update", Order = -15,
                AppliesTo = p => !IsSelf(p.Folder),
                State = AppBadge,
                Click = AppClick,
                Menu = p =>
                {
                    var a = ReadApp(p); var list = new List<MenuEntry>();
                    if (a != null) list.Add(new MenuEntry { Text = a.Exists ? "Build + install now" : "Install", Glyph = "", Hint = "tools\\install.ps1, hidden", Run = () => RunInstall(p) });
                    if (_installs.TryGetValue(p.Folder, out var r)) list.Add(new MenuEntry { Text = "Open the install log", Glyph = "", Run = () => Launch.Shell(r.Log, null) });
                    if (a?.Exists == true) list.Add(new MenuEntry { Text = "Open the install folder", Glyph = "", Hint = a.Exe, Run = () => Launch.Shell("explorer.exe", "/select,\"" + a.Exe + "\"") });
                    return list;
                },
            });
            _board.GitLoaded += p => { _appOf.Remove(p.Folder); PluginHost.I?.UpdateBadges(); };
        }

        private static string Expand(string s) => s == null ? null : Environment.ExpandEnvironmentVariables(s);

        /// <summary>Where the app's exe, version and install script are, or null if it isn't an installable app.</summary>
        private AppInfo ReadApp(Project p)
        {
            if (_appOf.TryGetValue(p.Folder, out var cached)) return cached;
            AppInfo a = null;
            try
            {
                string exe = null, ver = null, inst = null;
                if (p.Extra != null && p.Extra.TryGetValue("app", out var raw) && !string.IsNullOrWhiteSpace(raw))
                {
                    using var doc = JsonDocument.Parse(raw);
                    string S(string k) => doc.RootElement.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    exe = Expand(S("exe")); ver = S("version"); inst = S("install");
                }
                var name = Path.GetFileName(p.Folder);
                exe ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", name, name + ".exe");
                ver ??= Path.Combine("src", name, name + ".csproj");
                inst ??= Path.Combine("tools", "install.ps1");
                var verFull = Path.Combine(p.Folder, ver); var instFull = Path.Combine(p.Folder, inst);
                if (File.Exists(instFull) && File.Exists(verFull))
                {
                    a = new AppInfo { Exe = exe, VersionFile = verFull, Install = instFull, Exists = File.Exists(exe) };
                    var m = Regex.Match(File.ReadAllText(verFull), @"<Version>\s*([0-9]+(\.[0-9]+){1,3})\s*</Version>");
                    if (m.Success) Version.TryParse(m.Groups[1].Value, out a.Repo);
                    if (a.Exists)
                    {
                        var fv = FileVersionInfo.GetVersionInfo(exe);
                        var txt = (fv.ProductVersion ?? fv.FileVersion ?? "").Split('+', ' ')[0];
                        Version.TryParse(txt, out a.Installed);
                    }
                }
            }
            catch (Exception ex) { Log.Write("app " + p.Name + ": " + ex.Message); }
            _appOf[p.Folder] = a;
            return a;
        }

        private static Version Norm(Version v) => v == null ? null : new Version(v.Major, v.Minor, Math.Max(0, v.Build));

        private BadgeState AppBadge(Project p)
        {
            if (_installs.TryGetValue(p.Folder, out var r))
            {
                if (r.Code == null) return new BadgeState { Text = "INSTALLING", Tone = Tone.Select, Pulse = true, Card = CardMood.Working, Tip = "BUILDING AND INSTALLING " + p.Name.ToUpperInvariant() + " (TOOLS\\INSTALL.PS1, HIDDEN)\nRIGHT-CLICK: THE LOG" };
                if (r.Code != 0) return new BadgeState { Text = "INSTALL FAILED", Tone = Tone.Danger, Card = CardMood.Danger, Tip = "TOOLS\\INSTALL.PS1 STOPPED (EXIT " + r.Code + ")  //  CLICK: THE LOG  //  RIGHT-CLICK: TRY AGAIN" };
                if (DateTime.Now - r.Ended < TimeSpan.FromMinutes(2)) { var done = ReadApp(p); return new BadgeState { Text = "✓  INSTALLED" + (done?.Installed != null ? "  " + V3(done.Installed) : ""), Tone = Tone.Ok, Tip = "INSTALLED  //  " + (done?.Exe ?? "").ToUpperInvariant() }; }
            }
            var a = ReadApp(p);
            if (a == null || a.Repo == null) return null;
            if (!a.Exists) return new BadgeState { Text = "INSTALL", Tone = Tone.Faint, Tip = "NOT INSTALLED ON THIS PC (" + a.Exe.ToUpperInvariant() + ")\nCLICK: BUILD + INSTALL " + V3(a.Repo) + "  //  ABOUT A MINUTE" };
            if (a.Installed == null || Norm(a.Repo) > Norm(a.Installed))
                return new BadgeState { Text = "UPDATE  " + (a.Installed != null ? V3(a.Installed) : "?") + " → " + V3(a.Repo), Tone = Tone.Select, Pulse = true, Tip = "THE REPO HAS " + V3(a.Repo) + ", THIS PC RUNS " + (a.Installed != null ? V3(a.Installed) : "AN UNKNOWN VERSION") + "\nCLICK: BUILD, CLOSE " + p.Name.ToUpperInvariant() + ", INSTALL ON TOP, RESTART IT" };
            return null;   // up to date: nothing to show
        }

        private void AppClick(Project p)
        {
            if (_installs.TryGetValue(p.Folder, out var r))
            {
                if (r.Code == null) { Launch.Shell(r.Log, null); return; }
                if (r.Code != 0) { Launch.Shell(r.Log, null); return; }
            }
            RunInstall(p);
        }

        /// <summary>The app's tools\install.ps1, hidden, its own process, logged to %LOCALAPPDATA%\Catalyzer\installs.</summary>
        private void RunInstall(Project p)
        {
            var a = ReadApp(p);
            if (a == null) { Say(p.Name.ToUpperInvariant() + " HAS NO TOOLS\\INSTALL.PS1"); return; }
            if (_installs.TryGetValue(p.Folder, out var running) && running.Code == null) { Say(p.Name.ToUpperInvariant() + " IS ALREADY INSTALLING"); return; }
            try
            {
                Directory.CreateDirectory(InstallLogs);
                var log = Path.Combine(InstallLogs, p.Name + ".log");
                // scripts that follow the contract take -Log; older ones get their output captured instead
                bool takesLog = Regex.IsMatch(File.ReadAllText(a.Install), @"\$Log\b", RegexOptions.IgnoreCase);
                var args = takesLog
                    ? "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + a.Install + "\" -Log \"" + log + "\""
                    : "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"& { & '" + a.Install.Replace("'", "''") + "' *>&1 | Out-File -Encoding utf8 '" + log.Replace("'", "''") + "'; exit $LASTEXITCODE }\"";
                var psi = new ProcessStartInfo("powershell.exe", args)
                { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = p.Folder };
                var run = new InstallRun { Log = log };
                var pr = new Process { StartInfo = psi, EnableRaisingEvents = true };
                pr.Exited += (s, e) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { run.Code = pr.ExitCode; } catch { run.Code = -1; }
                    run.Ended = DateTime.Now;
                    _appOf.Remove(p.Folder);
                    Log.Write(p.Name + (run.Code == 0 ? ": installed" : ": install failed (exit " + run.Code + "), see " + log), show: true);
                    PluginHost.I?.UpdateBadges();
                }));
                run.Proc = pr;
                _installs[p.Folder] = run;
                pr.Start();
                Say("INSTALLING " + p.Name.ToUpperInvariant() + "  //  BUILD, CLOSE, INSTALL, RESTART");
                PluginHost.I?.UpdateBadges();
            }
            catch (Exception ex) { Log.Write(p.Name + " install: " + ex.Message, show: true); }
        }
    }
}
