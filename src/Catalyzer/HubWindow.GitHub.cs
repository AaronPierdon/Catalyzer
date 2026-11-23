using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Path = System.IO.Path;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// The GitHub drawer: every repo on the account next to what's on this PC.
    ///   on GitHub, not here  → CLONE into the projects folder
    ///   on both              → git state; PUSH when this PC is ahead
    ///   only on this PC      → PUBLISH (creates a private repo, pushes)
    /// Nothing is ever deleted here, locally or on GitHub.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Grid _ghHost = new Grid { Visibility = Visibility.Collapsed };
        private readonly TranslateTransform _ghX = new TranslateTransform();
        private readonly StackPanel _ghList = new StackPanel();
        private readonly Chip _ghChip = new Chip(24) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        private TextBlock _ghTitle;
        private List<GitHub.Repo> _ghRepos;
        private string _ghError;
        private bool _ghLoading;
        private readonly HashSet<string> _ghBusy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private const double GhWidth = 760;

        private bool GitHubOpen => _ghHost.Visibility == Visibility.Visible;

        private void InitGitHub() { }

        private string ProjectsRoot => Prefs.Value.Roots.FirstOrDefault(Directory.Exists) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos");

        private void BuildGhShell()
        {
            var veil = new Rectangle { Fill = Brushes.Black, Opacity = 0.45 };
            veil.MouseLeftButtonDown += (s, e) => CloseGitHub();
            _ghHost.Children.Add(veil);
            var drawer = new Grid { Width = GhWidth, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 10, 10), RenderTransform = _ghX };
            drawer.Children.Add(new Glass { Cut = 16, Solid = true });
            var body = new Grid { Margin = new Thickness(24, 20, 18, 18) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition());
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _ghTitle = Cz.Head("", 20, "CzTextBrush"); _ghTitle.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(_ghTitle);
            Grid.SetColumn(_ghChip, 1); _ghChip.HorizontalAlignment = HorizontalAlignment.Left; top.Children.Add(_ghChip);
            var x = new CzIconButton("", CloseGitHub, "CLOSE  //  ESC"); Grid.SetColumn(x, 2); top.Children.Add(x);
            body.Children.Add(top);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) };
            btns.Children.Add(new CzButton("REFRESH", () => LoadRepos(), "", height: 28) { Margin = new Thickness(0, 0, 8, 0) });
            btns.Children.Add(new CzButton("NEW PROJECT", () => { CloseGitHub(); NewProject(); }, "", primary: true, height: 28) { Margin = new Thickness(0, 0, 8, 0) });
            btns.Children.Add(new CzButton("GITHUB.COM", () => Launch.Url("https://github.com/" + (Prefs.Value.GitHubLogin ?? "")), "", height: 28));
            Grid.SetRow(btns, 1); body.Children.Add(btns);
            var sv = new ScrollViewer { Content = _ghList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
            _ghList.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetRow(sv, 2); body.Children.Add(sv);
            drawer.Children.Add(body);
            Cz.Glow(drawer, Cz.T.Accent, 30, 0.35);
            _ghHost.Children.Add(drawer);
        }

        private void OpenGitHub()
        {
            if (_ghHost.Children.Count == 0) BuildGhShell();
            if (_drawerOpen) Drawer(false);
            if (FeedOpen) CloseFeed();
            if (NotesOpen) CloseNotes();
            Decode.Play(_ghTitle, "GITHUB" + (Prefs.Value.GitHubLogin != null ? "  //  " + Prefs.Value.GitHubLogin.ToUpperInvariant() : ""), 0.05, 0.4);
            GhRefresh();
            _ghHost.Visibility = Visibility.Visible;
            if (_ghRepos == null && !_ghLoading) LoadRepos();
            if (Cz.Still) { _ghX.X = 0; return; }
            _ghX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(GhWidth + 20, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private void CloseGitHub()
        {
            if (!GitHubOpen) return;
            if (Cz.Still) { _ghHost.Visibility = Visibility.Collapsed; return; }
            var a = new DoubleAnimation(0, GhWidth + 20, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            a.Completed += (s, e) => _ghHost.Visibility = Visibility.Collapsed;
            _ghX.BeginAnimation(TranslateTransform.XProperty, a);
        }

        private async void LoadRepos()
        {
            if (_ghLoading) return;
            _ghLoading = true; _ghError = null; GhRefresh();
            try
            {
                var login = await GitHub.Login();
                if (login != Prefs.Value.GitHubLogin) Prefs.Save(p => p.GitHubLogin = login);
                _ghRepos = await GitHub.Repos();
                Decode.Play(_ghTitle, "GITHUB  //  " + login.ToUpperInvariant(), 0, 0.3);
            }
            catch (Exception ex) { _ghError = ex.Message; Log.Write("github: " + ex.Message); }
            _ghLoading = false;
            if (GitHubOpen) GhRefresh();
        }

        /// <summary>Draw the three lists from the repos and the board.</summary>
        private void GhRefresh()
        {
            _ghList.Children.Clear();
            if (_ghLoading) { _ghChip.Set("ASKING GITHUB", Tone.Select, true, null); _ghList.Children.Add(Para("Asking GitHub for your repos... (the first time, GitHub may ask you to sign in)", 13, "CzTextFaintBrush")); return; }
            if (_ghError != null)
            {
                _ghChip.Set("NOT CONNECTED", Tone.Warn, false, null);
                _ghList.Children.Add(Para("Couldn't reach GitHub: " + _ghError, 13.5, "CzTextBrush"));
                _ghList.Children.Add(Para("Catalyzer uses the sign-in git has on this PC. Press REFRESH to try again; GitHub's sign-in window opens if git has none.", 12.5, "CzTextFaintBrush"));
                return;
            }
            if (_ghRepos == null) return;

            var byKey = _board.Items.Where(p => p.Git?.Remote != null).GroupBy(p => GitHub.Key(p.Git.Remote)).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.First());
            var notHere = _ghRepos.Where(r => !byKey.ContainsKey(r.FullName.ToLowerInvariant())).ToList();
            var both = _ghRepos.Where(r => byKey.ContainsKey(r.FullName.ToLowerInvariant())).ToList();
            var onlyHere = _board.Items.Where(p => p.Git != null && p.Git.Loaded && (!p.Git.IsRepo || GitHub.Key(p.Git.Remote) == null)).ToList();
            _ghChip.Set(_ghRepos.Count + " ON GITHUB  ·  " + notHere.Count + " NOT HERE  ·  " + onlyHere.Count + " ONLY HERE", Tone.Dim, false, null);

            Section(_ghList, "On GitHub, not on this PC", notHere.Count);
            foreach (var r in notHere) _ghList.Children.Add(RepoRow(r, null));
            if (notHere.Count == 0) _ghList.Children.Add(Para("—", 12.5, "CzTextFaintBrush"));
            Section(_ghList, "On GitHub and here", both.Count);
            foreach (var r in both) _ghList.Children.Add(RepoRow(r, byKey[r.FullName.ToLowerInvariant()]));
            Section(_ghList, "Only on this PC", onlyHere.Count);
            foreach (var p in onlyHere) _ghList.Children.Add(LocalRow(p));
            if (onlyHere.Count == 0) _ghList.Children.Add(Para("—", 12.5, "CzTextFaintBrush"));
        }

        private static void Section(Panel list, string label, int n)
        {
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, list.Children.Count == 0 ? 0 : 18, 0, 8) };
            head.Children.Add(Cz.Mono(label.ToUpperInvariant(), 10.5, "CzTextDimBrush"));
            head.Children.Add(Cz.Mono("  ·  " + n, 10.5, "CzTextFaintBrush"));
            list.Children.Add(head);
        }

        private static string Ago(DateTime? t)
        {
            if (t == null) return "";
            var s = DateTime.Now - t.Value;
            return s.TotalHours < 1 ? (int)Math.Max(1, s.TotalMinutes) + "m ago" : s.TotalDays < 1 ? (int)s.TotalHours + "h ago" : s.TotalDays < 60 ? (int)s.TotalDays + "d ago" : t.Value.ToString("yyyy-MM-dd");
        }

        /// <summary>One repo: name, visibility, last push, description; what you can do with it here.</summary>
        private FrameworkElement RepoRow(GitHub.Repo r, Project local)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 6), Background = Brushes.Transparent };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            var name = new StackPanel { Orientation = Orientation.Horizontal };
            name.Children.Add(Cz.Text(r.Name, 15, "CzTextBrush", "CzFontHead"));
            name.Children.Add(Cz.Mono("   " + (r.Private ? "PRIVATE" : "PUBLIC") + "  ·  " + (r.Owner ?? "").ToUpperInvariant() + "  ·  PUSHED " + Ago(r.Pushed).ToUpperInvariant(), 9.5, r.Private ? "CzTextFaintBrush" : "CzWarnBrush").Also(t => t.VerticalAlignment = VerticalAlignment.Center));
            left.Children.Add(name);
            var desc = local != null ? StatusLine(local) : (r.Description ?? "");
            if (desc != "") left.Children.Add(Cz.Text(desc, 12, "CzTextFaintBrush").Also(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.Margin = new Thickness(0, 2, 0, 0); }));
            row.Children.Add(left);
            var acts = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            acts.Children.Add(new CzIconButton("", () => Launch.Url(r.HtmlUrl), "OPEN ON GITHUB", 30, 12));
            if (local == null)
                acts.Children.Add(new CzButton(_ghBusy.Contains(r.FullName) ? "CLONING..." : "CLONE", () => Clone(r), "", height: 28, tip: "INTO " + ProjectsRoot.ToUpperInvariant() + "\\" + r.Name.ToUpperInvariant()) { Margin = new Thickness(8, 0, 0, 0) });
            else if (local.Git.Ahead > 0)
                acts.Children.Add(new CzButton(_ghBusy.Contains(r.FullName) ? "PUSHING..." : "PUSH " + local.Git.Ahead, () => Push(local, r.FullName), "", primary: true, height: 28, tip: "SEND " + local.Git.Ahead + " COMMIT(S) TO GITHUB") { Margin = new Thickness(8, 0, 0, 0) });
            Grid.SetColumn(acts, 1); row.Children.Add(acts);
            return row;
        }

        private static string StatusLine(Project p)
        {
            var g = p.Git; if (g == null || !g.IsRepo) return "";
            var bits = new List<string> { "HERE: " + (g.Branch ?? "?").ToUpperInvariant() };
            if (g.Changed > 0) bits.Add(g.Changed + " CHANGED");
            if (g.Ahead > 0) bits.Add(g.Ahead + " TO PUSH");
            if (g.Behind > 0) bits.Add(g.Behind + " TO PULL");
            if (bits.Count == 1) bits.Add("IN STEP");
            return string.Join("  ·  ", bits);
        }

        private FrameworkElement LocalRow(Project p)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(Cz.Text(p.Name, 15, "CzTextBrush", "CzFontHead"));
            left.Children.Add(Cz.Mono(p.Git.IsRepo ? (p.Git.Remote == null ? "GIT, NO REMOTE" : "REMOTE ISN'T GITHUB: " + p.Git.Remote) : "NOT A GIT REPO", 9.5, "CzTextFaintBrush"));
            row.Children.Add(left);
            if (p.Git.IsRepo && p.Git.Remote == null)
            {
                var b = new CzButton(_ghBusy.Contains(p.Folder) ? "PUBLISHING..." : "PUBLISH", () => Publish(p), "", height: 28, tip: "CREATE A GITHUB REPO FOR IT AND PUSH");
                Grid.SetColumn(b, 1); b.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(b);
            }
            return row;
        }

        // ================================================================== actions

        private async void Clone(GitHub.Repo r)
        {
            if (_ghBusy.Contains(r.FullName)) return;
            var dest = Path.Combine(ProjectsRoot, r.Name);
            if (Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any()) { Log.Write("clone: " + dest + " already exists and isn't empty", show: true); return; }
            _ghBusy.Add(r.FullName); GhRefresh(); Say("CLONING " + r.Name.ToUpperInvariant() + "...");
            var (code, output) = await GitHub.Git(null, "clone \"" + r.CloneUrl + "\" \"" + dest + "\"", 600000);
            _ghBusy.Remove(r.FullName);
            Log.Write(code == 0 ? "CLONED " + r.Name.ToUpperInvariant() + "  //  " + dest.ToUpperInvariant() : "CLONE FAILED  //  " + GitHub.First(output), show: true);
            if (code == 0) _board.Rescan();
            if (GitHubOpen) GhRefresh();
        }

        private async void Push(Project p, string key)
        {
            if (_ghBusy.Contains(key)) return;
            _ghBusy.Add(key); GhRefresh();
            var (code, output) = await GitHub.Git(p.Folder, "push", 120000);
            _ghBusy.Remove(key);
            Log.Write(code == 0 ? "PUSHED " + p.Name.ToUpperInvariant() : "PUSH FAILED  //  " + GitHub.First(output), show: true);
            await Projects.LoadGit(p); _board.Touch(p);
            if (GitHubOpen) GhRefresh();
        }

        /// <summary>PUBLISH: a new GitHub repo (private unless you say otherwise) for a local-only project, then push.</summary>
        private void Publish(Project p)
        {
            var d = Dialog("Publish " + p.Name + " to GitHub", 560, onEsc: null);
            d.Body.Children.Add(Para("Creates a new repo on your GitHub account and pushes this project's history to it. Nothing on this PC changes except the new remote.", 13.5));
            d.Body.Children.Add(Caption("repo name"));
            var (nameRow, name) = Field("Name on GitHub", Regex.Replace(p.Name, @"[^A-Za-z0-9._-]", "-"));
            d.Body.Children.Add(nameRow);
            bool priv = true;
            d.Body.Children.Add(new CzToggle(true, v => priv = v, "Private") { Margin = new Thickness(0, 4, 0, 6) });
            var (st, set) = Step(""); st.Visibility = Visibility.Collapsed; d.Body.Children.Add(st);
            d.Buttons.Children.Add(Btn("CANCEL", d.Close));
            var go = Btn("PUBLISH", null, "", primary: true);
            go.Click = async () =>
            {
                var n = name.Text.Trim();
                if (!Regex.IsMatch(n, @"^[A-Za-z0-9._-]{1,100}$")) { st.Visibility = Visibility.Visible; set("USE LETTERS, DIGITS, . _ - ONLY", Tone.Warn, false); return; }
                if ((await GitHub.Git(p.Folder, "rev-parse --verify HEAD")).code != 0) { st.Visibility = Visibility.Visible; set("THIS REPO HAS NO COMMITS YET: COMMIT ONCE, THEN PUBLISH", Tone.Warn, false); return; }
                go.IsEnabled = false; st.Visibility = Visibility.Visible; set("CREATING " + n.ToUpperInvariant() + " ON GITHUB...", Tone.Select, true);
                try
                {
                    var r = await GitHub.Create(n, p.Description, priv);
                    set("ADDING THE REMOTE...", Tone.Select, true);
                    var a = await GitHub.Git(p.Folder, "remote add origin \"" + r.CloneUrl + "\"");
                    if (a.code != 0) throw new Exception(GitHub.First(a.output));
                    set("PUSHING...", Tone.Select, true);
                    var ps = await GitHub.Git(p.Folder, "push -u origin HEAD", 300000);
                    if (ps.code != 0) throw new Exception(GitHub.First(ps.output));
                    set("PUBLISHED  //  " + r.HtmlUrl.ToUpperInvariant(), Tone.Ok, false);
                    Log.Write("published " + p.Name + " to " + r.HtmlUrl, show: true);
                    await Projects.LoadGit(p); _board.Touch(p);
                    _ghRepos = null; if (GitHubOpen) LoadRepos();
                    d.Buttons.Children.Clear(); d.Buttons.Children.Add(Btn("DONE", d.Close, primary: true));
                }
                catch (Exception ex) { set(ex.Message.ToUpperInvariant(), Tone.Danger, false); go.IsEnabled = true; }
            };
            d.Buttons.Children.Add(go);
        }
    }
}
