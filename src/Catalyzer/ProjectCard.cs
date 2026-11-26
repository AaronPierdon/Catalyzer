using System;
using System.Collections.Generic;

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// One project, floating on glass: its badge + name (decoding in), a status light, what it is, where its git stands,
    /// three weeks of activity, and its buttons (built-in, the project's own from catalyzer.json, and plugins').
    /// </summary>
    public sealed class ProjectCard : Grid
    {
        public const double W = 432, H = 252;
        public Project P { get; }
        private readonly Glass _glass = new Glass();
        private readonly TextBlock _name, _state, _branch, _commit, _when, _activity;
        private readonly Led _led;
        private readonly ActivityBars _bars = new ActivityBars { Height = 30 };
        private readonly OneRow _buttons = new OneRow();
        private readonly StackPanel _badges = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly Dictionary<string, Chip> _chipOf = new Dictionary<string, Chip>();
        private readonly TranslateTransform _lift = new TranslateTransform();
        private readonly Path _badge = new Path(), _badgeRing = new Path();
        private readonly TextBlock _badgeGlyph;
        private double _lit, _litTo;
        private bool _hadGitHub;
        public event Action<ProjectCard> Menu;

        private Color Tint => P.Accent ?? Cz.T.Accent;

        public ProjectCard(Project p, int index)
        {
            P = p; Width = W; Height = H; Margin = new Thickness(0, 0, 18, 18);
            RenderTransform = _lift; Background = Brushes.Transparent;
            _glass.Tint = p.Accent;
            Children.Add(_glass);
            var body = new Grid { Margin = new Thickness(18, 14, 18, 12) };
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });   // badge + name
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });   // description
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });   // rule + plugin badges
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });   // git
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });       // activity
            body.RowDefinitions.Add(new RowDefinition());                                  // buttons
            Children.Add(body);

            // ---- badge (a hex with the project's glyph or initials) + name + state
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var badge = new Grid { Width = 42, Height = 42, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            _badge.Data = Hex(21, 21, 19); _badgeRing.Data = Hex(21, 21, 20.5);
            badge.Children.Add(_badge); badge.Children.Add(_badgeRing);
            _badgeGlyph = string.IsNullOrEmpty(p.Glyph)
                ? Cz.Text(Initials(p.Name), 15, "CzTextBrush", "CzFontHead", FontWeights.Bold)
                : Cz.Icon(p.Glyph, 17, "CzTextBrush");
            _badgeGlyph.HorizontalAlignment = HorizontalAlignment.Center; _badgeGlyph.VerticalAlignment = VerticalAlignment.Center; _badgeGlyph.TextAlignment = TextAlignment.Center;
            badge.Children.Add(_badgeGlyph);
            head.Children.Add(badge);
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _name = Cz.Head("", 21, "CzTextBrush");
            nameStack.Children.Add(_name);
            var path = Cz.Mono(ShortPath(p.Folder).ToUpperInvariant() + (p.HasManifest ? "" : "  //  NO CATALYZER.JSON"), 9.5, "CzTextFaintBrush");
            path.ToolTip = p.Folder;
            nameStack.Children.Add(path);
            Grid.SetColumn(nameStack, 1); head.Children.Add(nameStack);
            var state = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 6, 0, 0) };
            _led = new Led(Cz.T.TextFaint, 9) { VerticalAlignment = VerticalAlignment.Center };
            _state = Cz.Mono("READING", 10, "CzTextDimBrush"); _state.Margin = new Thickness(7, 0, 0, 0); _state.VerticalAlignment = VerticalAlignment.Center;
            state.Children.Add(_led); state.Children.Add(_state);
            Grid.SetColumn(state, 2); head.Children.Add(state);
            body.Children.Add(head);

            // ---- what it is
            var desc = Cz.Text(string.IsNullOrWhiteSpace(p.Description) ? "No description yet  -  add one in catalyzer.json" : p.Description, 12.5, string.IsNullOrWhiteSpace(p.Description) ? "CzTextFaintBrush" : "CzTextDimBrush");
            desc.TextWrapping = TextWrapping.Wrap; desc.TextTrimming = TextTrimming.WordEllipsis; desc.MaxHeight = 36; desc.VerticalAlignment = VerticalAlignment.Top; desc.Margin = new Thickness(2, 2, 0, 0);
            Grid.SetRow(desc, 1); body.Children.Add(desc);

            // ---- a rule with a diamond
            var rule = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var line = new Rectangle { Height = 1 };
            line.Fill = new LinearGradientBrush(Tint.A(0.5), Tint.A(0), 0);
            rule.Children.Add(line);
            var dia = new Rectangle { Width = 5, Height = 5, HorizontalAlignment = HorizontalAlignment.Left, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(45), Fill = Tint.Brush() };
            rule.Children.Add(dia);
            _badges.HorizontalAlignment = HorizontalAlignment.Right; _badges.VerticalAlignment = VerticalAlignment.Center;
            rule.Children.Add(_badges);
            Grid.SetRow(rule, 2); body.Children.Add(rule);

            // ---- git
            var git = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
            var row1 = new StackPanel { Orientation = Orientation.Horizontal };
            row1.Children.Add(Cz.Icon("", 10, "CzAccentBrush").Also(i => i.Margin = new Thickness(0, 1, 6, 0)));
            _branch = Cz.Mono("", 11, "CzTextBrush"); row1.Children.Add(_branch);
            _when = Cz.Mono("", 10.5, "CzTextFaintBrush"); _when.Margin = new Thickness(10, 0, 0, 0); row1.Children.Add(_when);
            git.Children.Add(row1);
            _commit = Cz.Text("", 12.5, "CzTextBrush"); _commit.Margin = new Thickness(0, 3, 0, 0);
            git.Children.Add(_commit);
            Grid.SetRow(git, 3); body.Children.Add(git);

            // ---- three weeks of activity
            var act = new Grid { Margin = new Thickness(2, 4, 0, 0) };
            act.ColumnDefinitions.Add(new ColumnDefinition()); act.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _bars.Tint = p.Accent;
            act.Children.Add(_bars);
            _activity = Cz.Mono("", 9.5, "CzTextFaintBrush"); _activity.VerticalAlignment = VerticalAlignment.Bottom; _activity.Margin = new Thickness(12, 0, 0, 0); _activity.TextAlignment = TextAlignment.Right; _activity.Width = 92;
            Grid.SetColumn(_activity, 1); act.Children.Add(_activity);
            Grid.SetRow(act, 4); body.Children.Add(act);

            // ---- buttons
            _buttons.VerticalAlignment = VerticalAlignment.Bottom; _buttons.Margin = new Thickness(0, 8, 0, 0);
            Grid.SetRow(_buttons, 5); body.Children.Add(_buttons);
            _hadGitHub = p.GitHub != null;
            BuildButtons();
            Badges();

            // ---- life
            ThemeService.I.Changed += Look;
            Look();
            Git();
            MouseEnter += (s, e) => Hot(true);
            MouseLeave += (s, e) => Hot(false);
            MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 2 && !Cz.IsInteractive(e.OriginalSource as DependencyObject, this)) { e.Handled = true; Launch.Shell("explorer.exe", "\"" + p.Folder + "\""); } };
            MouseRightButtonUp += (s, e) => { e.Handled = true; Menu?.Invoke(this); };
            // hover light: a clock that runs only while the light is moving (cards never tick at rest)
            _litWake = Cz.Tick(this, dt =>
            {
                if (Cz.Still || Math.Abs(_lit - _litTo) < 0.01) _lit = _litTo;
                else _lit += (_litTo - _lit) * Math.Min(1, dt * 12);
                _glass.Lit = _lit; _lift.Y = -3 * _lit;
                return _lit != _litTo;
            }, 60);

            // arrive: rise + fade, staggered; the name decodes in
            if (!Cz.Still)
            {
                Opacity = 0; _lift.Y = 18;
                var d = TimeSpan.FromMilliseconds(60 * Math.Min(index, 12));
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(420)) { BeginTime = d });
                _lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(520)) { BeginTime = d, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
                Decode.Play(_name, Cz.T.HeadCaps ? p.Name.ToUpperInvariant() : p.Name, d.TotalSeconds + 0.1, 0.55);
            }
            else _name.Text = Cz.T.HeadCaps ? p.Name.ToUpperInvariant() : p.Name;
        }

        public void BuildButtons()
        {
            _buttons.Children.Clear();
            var acts = new List<ProjectAction>();
            var all = Launch.BuiltIn().Concat(Launch.FromManifest(P)).Concat(PluginHost.I?.Actions ?? new List<ProjectAction>())
                                      .Where(a => { try { return a.AppliesTo == null || a.AppliesTo(P); } catch { return false; } })
                                      .OrderBy(a => a.Order);
            foreach (var a in all)
            {
                var act = a;
                var b = new CzButton(a.Label, () => { try { act.Run(P); Log.Write(act.Label + "  //  " + P.Name, show: true); } catch (Exception ex) { Log.Write(act.Label + ": " + ex.Message, show: true); } }, a.Glyph, height: 26, tip: a.Tip) { Margin = new Thickness(0, 0, 6, 6) };
                acts.Add(act);
                _buttons.Children.Add(b);
            }
            // what doesn't fit on the row folds into a menu
            CzButton more = null;
            more = new CzButton(null, () =>
            {
                var items = _buttons.Hidden.Select(i => acts[i]).Select(a => new CzMenu.Item { Text = a.Label, Glyph = a.Glyph, Hint = a.Tip?.ToLowerInvariant(), Click = () => { try { a.Run(P); } catch (Exception ex) { Log.Write(a.Label + ": " + ex.Message, show: true); } } }).ToList();
                if (items.Count > 0) new CzMenu().Show(more, P.Name, items);
            }, "\uE712", height: 26, tip: "MORE") { Margin = new Thickness(0, 0, 0, 6) };
            _buttons.More = more;
            _buttons.Children.Add(more);
        }

        /// <summary>Plugins' chips (API 2), read again whenever a plugin says its state changed.</summary>
        public void Badges()
        {
            var want = new List<(ProjectBadge b, BadgeState st)>();
            foreach (var b in (PluginHost.I?.Badges ?? new List<ProjectBadge>()).OrderBy(x => x.Order))
            {
                try { if (b.AppliesTo != null && !b.AppliesTo(P)) continue; var st = b.State(P); if (st != null) want.Add((b, st)); }
                catch (Exception ex) { Log.Write("badge " + b.Id + ": " + ex.Message); }
            }
            foreach (var gone in _chipOf.Keys.Where(k => want.All(w => w.b.Id != k)).ToList()) { _badges.Children.Remove(_chipOf[gone]); _chipOf.Remove(gone); }
            foreach (var (b, st) in want)
            {
                if (!_chipOf.TryGetValue(b.Id, out var chip))
                {
                    var badge = b;
                    chip = new Chip(22) { Margin = new Thickness(8, 0, 0, 0) };
                    void OpenMenu(Chip c)
                    {
                        List<CzMenu.Item> items;
                        try { items = CzMenu.From(badge.Menu?.Invoke(P), badge.Id); } catch (Exception ex) { Log.Write("badge " + badge.Id + ": " + ex.Message, show: true); return; }
                        if (items.Count > 0) new CzMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom }.Show(c, P.Name, items);
                    }
                    if (badge.Click != null)
                    {
                        chip.Click = c => { try { badge.Click(P); } catch (Exception ex) { Log.Write("badge " + badge.Id + ": " + ex.Message, show: true); } };
                        if (badge.Menu != null) chip.RightClick = OpenMenu;
                    }
                    else if (badge.Menu != null) { chip.Click = OpenMenu; chip.RightClick = OpenMenu; }
                    _chipOf[b.Id] = chip; _badges.Children.Add(chip);
                }
                chip.Set(st.Text ?? "", st.Tone, st.Pulse, st.Tip);
            }
        }

        public void Git()
        {
            var g = P.Git; var T = Cz.T;
            if ((P.GitHub != null) != _hadGitHub) { _hadGitHub = P.GitHub != null; BuildButtons(); }   // the remote arrived with git: GITHUB appears
            if (!g.Loaded) { _branch.Text = "..."; _commit.Text = ""; _state.Text = "READING"; _led.Color = T.TextFaint; _led.Pulse = true; _led.InvalidateVisual(); return; }
            if (!g.IsRepo)
            {
                _branch.Text = "NOT A GIT REPO"; _commit.Text = ""; _when.Text = ""; _state.Text = "LOCAL"; _led.Color = T.TextDim; _led.Pulse = false; _bars.Set(Array.Empty<int>()); _activity.Text = "";
                _led.InvalidateVisual(); return;
            }
            var parts = new List<string> { (g.Branch ?? "?").ToUpperInvariant() };
            if (g.Ahead > 0) parts.Add("↑" + g.Ahead);
            if (g.Behind > 0) parts.Add("↓" + g.Behind);
            if (g.Remote == null) parts.Add("NO REMOTE");
            _branch.Text = string.Join("   ", parts);
            _when.Text = g.LastHash != null ? g.LastHash + "  ·  " + Cz.Ago(g.LastWhen) : "NO COMMITS YET";
            _commit.Text = g.LastSubject ?? "";
            _commit.ToolTip = g.LastSubject;
            // the light: changes waiting (warn) > commits to push (select) > clean (ok)
            if (g.Changed > 0) { _state.Text = g.Changed + " CHANGED"; _led.Color = T.Warn; _led.Pulse = true; _state.SetResourceReference(TextBlock.ForegroundProperty, "CzWarnBrush"); }
            else if (g.Ahead > 0) { _state.Text = g.Ahead + " TO PUSH"; _led.Color = T.Select; _led.Pulse = true; _state.SetResourceReference(TextBlock.ForegroundProperty, "CzSelectBrush"); }
            else if (g.Behind > 0) { _state.Text = g.Behind + " TO PULL"; _led.Color = T.Select; _led.Pulse = false; _state.SetResourceReference(TextBlock.ForegroundProperty, "CzSelectBrush"); }
            else { _state.Text = "CLEAN"; _led.Color = T.Ok; _led.Pulse = false; _state.SetResourceReference(TextBlock.ForegroundProperty, "CzOkBrush"); }
            _led.InvalidateVisual();
            _bars.Set(g.Daily);
            int sum = g.Daily.Sum();
            _activity.Text = "21 DAYS\n" + sum + (sum == 1 ? " COMMIT" : " COMMITS");
        }

        private Action _litWake;
        private void Hot(bool on) { _litTo = on ? 1 : 0; _litWake?.Invoke(); if (on) Cz.Glow(_glass, Tint, 26, 0.45); else _glass.Effect = null; }

        private void Look()
        {
            var T = Cz.T; var c = Tint;
            _badge.Fill = new LinearGradientBrush(c.A(0.38), c.Mix(T.Select, 0.4).A(0.16), 45);
            _badgeRing.Stroke = c.A(0.9).Brush(); _badgeRing.StrokeThickness = 1.3;
            Cz.Glow(_badgeRing, c, 12, 0.8);
            if (P.Git.Loaded) Git();
        }

        private static Geometry Hex(double cx, double cy, double r)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                for (int k = 0; k < 6; k++) { double a = Math.PI / 3 * k - Math.PI / 2; var pt = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)); if (k == 0) c.BeginFigure(pt, true, true); else c.LineTo(pt, true, false); }
            }
            g.Freeze(); return g;
        }

        private static string Initials(string name)
        {
            var w = (name ?? "?").Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (w.Length >= 2) return (w[0].Substring(0, 1) + w[1].Substring(0, 1)).ToUpperInvariant();
            var s = w.Length > 0 ? w[0] : "?";
            var caps = new string(s.Where(char.IsUpper).Take(2).ToArray());
            return (caps.Length == 2 ? caps : s.Substring(0, Math.Min(2, s.Length))).ToUpperInvariant();
        }

        private static string ShortPath(string p)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return p.StartsWith(home, StringComparison.OrdinalIgnoreCase) ? "~" + p.Substring(home.Length) : p;
        }
    }

    /// <summary>Buttons on one line; whatever doesn't fit hides, and <see cref="More"/> (the last child) appears for it.</summary>
    public sealed class OneRow : Panel
    {
        public UIElement More;
        public OneRow() { ClipToBounds = true; }
        public readonly List<int> Hidden = new List<int>();
        private readonly List<bool> _shown = new List<bool>();

        protected override Size MeasureOverride(Size avail)
        {
            double h = 0;
            foreach (UIElement c in InternalChildren) { c.Measure(new Size(double.PositiveInfinity, avail.Height)); h = Math.Max(h, c.DesiredSize.Height); }
            Hidden.Clear(); _shown.Clear();
            var items = InternalChildren.Cast<UIElement>().Where(c => c != More).ToList();
            double total = items.Sum(c => c.DesiredSize.Width), room = double.IsInfinity(avail.Width) ? total : avail.Width;
            bool overflow = total > room + 0.5;
            double limit = overflow && More != null ? room - More.DesiredSize.Width : room, x = 0;
            for (int i = 0; i < items.Count; i++)
            {
                bool fit = !overflow || x + items[i].DesiredSize.Width <= limit + 0.5;
                if (fit && (Hidden.Count == 0)) { x += items[i].DesiredSize.Width; _shown.Add(true); }
                else { Hidden.Add(i); _shown.Add(false); }
            }
            return new Size(Math.Min(room, x + (overflow && More != null ? More.DesiredSize.Width : 0)), h);
        }

        protected override Size ArrangeOverride(Size size)
        {
            double x = 0; int i = 0;
            foreach (UIElement c in InternalChildren)
            {
                bool show = c == More ? Hidden.Count > 0 : i < _shown.Count && _shown[i];
                if (c != More) i++;
                if (show) { c.Arrange(new Rect(x, 0, c.DesiredSize.Width, c.DesiredSize.Height)); x += c.DesiredSize.Width; }
                else c.Arrange(new Rect(-10000, 0, c.DesiredSize.Width, c.DesiredSize.Height));   // off the clipped panel
            }
            return size;
        }
    }
}
