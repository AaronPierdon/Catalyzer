using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// UPDATE: Catalyzer updates itself from its own repo, like a real app. When the repo on the board says a newer
    /// version than the one running (src/Catalyzer/Catalyzer.csproj &lt;Version&gt;), an UPDATE chip lights in the header.
    /// One click runs tools\install.ps1 hidden: it builds first (a failed build leaves this copy running and says so),
    /// then closes Catalyzer, installs on top, and starts the new one. Settings, links and notes backups stay.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Chip _updateChip = new Chip(32) { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        private bool _updating;
        private static readonly string UpdateLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Catalyzer", "update.log");

        /// <summary>The Catalyzer repo itself (it has the app and the principles).</summary>
        private static bool IsSelf(string folder) =>
            File.Exists(Path.Combine(folder, "src", "Catalyzer", "Catalyzer.csproj")) && File.Exists(Path.Combine(folder, "principles", "VERSION"));

        private Project SelfProject() => _board.Items.FirstOrDefault(p => IsSelf(p.Folder));

        private static Version RepoVersion(string folder)
        {
            try
            {
                var m = Regex.Match(File.ReadAllText(Path.Combine(folder, "src", "Catalyzer", "Catalyzer.csproj")), @"<Version>\s*([0-9]+(\.[0-9]+){1,3})\s*</Version>");
                return m.Success && Version.TryParse(m.Groups[1].Value, out var v) ? v : null;
            }
            catch { return null; }
        }

        private static Version RunningVersion() => typeof(HubWindow).Assembly.GetName().Version ?? new Version(0, 0);

        private static string V3(Version v) => v.Build >= 0 ? v.ToString(3) : v.ToString(2);

        /// <summary>Light the UPDATE chip when the repo is ahead of what's running.</summary>
        private void CheckUpdate()
        {
            if (Prefs.Offline || _updating || _updateChip.Tag as string == "failed") return;
            var self = SelfProject(); var have = RunningVersion();
            var repo = self != null ? RepoVersion(self.Folder) : null;
            if (repo == null || repo <= new Version(have.Major, have.Minor, Math.Max(0, have.Build)))
            {
                if (_updateChip.Tag as string != "failed") _updateChip.Visibility = Visibility.Collapsed;
                return;
            }
            _updateChip.Click = c => RunUpdate();
            _updateChip.RightClick = null;
            _updateChip.Set("UPDATE  " + V3(have) + " → " + V3(repo), Tone.Select, true,
                "THE CATALYZER REPO HAS " + V3(repo) + " (RUNNING " + V3(have) + ")\nCLICK: BUILD, CLOSE, INSTALL ON TOP, RESTART  //  ABOUT A MINUTE\nSETTINGS, LINKS AND NOTES BACKUPS STAY");
            _updateChip.Tag = null;
            if (_updateChip.Visibility != Visibility.Visible) { _updateChip.Visibility = Visibility.Visible; Say("CATALYZER " + V3(repo) + " IS READY  //  UPDATE IN THE HEADER"); }
        }

        private void RunUpdate()
        {
            var self = SelfProject();
            if (self == null || _updating) return;
            int work = PluginHost.I?.Running ?? 0;
            if (work > 0)
            {
                var d = Dialog("Update Catalyzer", 560, onEsc: null);
                d.Body.Children.Add(Para(work + " helper(s) or link request(s) are running. Updating restarts Catalyzer, which stops them.", 13.5, "CzTextBrush"));
                d.Buttons.Children.Add(Btn("NOT NOW", d.Close));
                d.Buttons.Children.Add(Btn("UPDATE ANYWAY", () => { d.Close(); StartUpdate(self); }, "", primary: true));
                return;
            }
            StartUpdate(self);
        }

        private void StartUpdate(Project self)
        {
            var script = Path.Combine(self.Folder, "tools", "install.ps1");
            if (!File.Exists(script)) { Log.Write("update: " + script + " is missing", show: true); return; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(UpdateLog));
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -Log \"" + UpdateLog + "\"")
                {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = self.Folder,
                };
                var pr = new Process { StartInfo = psi, EnableRaisingEvents = true };
                pr.Exited += (s, e) => Dispatcher.BeginInvoke(new Action(() =>
                {
                    // still here, so the install never got as far as closing us: it failed (usually the build)
                    _updating = false;
                    int code = 0; try { code = pr.ExitCode; } catch { }
                    _updateChip.Tag = "failed";
                    _updateChip.Click = c => Launch.Shell(UpdateLog, null);
                    _updateChip.RightClick = c => { _updateChip.Tag = null; RunUpdate(); };
                    _updateChip.Set("UPDATE FAILED", Tone.Danger, false, "THE BUILD OR INSTALL STOPPED (EXIT " + code + "). THIS COPY KEEPS RUNNING.\nCLICK: OPEN THE LOG  //  RIGHT-CLICK: TRY AGAIN\n" + UpdateLog.ToUpperInvariant());
                    _updateChip.Visibility = Visibility.Visible;
                    Log.Write("update failed (exit " + code + "), see " + UpdateLog, show: true);
                }));
                pr.Start();
                _updating = true;
                _updateChip.Click = null;
                _updateChip.Set("UPDATING  //  BUILDING", Tone.Select, true, "BUILDING THE NEW VERSION  //  CATALYZER RESTARTS BY ITSELF WHEN IT'S READY");
                Say("UPDATING CATALYZER  //  BUILDING, THEN IT RESTARTS");
                Log.Write("update: started " + script);
            }
            catch (Exception ex) { Log.Write("update: " + ex.Message, show: true); }
        }
    }
}
