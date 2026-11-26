using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// GIT on every card: where the repo stands against GitHub and the one thing to do next, as a chip (click does it:
    /// PULL / PUSH / open the git drawer / Visual Studio for anything odd). The git drawer shows what's incoming and
    /// outgoing, the changed files with checkboxes, a message box, COMMIT and COMMIT + PUSH. FETCH ALL in the header
    /// checks every repo. All the git itself is in <see cref="Repo"/>, which refuses anything that isn't safe.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Dictionary<string, Repo.State> _repoOf = new Dictionary<string, Repo.State>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _repoBusy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _repoReading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Grid _gitHost = new Grid { Visibility = Visibility.Collapsed };
        private readonly TranslateTransform _gitX = new TranslateTransform();
        private readonly StackPanel _gitBody = new StackPanel();
        private TextBlock _gitTitle;
        private Project _gitFor;
        private readonly HashSet<string> _gitChecked = new HashSet<string>(StringComparer.Ordinal);
        private readonly TextBox _gitMsg = new TextBox();
        private bool _gitMsgTouched, _gitMsgSetting;
        private string _gitFirstFor;
        private DispatcherTimer _gitFetchTimer;
        private bool _fetchingAll;
        private const double GitWidth = 700;

        private bool GitOpen => _gitHost.Visibility == Visibility.Visible && _gitFor != null;

        private void InitGit()
        {
            PluginHost.I?.AddBadge(new ProjectBadge
            {
                Id = "git", Order = -20,
                State = GitBadge,
                Click = GitGo,
                Menu = GitMenu,
            });
            _board.GitLoaded += p => { _ = RefreshRepo(p.Folder, fetch: false); };
            // GitHub is asked every 15 minutes, quietly, so "TO PULL" shows up without a click
            _gitFetchTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _gitFetchTimer.Tick += (s, e) => FetchAll(quiet: true);
            if (!Prefs.Offline) _gitFetchTimer.Start();
            _gitMsg.TextChanged += (s, e) => { if (!_gitMsgSetting) _gitMsgTouched = _gitMsg.Text.Trim() != ""; };
        }

        /// <summary>Read where a repo stands (in the background); the chip and the drawer redraw when it answers.</summary>
        private async Task RefreshRepo(string folder, bool fetch)
        {
            if (Prefs.Offline) return;
            if (_repoReading.Contains(folder))
            {
                if (!fetch) return;   // a plain re-read is already on its way; a fetch waits its turn
                while (_repoReading.Contains(folder)) await Task.Delay(100);
            }
            _repoReading.Add(folder);
            try
            {
                var locked = _syncBusy.Contains(folder) ? "CATALYZER IS SYNCING THE NOTES  //  A MOMENT" : null;
                var st = await Task.Run(() => new Repo(folder).Read(fetch, locked));
                _repoOf[folder] = st;
                PluginHost.I?.UpdateBadges();
                if (GitOpen && string.Equals(_gitFor.Folder, folder, StringComparison.OrdinalIgnoreCase)) GitRefresh();
                if (IsSelf(folder)) CheckUpdate();
            }
            catch (Exception ex) { Log.Write("git " + folder + ": " + ex.Message); }
            finally { _repoReading.Remove(folder); }
        }

        private static string Since(DateTime? t)
        {
            if (t == null) return "NEVER";
            var d = DateTime.Now - t.Value;
            return d.TotalMinutes < 1 ? "JUST NOW" : d.TotalHours < 1 ? (int)d.TotalMinutes + " MIN AGO" : d.TotalDays < 1 ? (int)d.TotalHours + " H AGO" : (int)d.TotalDays + " DAYS AGO";
        }

        // ================================================================== the chip

        private BadgeState GitBadge(Project p)
        {
            if (_repoBusy.Contains(p.Folder)) return new BadgeState { Text = "GIT  ·  WORKING", Tone = Tone.Select, Pulse = true, Card = CardMood.Working, Tip = "GIT IS RUNNING FOR THIS REPO" };
            if (!_repoOf.TryGetValue(p.Folder, out var s) || s.Kind == Repo.Kind.NotRepo) return null;
            string checkedAt = "\nCHECKED GITHUB " + Since(s.Fetched);
            BadgeState B(string text, Tone tone, bool pulse, string click) => new BadgeState { Text = text, Tone = tone, Pulse = pulse, Tip = s.Why + checkedAt + "\nCLICK: " + click + "  //  RIGHT-CLICK: MORE" };
            if (s.LockedBy != null && s.Kind != Repo.Kind.InStep) return B("GIT  ·  WAIT", Tone.Dim, true, "OPEN THE GIT PANEL");
            return s.Kind switch
            {
                Repo.Kind.Pull when s.CanPull => B("PULL  ↓" + s.Behind, Tone.Accent, true, "PULL (FAST-FORWARD ONLY)"),
                Repo.Kind.Pull => B("↓" + s.Behind + "  ·  COMMIT FIRST", Tone.Warn, true, "OPEN THE GIT PANEL"),
                Repo.Kind.Push => B("PUSH  ↑" + s.Ahead, Tone.Select, true, "PUSH"),
                Repo.Kind.NoUpstream when s.CanPush => B("PUBLISH BRANCH", Tone.Select, true, "PUSH THE BRANCH TO GITHUB"),
                Repo.Kind.Changes => B("COMMIT  " + s.Changes.Count, Tone.Warn, false, "OPEN THE GIT PANEL"),
                Repo.Kind.Diverged => B("DIVERGED  ↕", Tone.Danger, true, "VISUAL STUDIO'S GIT WINDOW").Also(b => b.Card = CardMood.Warn),
                Repo.Kind.Conflict => B("CONFLICT", Tone.Danger, true, "VISUAL STUDIO'S GIT WINDOW").Also(b => b.Card = CardMood.Danger),
                Repo.Kind.Detached => B("DETACHED", Tone.Warn, false, "VISUAL STUDIO'S GIT WINDOW"),
                Repo.Kind.Gone => B("BRANCH GONE", Tone.Warn, false, "VISUAL STUDIO'S GIT WINDOW"),
                Repo.Kind.Busy => B("GIT BUSY", Tone.Warn, true, "CHECK AGAIN"),
                Repo.Kind.NoRemote => B("NOT ON GITHUB", Tone.Faint, false, "OPEN THE GIT PANEL"),
                Repo.Kind.InStep => B("✓  IN STEP", Tone.Faint, false, "CHECK GITHUB NOW"),
                _ => null,
            };
        }

        /// <summary>The chip's click: the one sensible next step.</summary>
        private void GitGo(Project p)
        {
            if (p == null || _repoBusy.Contains(p.Folder)) return;
            if (!_repoOf.TryGetValue(p.Folder, out var s)) { _ = RefreshRepo(p.Folder, fetch: true); return; }
            if (s.LockedBy != null && s.Kind != Repo.Kind.InStep) { OpenGit(p); return; }
            switch (s.Kind)
            {
                case Repo.Kind.Pull when s.CanPull: DoPull(p); break;
                case Repo.Kind.Push when s.CanPush: case Repo.Kind.NoUpstream when s.CanPush: DoPush(p); break;
                case Repo.Kind.Diverged: case Repo.Kind.Conflict: case Repo.Kind.Detached: case Repo.Kind.Gone: OpenVsGit(p); break;
                case Repo.Kind.InStep: case Repo.Kind.Busy: CheckNow(p); break;
                default: OpenGit(p); break;
            }
        }

        private IEnumerable<MenuEntry> GitMenu(Project p)
        {
            _repoOf.TryGetValue(p.Folder, out var s);
            var list = new List<MenuEntry>();
            if (s != null && s.CanPull) list.Add(new MenuEntry { Text = "Pull", Glyph = "", Hint = s.Behind + " from GitHub", Run = () => DoPull(p) });
            if (s != null && s.CanPush) list.Add(new MenuEntry { Text = "Push", Glyph = "", Hint = s.HasUpstream ? s.Ahead + " to GitHub" : "publish the branch", Run = () => DoPush(p) });
            list.Add(new MenuEntry { Text = "Git panel", Glyph = "", Hint = "changes · commit · incoming / outgoing", Run = () => OpenGit(p) });
            list.Add(new MenuEntry { Text = "Check GitHub now", Glyph = "", Hint = s != null ? "checked " + Since(s.Fetched).ToLowerInvariant() : null, Run = () => CheckNow(p) });
            list.Add(new MenuEntry { Separator = true });
            list.Add(new MenuEntry { Text = "Visual Studio: Git Changes", Glyph = "", Hint = p.Solution != null ? System.IO.Path.GetFileName(p.Solution) : "the folder", Run = () => OpenVsGit(p) });
            if (p.GitHub != null) list.Add(new MenuEntry { Text = "Open on GitHub", Glyph = "", Run = () => Launch.Url(p.GitHub) });
            return list;
        }

        private async void CheckNow(Project p)
        {
            Say("CHECKING GITHUB FOR " + p.Name.ToUpperInvariant());
            await RefreshRepo(p.Folder, fetch: true);
            await Projects.LoadGit(p); _board.Touch(p);
            if (_repoOf.TryGetValue(p.Folder, out var s)) Say(p.Name.ToUpperInvariant() + "  //  " + s.Why);
        }

        // ================================================================== doing

        private async void DoPull(Project p) => await RunGit(p, "PULLING", r => r.Pull());
        private async void DoPush(Project p) => await RunGit(p, "PUSHING", r => r.Push());

        /// <summary>One git action at a time per repo; the result goes to the footer and the log; the card re-reads.</summary>
        private async Task<bool> RunGit(Project p, string doing, Func<Repo, Task<(bool ok, string say)>> act)
        {
            if (p == null || _repoBusy.Contains(p.Folder)) return false;
            if (_syncBusy.Contains(p.Folder)) { Say("NOTES ARE SYNCING  //  TRY AGAIN IN A MOMENT"); return false; }
            _repoBusy.Add(p.Folder);
            PluginHost.I?.UpdateBadges(); if (GitOpen && _gitFor == p) GitRefresh();
            Say(doing + "  //  " + p.Name.ToUpperInvariant());
            (bool ok, string say) r;
            try { r = await act(new Repo(p.Folder)); }
            catch (Exception ex) { r = (false, "GIT FAILED  //  " + ex.Message); }
            _repoBusy.Remove(p.Folder);
            Log.Write(p.Name + " git: " + r.say, show: true);
            await RefreshRepo(p.Folder, fetch: false);
            await Projects.LoadGit(p); _board.Touch(p);
            RefreshSync(p.Folder, fetch: false);
            return r.ok;
        }

        /// <summary>FETCH ALL: ask GitHub about every repo (three at a time), then say what needs doing.</summary>
        private async void FetchAll(bool quiet)
        {
            if (_fetchingAll || Prefs.Offline) return;
            _fetchingAll = true;
            if (!quiet) Say("CHECKING GITHUB FOR EVERY PROJECT");
            try
            {
                var items = _board.Items.Where(p => p.Git?.IsRepo != false).ToList();
                using var gate = new System.Threading.SemaphoreSlim(3);
                await Task.WhenAll(items.Select(async p =>
                {
                    await gate.WaitAsync();
                    try { await RefreshRepo(p.Folder, fetch: true); await Projects.LoadGit(p); _board.Touch(p); }
                    finally { gate.Release(); }
                }));
                var st = items.Select(p => _repoOf.TryGetValue(p.Folder, out var s) ? s : null).Where(s => s != null).ToList();
                var parts = new List<string>();
                void Add(int n, string what) { if (n > 0) parts.Add(n + " " + what); }
                Add(st.Count(s => s.Kind == Repo.Kind.Pull), "TO PULL");
                Add(st.Count(s => s.Kind == Repo.Kind.Push || (s.Kind == Repo.Kind.NoUpstream && s.CanPush)), "TO PUSH");
                Add(st.Count(s => s.Kind == Repo.Kind.Diverged), "DIVERGED");
                Add(st.Count(s => s.Kind == Repo.Kind.Conflict), "IN CONFLICT");
                Add(st.Count(s => s.Changes.Count > 0), "WITH CHANGES");
                if (!quiet || parts.Count > 0) Say("GITHUB CHECKED  //  " + (parts.Count == 0 ? "EVERYTHING IN STEP" : string.Join("  ·  ", parts)));
            }
            catch (Exception ex) { Log.Write("fetch all: " + ex.Message); }
            finally { _fetchingAll = false; }
        }

        // ================================================================== Visual Studio

        private static string _devenv;
        /// <summary>The newest Visual Studio (Community included), through vswhere; "" when there's none.</summary>
        private static string Devenv()
        {
            if (_devenv != null) return _devenv == "" ? null : _devenv;
            _devenv = "";
            try
            {
                var vswhere = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
                if (File.Exists(vswhere))
                {
                    var psi = new ProcessStartInfo(vswhere, "-latest -prerelease -products * -property productPath") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    using var pr = Process.Start(psi);
                    var o = pr.StandardOutput.ReadToEnd().Trim(); pr.WaitForExit(5000);
                    var first = o.Split('\n').Select(x => x.Trim()).FirstOrDefault(File.Exists);
                    if (first != null) _devenv = first;
                }
            }
            catch (Exception ex) { Log.Write("vswhere: " + ex.Message); }
            return _devenv == "" ? null : _devenv;
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);

        /// <summary>The project's solution (or folder) in Visual Studio, opened on the Git Changes window. If Visual Studio
        /// already has it open, that window comes to the front instead of a second copy.</summary>
        private void OpenVsGit(Project p)
        {
            var target = p.Solution != null && File.Exists(p.Solution) ? p.Solution : p.Folder;
            var name = System.IO.Path.GetFileNameWithoutExtension(target);
            try
            {
                foreach (var vs in Process.GetProcessesByName("devenv"))
                    if (vs.MainWindowHandle != IntPtr.Zero && vs.MainWindowTitle.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsIconic(vs.MainWindowHandle)) ShowWindow(vs.MainWindowHandle, 9);
                        SetForegroundWindow(vs.MainWindowHandle);
                        Say("VISUAL STUDIO ALREADY HAS " + name.ToUpperInvariant() + "  //  GIT CHANGES: CTRL+0, CTRL+G");
                        return;
                    }
            }
            catch { }
            var dev = Devenv();
            if (dev == null) { Launch.Shell(target, null); Say("OPENED  //  VISUAL STUDIO WASN'T FOUND, SO WINDOWS PICKED THE APP"); return; }
            Launch.Shell(dev, "\"" + target + "\" /Command View.GitWindow", p.Folder);
            Say("VISUAL STUDIO  //  " + name.ToUpperInvariant() + " ON THE GIT CHANGES WINDOW");
        }

        // ================================================================== the drawer

        private void BuildGitShell()
        {
            var veil = new Rectangle { Fill = Brushes.Black, Opacity = 0.45 };
            veil.MouseLeftButtonDown += (s, e) => CloseGit();
            _gitHost.Children.Add(veil);
            var drawer = new Grid { Width = GitWidth, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 10, 10), RenderTransform = _gitX };
            drawer.Children.Add(new Glass { Cut = 16, Solid = true });
            var outer = new Grid { Margin = new Thickness(24, 20, 18, 18) };
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition());
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _gitTitle = Cz.Head("", 20, "CzTextBrush"); _gitTitle.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(_gitTitle);
            var x = new CzIconButton("", CloseGit, "CLOSE  //  ESC"); Grid.SetColumn(x, 1); top.Children.Add(x);
            outer.Children.Add(top);
            var scroll = new ScrollViewer { Content = _gitBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Margin = new Thickness(0, 10, 0, 0) };
            _gitBody.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetRow(scroll, 1); outer.Children.Add(scroll);
            drawer.Children.Add(outer);
            Cz.Glow(drawer, Cz.T.Accent, 30, 0.35);
            _gitHost.Children.Add(drawer);

            Field(_gitMsg, 13, "CzFontUi");
            _gitMsg.AcceptsReturn = true; _gitMsg.TextWrapping = TextWrapping.Wrap; _gitMsg.MinHeight = 54; _gitMsg.MaxHeight = 140;
            _gitMsg.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; _gitMsg.Margin = new Thickness(12, 8, 12, 8);
            _gitMsg.ToolTip = "THE COMMIT MESSAGE  //  WHAT CHANGED AND WHY";
        }

        private void OpenGit(Project p)
        {
            if (_gitHost.Children.Count == 0) BuildGitShell();
            if (_drawerOpen) Drawer(false);
            if (FeedOpen) CloseFeed();
            if (NotesOpen) CloseNotes();
            if (_gitFirstFor != p.Folder) { _gitChecked.Clear(); _gitMsgTouched = false; _gitFirstFor = p.Folder; SetMsg(""); }
            _gitFor = p;
            Decode.Play(_gitTitle, (p.Name + "  //  GIT").ToUpperInvariant(), 0.05, 0.4);
            _gitHost.Visibility = Visibility.Visible;
            GitRefresh();
            _ = RefreshRepo(p.Folder, fetch: true);
            if (Cz.Still) { _gitX.X = 0; return; }
            _gitX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(GitWidth + 20, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private void CloseGit()
        {
            _gitFor = null;
            if (_gitHost.Visibility != Visibility.Visible) return;
            if (Cz.Still) { _gitHost.Visibility = Visibility.Collapsed; return; }
            var a = new DoubleAnimation(0, GitWidth + 20, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            a.Completed += (s, e) => { if (_gitFor == null) _gitHost.Visibility = Visibility.Collapsed; };
            _gitX.BeginAnimation(TranslateTransform.XProperty, a);
        }

        private void SetMsg(string text) { _gitMsgSetting = true; _gitMsg.Text = text; _gitMsg.CaretIndex = text.Length; _gitMsgSetting = false; }

        private static CzButton GitBtn(string label, Action click, string glyph, bool enabled, string tip, bool primary = false)
        {
            var b = new CzButton(label, click, glyph, primary: primary && enabled, height: 30, tip: tip) { Margin = new Thickness(0, 0, 8, 8), IsEnabled = enabled, Opacity = enabled ? 1 : 0.38 };
            ToolTipService.SetShowOnDisabled(b, true);
            return b;
        }

        private static FrameworkElement GitHead(string text, int count, string brush = "CzTextDimBrush")
        {
            var h = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 6) };
            h.Children.Add(Cz.Mono(text, 10.5, brush));
            h.Children.Add(Cz.Mono("  ·  " + count, 10.5, "CzTextFaintBrush"));
            return h;
        }

        private void GitRefresh()
        {
            var p = _gitFor; if (p == null) return;
            _gitBody.Children.Clear();
            bool busy = _repoBusy.Contains(p.Folder);
            if (!_repoOf.TryGetValue(p.Folder, out var s)) { _gitBody.Children.Add(Cz.Mono("READING THE REPO...", 10.5, "CzTextFaintBrush")); return; }
            if (s.Kind == Repo.Kind.NotRepo) { _gitBody.Children.Add(Cz.Text("This folder isn't a git repo. Publish it from the GitHub panel to start one.", 13.5, "CzTextDimBrush")); return; }

            // where it stands
            var where = (s.Branch ?? "(NO BRANCH)").ToUpperInvariant() + (s.Upstream != null ? "  →  " + s.Upstream.ToUpperInvariant() : s.HasOrigin ? "  →  NOT ON GITHUB YET" : "  //  NO GITHUB REMOTE") + "   //   CHECKED GITHUB " + Since(s.Fetched);
            _gitBody.Children.Add(Cz.Mono(where, 10, "CzTextFaintBrush").Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.ToolTip = where; }));
            var bad = s.Kind == Repo.Kind.Conflict || s.Kind == Repo.Kind.Diverged;
            var warn = s.Kind == Repo.Kind.Detached || s.Kind == Repo.Kind.Gone || s.Kind == Repo.Kind.Busy || s.LockedBy != null || (s.Kind == Repo.Kind.Pull && !s.CanPull);
            var why = Cz.Text(s.LockedBy ?? s.Why, 14, bad ? "CzDangerBrush" : warn ? "CzWarnBrush" : s.Kind == Repo.Kind.InStep ? "CzOkBrush" : "CzAccentBrush");
            why.TextWrapping = TextWrapping.Wrap; why.TextTrimming = TextTrimming.None; why.Margin = new Thickness(0, 6, 0, 12);
            _gitBody.Children.Add(why);

            var row = new WrapPanel();
            row.Children.Add(GitBtn(busy ? "WORKING..." : "PULL" + (s.Behind > 0 ? "  ↓" + s.Behind : ""), () => DoPull(p), "", s.CanPull && !busy, s.CanPull ? "FETCH + FAST-FORWARD  //  NEVER MERGES, NEVER TOUCHES UNCOMMITTED WORK" : s.PullBlock, primary: s.CanPull));
            row.Children.Add(GitBtn(busy ? "WORKING..." : "PUSH" + (s.Ahead > 0 ? "  ↑" + s.Ahead : ""), () => DoPush(p), "", s.CanPush && !busy, s.CanPush ? (s.HasUpstream ? "CHECKS GITHUB, THEN PUSHES  //  NEVER FORCES" : "PUBLISHES THE BRANCH  //  ONLY IF GITHUB HAS NONE BY THAT NAME") : s.PushBlock, primary: s.CanPush && !s.CanPull));
            row.Children.Add(GitBtn("CHECK GITHUB", () => CheckNow(p), "", !busy, "FETCH  //  CHANGES NOTHING HERE"));
            row.Children.Add(GitBtn("VISUAL STUDIO", () => OpenVsGit(p), "", true, "THE SOLUTION ON THE GIT CHANGES WINDOW  //  FOR CONFLICTS, MERGES, BRANCHES, HISTORY", primary: bad || s.Kind == Repo.Kind.Detached || s.Kind == Repo.Kind.Gone));
            if (p.GitHub != null) row.Children.Add(GitBtn("GITHUB", () => Launch.Url(p.GitHub), "", true, p.GitHub.ToUpperInvariant()));
            _gitBody.Children.Add(row);

            // incoming / outgoing
            void Commits(string head, List<Repo.Commit> list, string brush)
            {
                if (list.Count == 0) return;
                _gitBody.Children.Add(GitHead(head, list.Count, brush));
                foreach (var c in list.Take(12))
                {
                    var g = new Grid { Margin = new Thickness(4, 1, 0, 1) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.Children.Add(Cz.Mono(c.Hash, 10, "CzTextFaintBrush"));
                    var t = Cz.Text(c.Subject, 12.5, "CzTextBrush"); t.TextTrimming = TextTrimming.CharacterEllipsis; t.ToolTip = c.Subject; Grid.SetColumn(t, 1); g.Children.Add(t);
                    var w = Cz.Mono(c.When.ToUpperInvariant(), 9.5, "CzTextFaintBrush"); w.Margin = new Thickness(10, 0, 0, 0); Grid.SetColumn(w, 2); g.Children.Add(w);
                    _gitBody.Children.Add(g);
                }
                if (list.Count > 12) _gitBody.Children.Add(Cz.Mono("+" + (list.Count - 12) + " MORE", 9.5, "CzTextFaintBrush").Also(t => t.Margin = new Thickness(74, 2, 0, 0)));
            }
            Commits("ON GITHUB, NOT HERE", s.Incoming, "CzAccentBrush");
            Commits(s.HasUpstream ? "HERE, NOT ON GITHUB" : "NOT ON GITHUB YET", s.Outgoing, "CzSelectBrush");

            // changes + commit
            if (s.Changes.Count == 0) { _gitBody.Children.Add(GitHead("CHANGES", 0)); _gitBody.Children.Add(Cz.Text("Nothing uncommitted.", 12.5, "CzTextFaintBrush").Also(t => t.Margin = new Thickness(4, 0, 0, 0))); return; }
            var paths = new HashSet<string>(s.Changes.Select(c => c.Path), StringComparer.Ordinal);
            _gitChecked.RemoveWhere(x => !paths.Contains(x));
            if (_gitChecked.Count == 0 && !_gitMsgTouched) foreach (var c in s.Changes.Where(c => c.Suggested)) _gitChecked.Add(c.Path);

            var ch = new Grid();
            ch.ColumnDefinitions.Add(new ColumnDefinition()); ch.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ch.Children.Add(GitHead("CHANGES", s.Changes.Count, "CzWarnBrush"));
            var quick = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4) };
            void Q(string label, Action a) { var t = Cz.Mono(label, 9.5, "CzAccentBrush"); t.Cursor = Cursors.Hand; t.Margin = new Thickness(12, 0, 0, 0); t.MouseLeftButtonUp += (o, e) => { a(); GitRefresh(); }; quick.Children.Add(t); }
            Q("SUGGESTED", () => { _gitChecked.Clear(); foreach (var c in s.Changes.Where(c => c.Suggested)) _gitChecked.Add(c.Path); if (!_gitMsgTouched) SetMsg(""); });
            Q("ALL", () => { foreach (var c in s.Changes.Where(c => !c.Conflict)) _gitChecked.Add(c.Path); });
            Q("NONE", () => _gitChecked.Clear());
            Grid.SetColumn(quick, 1); ch.Children.Add(quick);
            _gitBody.Children.Add(ch);

            foreach (var c in s.Changes.Take(300)) _gitBody.Children.Add(ChangeRow(c, s.CanCommit && !busy));
            if (s.Changes.Count > 300) _gitBody.Children.Add(Cz.Mono("+" + (s.Changes.Count - 300) + " MORE  //  USE VISUAL STUDIO FOR THIS MANY", 9.5, "CzTextFaintBrush"));

            if (!_gitMsgTouched) SetMsg(Repo.SuggestMessage(s.Changes.Where(c => _gitChecked.Contains(c.Path)).Select(c => c.Path).ToList()));
            var box = new Grid { Margin = new Thickness(0, 12, 0, 10) };
            var frame = new System.Windows.Shapes.Path { StrokeThickness = 1 };
            frame.SetResourceReference(Shape.StrokeProperty, "CzLineBrush");
            box.SizeChanged += (o, e) => frame.Data = Cz.Chamfer(Math.Max(4, box.ActualWidth), Math.Max(4, box.ActualHeight), 7);
            box.Children.Add(Cz.Under(frame));
            if (_gitMsg.Parent is Panel old) old.Children.Remove(_gitMsg);
            box.Children.Add(_gitMsg);
            _gitBody.Children.Add(box);

            int n = _gitChecked.Count;
            bool can = s.CanCommit && !busy && n > 0;
            string block = !s.CanCommit ? (s.LockedBy ?? s.Why) : n == 0 ? "TICK THE FILES TO COMMIT" : null;
            var go = new WrapPanel();
            go.Children.Add(GitBtn("COMMIT " + n, () => DoCommit(p, push: false), "", can, block ?? "COMMITS EXACTLY THE TICKED FILES"));
            bool pushAfter = s.HasOrigin && s.Behind == 0 && s.Kind != Repo.Kind.Diverged && s.Kind != Repo.Kind.Gone;
            go.Children.Add(GitBtn("COMMIT + PUSH", () => DoCommit(p, push: true), "", can && pushAfter, block ?? (pushAfter ? "COMMIT, THEN CHECK GITHUB AND PUSH" : "GITHUB IS AHEAD: COMMIT, THEN PULL ISN'T POSSIBLE HERE  //  COMMIT ONLY"), primary: true));
            var risky = s.Changes.Count(c => c.Risk != null && _gitChecked.Contains(c.Path));
            if (risky > 0) go.Children.Add(Cz.Mono("⚠  " + risky + " TICKED FILE(S) FLAGGED: CHECK THEM", 10, "CzWarnBrush").Also(t => { t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(4, 0, 0, 8); }));
            _gitBody.Children.Add(go);
        }

        private FrameworkElement ChangeRow(Repo.Change c, bool enabled)
        {
            bool on = _gitChecked.Contains(c.Path);
            var row = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 1, 0, 1), MinHeight = 26, Cursor = enabled && !c.Conflict ? Cursors.Hand : null };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var box = new Grid { Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            var frame = new System.Windows.Shapes.Path { Data = Cz.Chamfer(15, 15, 4), StrokeThickness = 1 };
            frame.SetResourceReference(Shape.StrokeProperty, on ? "CzSelectBrush" : "CzTextDimBrush");
            box.Children.Add(frame);
            if (on) { var tick = Cz.Text("", 9.5, "CzSelectBrush", "CzFontIcons"); tick.HorizontalAlignment = HorizontalAlignment.Center; tick.VerticalAlignment = VerticalAlignment.Center; box.Children.Add(tick); }
            row.Children.Add(box);
            var path = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var t = Cz.Text(c.Path, 12.5, c.Conflict ? "CzDangerBrush" : on ? "CzTextBrush" : "CzTextDimBrush", "CzFontMono");
            t.TextTrimming = TextTrimming.CharacterEllipsis; t.ToolTip = c.Path;
            path.Children.Add(t);
            if (c.Risk != null) path.Children.Add(Cz.Mono("⚠  " + c.Risk, 9.5, "CzWarnBrush"));
            Grid.SetColumn(path, 1); row.Children.Add(path);
            var what = Cz.Mono(c.What, 9.5, c.Conflict ? "CzDangerBrush" : c.Untracked ? "CzAccentBrush" : "CzTextFaintBrush");
            what.VerticalAlignment = VerticalAlignment.Center; what.Margin = new Thickness(10, 0, 4, 0);
            Grid.SetColumn(what, 2); row.Children.Add(what);
            if (enabled && !c.Conflict)
                row.MouseLeftButtonUp += (s, e) => { if (!_gitChecked.Remove(c.Path)) _gitChecked.Add(c.Path); GitRefresh(); };
            return row;
        }

        private async void DoCommit(Project p, bool push)
        {
            var files = _gitChecked.ToList(); var msg = _gitMsg.Text;
            bool ok = await RunGit(p, "COMMITTING", r => r.CommitFiles(files, msg));
            if (!ok) return;
            _gitChecked.Clear(); _gitMsgTouched = false; SetMsg("");
            if (push) await RunGit(p, "PUSHING", r => r.Push());
            if (GitOpen && _gitFor == p) GitRefresh();
        }
    }
}
