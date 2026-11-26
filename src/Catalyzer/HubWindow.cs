using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>The command center: header (reactor, title, live stats, search, tools), the card field, the info bar.</summary>
    public sealed partial class HubWindow : Window
    {
        private readonly Board _board;
        private readonly Grid _root = new Grid();
        private readonly ContentControl _backHost = new ContentControl { IsHitTestVisible = false, Focusable = false };
        private readonly WrapPanel _cards = new WrapPanel { Margin = new Thickness(28, 6, 10, 20) };
        private readonly ScrollViewer _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        private readonly TextBox _search = new TextBox();
        private readonly TextBlock _footer = Cz.Mono("", 10.5, "CzTextDimBrush"), _footerRight = Cz.Mono("", 10, "CzTextFaintBrush");
        private readonly StackPanel _stats = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        private readonly Led _footLed = new Led(Colors.Gray, 8);
        private readonly Dictionary<Project, ProjectCard> _cardOf = new Dictionary<Project, ProjectCard>();
        private readonly CzMenu _menu = new CzMenu();
        private WinButton _maxBtn;
        private readonly TextBlock _title;

        public HubWindow(Board board)
        {
            _board = board;
            Title = "Catalyzer";
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
            var pv = Prefs.Value;
            Width = Math.Max(1000, pv.Width); Height = Math.Max(640, pv.Height); MinWidth = 900; MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
            SetResourceReference(BackgroundProperty, "CzBg0Brush");
            SetResourceReference(FontFamilyProperty, "CzFontUi");
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Resources.MergedDictionaries.Add(Styles.Scroll());
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/catalyzer.ico"));

            _root.Children.Add(_backHost);
            var body = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
            body.RowDefinitions.Add(new RowDefinition());
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            body.Children.Add(Header(out _title));
            _scroll.Content = _cards;
            _scroll.SizeChanged += (s, e) => Fit();
            _scroll.ScrollChanged += (s, e) => { if (e.ViewportWidthChange != 0) Fit(); };   // the scroll bar arriving narrows the row
            Grid.SetRow(_scroll, 1); body.Children.Add(_scroll);
            body.Children.Add(Footer());
            _root.Children.Add(body);
            _root.Children.Add(Rim());
            _root.Children.Add(_drawerHost);
            _root.Children.Add(_notesHost);
            _root.Children.Add(_gitHost);
            _root.Children.Add(_ghHost);
            _root.Children.Add(_feedHost);
            _root.Children.Add(_promptHost);
            _root.Children.Add(_dialogHost);
            Content = _root;

            ThemeService.I.Changed += OnTheme;
            Visuals.Hook();
            Visuals.Changed += () => { if (!Visuals.Glow) NoGlow(_root); };
            OnTheme();
            Log.Shown += line => Dispatcher.BeginInvoke(new Action(() => Say(line)));
            _board.Changed += BuildCards;
            _board.GitLoaded += p => { if (_cardOf.TryGetValue(p, out var c)) c.Git(); Stats(); };
            _board.ActionsChanged += () => { foreach (var c in _cardOf.Values) c.BuildButtons(); };
            _search.TextChanged += (s, e) => Filter();
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { _search.Focus(); _search.SelectAll(); e.Handled = true; }
                else if (e.Key == Key.F5) { _board.Rescan(); e.Handled = true; }
                else if (e.Key == Key.Escape) { if (_noteEditCancel != null) _noteEditCancel(); else if (DialogOpen) _dialogEsc?.Invoke(); else if (PromptOpen) ClosePrompt(); else if (GitHubOpen) CloseGitHub(); else if (FeedOpen) CloseFeed(); else if (GitOpen) CloseGit(); else if (NotesOpen) CloseNotes(); else if (_drawerOpen) Drawer(false); else if (_search.Text != "") _search.Text = ""; e.Handled = true; }
            };
            StateChanged += (s, e) => { Visuals.SetMinimized(WindowState == WindowState.Minimized); _maxBtn?.InvalidateVisual(); _root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0); };
            Activated += (s, e) => { if ((DateTime.Now - _board.LastScan).TotalSeconds > 20 && !_board.Scanning) _board.RefreshGit(); };
            Closing += (s, e) => Prefs.Save(p => { p.Maximized = WindowState == WindowState.Maximized; if (WindowState == WindowState.Normal) { p.Width = ActualWidth; p.Height = ActualHeight; } });
            if (pv.Maximized) Loaded += (s, e) => WindowState = WindowState.Maximized;
            var clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            clock.Tick += (s, e) => { foreach (var c in _cardOf.Values) c.Git(); FooterRight(); };
            clock.Start();
            InitWork();
            InitFeed();
            InitNotes();
            InitGit();
            InitApps();
            InitGitHub();
            InitDevSpace();
            Loaded += (s, e) => { if (!Prefs.Value.FirstRunDone && !Prefs.Offline) Dispatcher.BeginInvoke(new Action(Welcome), System.Windows.Threading.DispatcherPriority.Background); };
            Say("READY  //  F5 RESCAN  ·  CTRL+F SEARCH  ·  RIGHT-CLICK A CARD FOR MORE");
        }

        private void OnTheme()
        {
            var b = Cz.T.Backdrop;
            FrameworkElement back = null;
            if (Visuals.ThemeBackdrop) try { back = b?.Invoke(); } catch (Exception ex) { Log.Write("theme backdrop: " + ex.Message); }
            _backHost.Content = back ?? (_backHost.Content as Backdrop ?? new Backdrop());
            Cz.Glow(_title, Cz.T.Accent, 16, 0.9);
            FooterRight();
        }

        /// <summary>Glow just went off: drop the glow layers already on screen (the next look pass won't add them back).</summary>
        private static void NoGlow(DependencyObject d)
        {
            if (d is UIElement u && u.Effect is System.Windows.Media.Effects.DropShadowEffect) u.Effect = null;
            int n = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetChildrenCount(d) : 0;
            for (int i = 0; i < n; i++) NoGlow(VisualTreeHelper.GetChild(d, i));
        }

        // ================================================================== header

        private UIElement Header(out TextBlock title)
        {
            var g = new Grid { Background = Brushes.Transparent, Margin = new Thickness(22, 0, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.MouseLeftButtonDown += (s, e) =>
            {
                if (Cz.IsInteractive(e.OriginalSource as DependencyObject, g) || e.OriginalSource is TextBox) return;
                if (e.ClickCount == 2) ToggleMax(); else { try { DragMove(); } catch { } }
            };
            // the reactor + title
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(new Reactor { Width = 56, Height = 56 });
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 2, 0, 0) };
            title = Cz.Head("", 30, "CzTextBrush");
            title.Typography.Capitals = FontCapitals.Normal;
            names.Children.Add(title);
            Decode.Play(title, "CATALYZER", 0.15, 0.7);
            var sub = Cz.Mono("//  PROJECT COMMAND CENTER", 10.5, "CzAccentBrush"); sub.Margin = new Thickness(2, -2, 0, 0);
            names.Children.Add(sub);
            brand.Children.Add(names);
            g.Children.Add(brand);
            // live stats
            _stats.Margin = new Thickness(36, 0, 0, 0); _stats.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(_stats, 1); g.Children.Add(_stats);
            // search + tools
            var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            tools.Children.Add(_workChip);   // "2 HELPERS" while plugins keep something running
            tools.Children.Add(_updateChip); // "UPDATE 1.1.0 → 1.2.0" when the Catalyzer repo is ahead of this copy
            var sg = new Grid { Width = 280, Height = 36, Margin = new Thickness(0, 0, 10, 0) };
            sg.Children.Add(new Glass { Cut = 7, Brackets = false });
            var sp = new Grid { Margin = new Thickness(12, 0, 10, 0) };
            sp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); sp.ColumnDefinitions.Add(new ColumnDefinition());
            sp.Children.Add(Cz.Icon("\uE721", 12, "CzAccentBrush"));
            _search.Background = Brushes.Transparent; _search.BorderThickness = new Thickness(0); _search.FontSize = 13.5; _search.VerticalContentAlignment = VerticalAlignment.Center; _search.Margin = new Thickness(8, 0, 0, 0);
            _search.SetResourceReference(ForegroundProperty, "CzTextBrush"); _search.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, "CzSelectBrush"); _search.SetResourceReference(FontFamilyProperty, "CzFontUi");
            _search.FocusVisualStyle = null; _search.ToolTip = "FILTER  //  NAME, DESCRIPTION, BRANCH  (CTRL+F)";
            Grid.SetColumn(_search, 1); sp.Children.Add(_search);
            sg.Children.Add(sp);
            tools.Children.Add(sg);
            tools.Children.Add(new CzIconButton("\uE895", () => FetchAll(quiet: false), "FETCH ALL  //  CHECK GITHUB FOR EVERY PROJECT"));
            tools.Children.Add(new CzIconButton("\uE72C", () => { _board.Rescan(); Say("RESCANNING"); }, "RESCAN  //  F5"));
            CzIconButton plus = null;
            plus = new CzIconButton("\uE710", () => new CzMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom }.Show(plus, "NEW", new List<CzMenu.Item>
            {
                new CzMenu.Item { Text = "New project", Glyph = "\uE710", Hint = "folder + repo + GitHub + kickoff", Click = NewProject },
                new CzMenu.Item { Text = "Clone from GitHub", Glyph = "\uE896", Hint = "the GitHub panel", Click = OpenGitHub },
                new CzMenu.Item { Text = "Add an existing folder", Glyph = "\uE8DA", Click = AddFolder },
            }), "NEW PROJECT  ·  CLONE  ·  ADD A FOLDER");
            tools.Children.Add(plus);
            tools.Children.Add(new CzIconButton("\uE753", OpenGitHub, "GITHUB  //  ALL YOUR REPOS"));
            tools.Children.Add(new CzIconButton("\uE713", () => Drawer(!_drawerOpen), "SETTINGS  //  THEMES · PLUGINS · FOLDERS"));
            Grid.SetColumn(tools, 2); g.Children.Add(tools);
            var win = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            win.Children.Add(new WinButton(WinButton.Kind.Min, () => WindowState = WindowState.Minimized));
            _maxBtn = new WinButton(WinButton.Kind.Max, ToggleMax) { Window = this };
            win.Children.Add(_maxBtn);
            win.Children.Add(new WinButton(WinButton.Kind.Close, () => Dispatcher.BeginInvoke(new Action(Close))));
            Grid.SetColumn(win, 3); g.Children.Add(win);
            return g;
        }

        private void ToggleMax() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void Stats()
        {
            var items = _board.Items;
            int changed = items.Count(p => p.Git.Changed > 0), push = items.Count(p => p.Git.Ahead > 0), pull = items.Count(p => p.Git.Behind > 0);
            int week = items.Sum(p => p.Git.Daily.Skip(Math.Max(0, p.Git.Daily.Length - 7)).Sum());
            _stats.Children.Clear();
            void S(string num, string label, string brush)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 30, 0) };
                var n = Cz.Text(num, 24, brush, "CzFontMono"); sp.Children.Add(n);
                sp.Children.Add(Cz.Mono(label, 9.5, "CzTextFaintBrush"));
                _stats.Children.Add(sp);
            }
            S(items.Count.ToString("00"), "PROJECTS", "CzTextBrush");
            S(changed.ToString("00"), "WITH CHANGES", changed > 0 ? "CzWarnBrush" : "CzTextDimBrush");
            S(pull.ToString("00"), "TO PULL", pull > 0 ? "CzAccentBrush" : "CzTextDimBrush");
            S(push.ToString("00"), "TO PUSH", push > 0 ? "CzSelectBrush" : "CzTextDimBrush");
            S(week.ToString("000"), "COMMITS  //  7 DAYS", "CzAccentBrush");
        }

        // ================================================================== cards

        private void BuildCards()
        {
            _cards.Children.Clear(); _cardOf.Clear();
            int i = 0;
            foreach (var p in _board.Items)
            {
                var c = new ProjectCard(p, i++);
                c.Menu += CardMenu;
                _cardOf[p] = c;
                _cards.Children.Add(c);
            }
            if (_board.Items.Count == 0)
            {
                var empty = new StackPanel { Margin = new Thickness(40), HorizontalAlignment = HorizontalAlignment.Left };
                empty.Children.Add(Cz.Head("No projects found", 18, "CzTextDimBrush"));
                empty.Children.Add(Cz.Text("Catalyzer shows every git repo in " + string.Join(", ", Prefs.Value.Roots) + ". Add a folder with + or in settings.", 13, "CzTextFaintBrush"));
                _cards.Children.Add(empty);
            }
            Fit(); Filter(); Stats(); FooterRight();
        }

        /// <summary>Cards share the row: as many columns as fit at the minimum width, stretched to fill it.</summary>
        private void Fit()
        {
            double avail = (_scroll.ViewportWidth > 0 ? _scroll.ViewportWidth : _scroll.ActualWidth - 10) - _cards.Margin.Left - _cards.Margin.Right - 2;
            if (avail < 100) return;
            int cols = Math.Max(1, (int)(avail / (ProjectCard.W + 18)));
            double w = Math.Floor(avail / cols) - 18;
            foreach (var c in _cardOf.Values) c.Width = w;
        }

        private void Filter()
        {
            var q = _search.Text.Trim();
            foreach (var (p, c) in _cardOf)
            {
                bool hit = q == "" || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || (p.Description ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) || (p.Git.Branch ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);
                c.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void CardMenu(ProjectCard c)
        {
            var p = c.P;
            var items = new List<CzMenu.Item>
            {
                new CzMenu.Item { Text = "Open folder", Glyph = "\uE8B7", Click = () => Launch.Shell("explorer.exe", "\"" + p.Folder + "\"") },
                new CzMenu.Item { Text = "Copy path", Glyph = "\uE8C8", Click = () => { try { Clipboard.SetText(p.Folder); Say("COPIED  " + p.Folder.ToUpperInvariant()); } catch { } } },
                new CzMenu.Item { Text = p.HasManifest ? "Edit catalyzer.json" : "Create catalyzer.json", Glyph = "\uE70F", Hint = p.HasManifest ? null : "name, colour, buttons", Click = () => EditManifest(p) },
                new CzMenu.Item { Text = "Card colours", Glyph = "\uE790", Hint = "primary + secondary", Click = () => CardColors(c) },
                new CzMenu.Item { Text = "Refresh git", Glyph = "\uE72C", Click = async () => { await Projects.LoadGit(p); c.Git(); Stats(); } },
                new CzMenu.Item { Separator = true },
                new CzMenu.Item { Text = "Move to the front", Glyph = "\uE74A", Click = () => { Prefs.Save(x => { x.Order.RemoveAll(o => string.Equals(o, p.Folder, StringComparison.OrdinalIgnoreCase)); x.Order.Insert(0, p.Folder); }); _board.Rescan(); } },
                new CzMenu.Item { Text = "Hide this project", Glyph = "\uED1A", Hint = "settings bring it back", Click = () => { Prefs.Save(x => x.Hidden.Add(p.Folder)); _board.Rescan(); } },
            };
            _menu.Show(c, p.Name, items);
        }

        private void EditManifest(Project p)
        {
            var f = System.IO.Path.Combine(p.Folder, Projects.ManifestName);
            if (!System.IO.File.Exists(f))
            {
                try
                {
                    var gh = p.GitHub != null ? "\n  \"github\": \"" + p.GitHub + "\"," : "";
                    System.IO.File.WriteAllText(f, "{\n  // Catalyzer reads this (docs: Catalyzer\\docs\\MANIFEST.md). Everything is optional.\n  \"name\": \"" + p.Name + "\",\n  \"description\": \"\"," + gh + "\n  \"accent\": null,\n  \"glyph\": null,\n  \"actions\": [\n    // { \"label\": \"Build\", \"glyph\": \"\\uE768\", \"run\": \"build.cmd\", \"args\": \"\" }\n  ]\n}\n");
                    Say("CREATED  " + f.ToUpperInvariant());
                }
                catch (Exception ex) { Say("COULDN'T CREATE IT: " + ex.Message); return; }
            }
            Launch.Shell(f, null);
        }

        private void AddFolder()
        {
            var d = new Microsoft.Win32.OpenFolderDialog { Title = "A project folder (or a folder of projects)" };
            if (d.ShowDialog(this) != true) return;
            var dir = d.FolderName;
            bool isRepo = System.IO.Directory.Exists(System.IO.Path.Combine(dir, ".git")) || System.IO.File.Exists(System.IO.Path.Combine(dir, Projects.ManifestName));
            Prefs.Save(p => { if (isRepo) { if (!p.Extra.Contains(dir)) p.Extra.Add(dir); } else if (!p.Roots.Contains(dir)) p.Roots.Add(dir); });
            Say(isRepo ? "ADDED PROJECT  " + dir.ToUpperInvariant() : "SCANNING FOLDER  " + dir.ToUpperInvariant());
            _board.Rescan();
        }

        // ================================================================== footer + rim

        private UIElement Footer()
        {
            var g = new Grid { Margin = new Thickness(24, 0, 18, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var line = new Rectangle { Height = 1, VerticalAlignment = VerticalAlignment.Top };
            line.SetResourceReference(Shape.FillProperty, "CzLineBrush");
            Grid.SetColumnSpan(line, 2); g.Children.Add(line);
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _footLed.Margin = new Thickness(0, 0, 10, 0); _footLed.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(_footLed); left.Children.Add(_footer);
            g.Children.Add(left);
            _footerRight.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_footerRight, 1); g.Children.Add(_footerRight);
            Grid.SetRow(g, 2);
            return g;
        }

        private void Say(string text)
        {
            _footLed.Color = Cz.T.Accent; _footLed.Pulse = true; _footLed.InvalidateVisual();
            Decode.Play(_footer, text.ToUpperInvariant(), 0, 0.35);
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            t.Tick += (s, e) => { t.Stop(); _footLed.Pulse = false; _footLed.InvalidateVisual(); };
            t.Start();
        }

        private void FooterRight()
        {
            int plugins = PluginHost.I?.Plugins.Count(p => p.Running) ?? 0;
            _footerRight.Text = string.Join("   //   ", Prefs.Value.Roots.Select(r => r.ToUpperInvariant())) + "   //   " + (Cz.T?.Name ?? "").ToUpperInvariant() + " THEME   //   " + plugins + " PLUGIN" + (plugins == 1 ? "" : "S") + "   //   V" + (PluginHost.I?.AppVersion ?? "0.1") + "  API " + Api.Version;
        }

        private UIElement Rim()
        {
            var r = new Path { IsHitTestVisible = false, StrokeThickness = 1 };
            SizeChanged += (s, e) => r.Data = Cz.Chamfer(Math.Max(10, e.NewSize.Width - 1), Math.Max(10, e.NewSize.Height - 1), 14);
            void Look() => r.Stroke = new LinearGradientBrush(Cz.T.Accent.A(0.55), Cz.T.Select.A(0.35), 45);
            ThemeService.I.Changed += Look; Look();
            return Cz.Under(r);
        }
    }

    /// <summary>The spinning core in the header: rings turning at different speeds around a pulsing hex.</summary>
    public sealed class Reactor : FrameworkElement
    {
        private double _t;
        public Reactor() { IsHitTestVisible = false; Cz.Tick(this, dt => { _t += dt; InvalidateVisual(); return true; }, 30, Pace.Ambient); ThemeService.I.Changed += InvalidateVisual; }
        protected override void OnRender(DrawingContext dc)
        {
            var T = Cz.T; double w = ActualWidth, h = ActualHeight, cx = w / 2, cy = h / 2, R = Math.Min(w, h) / 2 - 2;
            if (!T.Light) { var halo = new RadialGradientBrush(T.Accent.A(0.35), T.Accent.A(0)); halo.Freeze(); dc.DrawEllipse(halo, null, new Point(cx, cy), R * 1.25, R * 1.25); }
            void Arcs(double r, double rot, int n, double span, Color c, double th)
            {
                var pen = new Pen(c.Brush(), th) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; pen.Freeze();
                for (int i = 0; i < n; i++)
                {
                    double a0 = rot + i * 2 * Math.PI / n, a1 = a0 + span;
                    var g = new StreamGeometry();
                    using (var ctx = g.Open())
                    {
                        ctx.BeginFigure(new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0)), false, false);
                        ctx.ArcTo(new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1)), new Size(r, r), 0, span > Math.PI, SweepDirection.Clockwise, true, false);
                    }
                    g.Freeze(); dc.DrawGeometry(null, pen, g);
                }
            }
            Arcs(R, _t * 0.9, 3, 1.3, T.Accent.A(0.95), 2);
            Arcs(R * 0.78, -_t * 1.4, 4, 0.8, T.Select.A(0.85), 1.6);
            Arcs(R * 0.58, _t * 2.1, 2, 2.1, T.Accent.A(0.6), 1.2);
            double pulse = 0.75 + 0.25 * Math.Sin(_t * 2.6);
            var hex = new StreamGeometry();
            using (var c = hex.Open())
                for (int k = 0; k < 6; k++) { double a = Math.PI / 3 * k + _t * 0.3; var pt = new Point(cx + R * 0.32 * Math.Cos(a), cy + R * 0.32 * Math.Sin(a)); if (k == 0) c.BeginFigure(pt, true, true); else c.LineTo(pt, true, false); }
            hex.Freeze();
            var core = new RadialGradientBrush(Colors.White.A(0.9 * pulse), T.Accent.Mix(T.Select, 0.3).A(0.85 * pulse)); core.Freeze();
            dc.DrawGeometry(core, new Pen(T.Accent.Brush(), 1), hex);
        }
    }

    public static class Styles
    {
        /// <summary>A thin, theme-coloured scroll bar (no stock chrome).</summary>
        public static ResourceDictionary Scroll()
        {
            const string x = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='8'/><Setter Property='MinWidth' Value='8'/><Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>
      <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.Thumb>
        <Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='{DynamicResource CzAccentBrush}' Opacity='0.35' Margin='2,0,2,0'/></ControlTemplate></Thumb.Template></Thumb>
      </Track.Thumb></Track></Grid>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
</ResourceDictionary>";
            return (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(x);
        }
    }
}
