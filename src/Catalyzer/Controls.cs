using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>The theme in front + small helpers every control uses.</summary>
    public static class Cz
    {
        public static CatalyzerTheme T => ThemeService.I.Current;
        public static bool Still => Prefs.Value.ReduceMotion || SystemParameters.ClientAreaAnimation == false || Visuals.Still;

        public static TextBlock Text(string s, double size = 13, string brush = "CzTextBrush", string font = "CzFontUi", FontWeight? w = null)
        {
            var t = new TextBlock { Text = s, FontSize = size, FontWeight = w ?? FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis };
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            t.SetResourceReference(TextBlock.FontFamilyProperty, font);
            TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            return t;
        }
        /// <summary>A shape drawn to its host's size that must not take part in layout (a stroked Path sized from ActualWidth would grow 1 px per pass).</summary>
        public static Canvas Under(UIElement shape) { var c = new Canvas { IsHitTestVisible = false, ClipToBounds = false }; c.Children.Add(shape); return c; }

        public static TextBlock Mono(string s, double size = 11, string brush = "CzTextDimBrush") => Text(s, size, brush, "CzFontMono");
        public static TextBlock Head(string s, double size = 14, string brush = "CzAccentBrush")
        {
            var t = Text(T.HeadCaps ? s.ToUpperInvariant() : s, size, brush, "CzFontHead", FontWeights.SemiBold);
            t.Typography.Capitals = FontCapitals.Normal;
            return t;
        }
        public static TextBlock Icon(string glyph, double size = 14, string brush = "CzTextBrush")
        {
            var t = new TextBlock { Text = glyph, FontSize = size, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return t;
        }

        /// <summary>A panel outline with the top-left and bottom-right corners cut.</summary>
        public static Geometry Chamfer(double w, double h, double cut)
        {
            cut = Math.Min(cut, Math.Min(w, h) / 3);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(cut, 0), true, true);
                c.LineTo(new Point(w, 0), true, false); c.LineTo(new Point(w, h - cut), true, false);
                c.LineTo(new Point(w - cut, h), true, false); c.LineTo(new Point(0, h), true, false); c.LineTo(new Point(0, cut), true, false);
            }
            g.Freeze(); return g;
        }

        /// <summary>Glow on its own layer (no glow on light themes).</summary>
        public static void Glow(UIElement e, Color c, double radius = 12, double opacity = 0.7)
        {
            if (T.Light || T.Glow <= 0 || !Visuals.Glow) { e.Effect = null; return; }
            e.Effect = new DropShadowEffect { Color = c, BlurRadius = radius * T.Glow, ShadowDepth = 0, Opacity = opacity, RenderingBias = RenderingBias.Performance };
        }

        /// <summary>A tone's brush resource and colour (roles, never literals).</summary>
        public static string ToneBrush(Tone t) => t switch
        {
            Tone.Faint => "CzTextFaintBrush", Tone.Dim => "CzTextDimBrush", Tone.Accent => "CzAccentBrush", Tone.Select => "CzSelectBrush",
            Tone.Ok => "CzOkBrush", Tone.Warn => "CzWarnBrush", Tone.Danger => "CzDangerBrush", _ => "CzTextDimBrush",
        };
        public static Color ToneColor(Tone t) => t switch
        {
            Tone.Faint => T.TextFaint, Tone.Dim => T.TextDim, Tone.Accent => T.Accent, Tone.Select => T.Select,
            Tone.Ok => T.Ok, Tone.Warn => T.Warn, Tone.Danger => T.Danger, _ => T.TextDim,
        };

        public static bool IsInteractive(DependencyObject src, DependencyObject stop)
        {
            for (var d = src; d != null && d != stop; d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                // anything clickable: pressing it must never start a window drag (the drag swallows the click)
                if (d is ButtonBase || d is TextBoxBase || d is CzButton || d is CzToggle || d is Chip || d is CzIconButton || d is WinButton) return true;
            return false;
        }

        public static string Ago(DateTime? utc)
        {
            if (utc == null) return "";
            var s = DateTime.UtcNow - utc.Value;
            if (s.TotalMinutes < 1) return "NOW";
            if (s.TotalHours < 1) return (int)s.TotalMinutes + "M AGO";
            if (s.TotalDays < 1) return (int)s.TotalHours + "H AGO";
            if (s.TotalDays < 45) return (int)s.TotalDays + "D AGO";
            return utc.Value.ToLocalTime().ToString("MMM d yyyy").ToUpperInvariant();
        }

        /// <summary>
        /// A frame clock that only runs while something wants it: while the owner is loaded and visible, until
        /// <paramref name="step"/> returns false, and at the rate the visuals level gives its pace (0 = stopped; the
        /// owner is redrawn once so it holds a still frame). Returns wake(): call it to run a finished clock again.
        /// </summary>
        public static Action Tick(FrameworkElement owner, Func<double, bool> step, int fps = 30, Pace pace = Pace.Feedback)
        {
            var t = new DispatcherTimer(DispatcherPriority.Render);
            var last = DateTime.UtcNow; bool loaded = owner.IsLoaded, done = false;
            void Sync()
            {
                int f = Visuals.Fps(pace, fps);
                if (loaded && !done && f > 0 && owner.IsVisible)
                {
                    var iv = TimeSpan.FromMilliseconds(1000.0 / f);
                    if (t.Interval != iv) t.Interval = iv;
                    if (!t.IsEnabled) { last = DateTime.UtcNow; t.Start(); }
                }
                else t.Stop();
            }
            t.Tick += (s, e) =>
            {
                var now = DateTime.UtcNow; double dt = Math.Min(0.1, (now - last).TotalSeconds); last = now;
                if (!step(dt)) { done = true; t.Stop(); }
            };
            Action changed = () => { Sync(); owner.InvalidateVisual(); };
            owner.Loaded += (s, e) => { if (loaded) return; loaded = true; Visuals.Changed += changed; Sync(); };
            owner.Unloaded += (s, e) => { loaded = false; Visuals.Changed -= changed; t.Stop(); };
            owner.IsVisibleChanged += (s, e) => Sync();
            if (loaded) { Visuals.Changed += changed; Sync(); }
            return () => { done = false; Sync(); };
        }
    }

    /// <summary>
    /// The window floor: a hex lattice that lights up where two slow energy blooms drift, and sparks rising through it.
    /// A theme can bring its own backdrop instead (CatalyzerTheme.Backdrop).
    /// </summary>
    public sealed class Backdrop : FrameworkElement
    {
        private DrawingGroup _lattice; private Size _for; private string _theme;
        private double _t;
        private readonly Random _r = new Random(7);
        private readonly List<(double x, double y, double v, double s, double ph)> _sparks = new List<(double, double, double, double, double)>();

        public Backdrop()
        {
            IsHitTestVisible = false;
            ThemeService.I.Changed += () => { _lattice = null; InvalidateVisual(); };
            Cz.Tick(this, dt => { _t += dt; InvalidateVisual(); return true; }, 30, Pace.Ambient);
        }

        private void Build(Size s)
        {
            var T = Cz.T;
            _lattice = new DrawingGroup();
            using (var dc = _lattice.Open())
            {
                double r = 34, hw = Math.Sqrt(3) * r;
                var pen = new Pen(T.Accent.A(1).Brush(), 1); pen.Freeze();
                var geo = new StreamGeometry();
                using (var c = geo.Open())
                    for (double y = -r, row = 0; y < s.Height + r; y += r * 1.5, row++)
                        for (double x = (row % 2 == 0 ? 0 : hw / 2) - hw; x < s.Width + hw; x += hw)
                        {
                            c.BeginFigure(new Point(x, y - r), false, true);
                            for (int k = 1; k < 6; k++) { double a = Math.PI / 3 * k - Math.PI / 2; c.LineTo(new Point(x + r * Math.Cos(a), y + r * Math.Sin(a)), true, false); }
                        }
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }
            _lattice.Freeze();
            _for = s; _theme = T.Id;
            _sparks.Clear();
            for (int i = 0; i < 46; i++) _sparks.Add((_r.NextDouble(), _r.NextDouble(), 0.015 + _r.NextDouble() * 0.04, 0.6 + _r.NextDouble() * 1.4, _r.NextDouble() * 6.28));
        }

        protected override void OnRender(DrawingContext dc)
        {
            var s = RenderSize; if (s.Width < 10 || s.Height < 10) return;
            var T = Cz.T;
            if (_lattice == null || _for != s || _theme != T.Id) Build(s);
            var floor = new LinearGradientBrush(T.Bg1.Mix(T.Bg0, 0.35), T.Bg0, 90); floor.Freeze();
            dc.DrawRectangle(floor, null, new Rect(s));
            bool hex = Visuals.HexFloor;
            // the lattice, faint everywhere ...
            if (hex) { dc.PushOpacity(T.Light ? 0.05 : 0.045); dc.DrawDrawing(_lattice); dc.Pop(); }
            // ... and lit where the blooms are
            double w = s.Width, h = s.Height;
            Point A = new Point(w * (0.28 + 0.16 * Math.Sin(_t * 0.07)), h * (0.35 + 0.18 * Math.Cos(_t * 0.05)));
            Point B = new Point(w * (0.74 + 0.14 * Math.Cos(_t * 0.06 + 1)), h * (0.62 + 0.2 * Math.Sin(_t * 0.045 + 2)));
            foreach (var (p, c, rad) in new[] { (A, T.Accent, Math.Max(w, h) * 0.42), (B, T.Select, Math.Max(w, h) * 0.36) })
            {
                var bloom = new RadialGradientBrush { Center = p, GradientOrigin = p, RadiusX = rad, RadiusY = rad * 0.75, MappingMode = BrushMappingMode.Absolute };
                bloom.GradientStops.Add(new GradientStop(c.A(T.Light ? 0.10 : 0.13), 0)); bloom.GradientStops.Add(new GradientStop(c.A(0.04), 0.5)); bloom.GradientStops.Add(new GradientStop(c.A(0), 1));
                bloom.Freeze();
                dc.DrawRectangle(bloom, null, new Rect(s));
                var mask = new RadialGradientBrush { Center = p, GradientOrigin = p, RadiusX = rad * 0.7, RadiusY = rad * 0.55, MappingMode = BrushMappingMode.Absolute };
                mask.GradientStops.Add(new GradientStop(Colors.Black, 0)); mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1)); mask.Freeze();
                if (hex) { dc.PushOpacityMask(mask); dc.PushOpacity(T.Light ? 0.12 : 0.2); dc.DrawDrawing(_lattice); dc.Pop(); dc.Pop(); }
            }
            // sparks drifting up
            int sparks = Visuals.Sparks;
            if (!T.Light)
                foreach (var (x0, y0, v, sz, ph) in _sparks)
                {
                    if (sparks-- <= 0) break;
                    double y = (y0 - _t * v) % 1; if (y < 0) y += 1;
                    double x = x0 + 0.012 * Math.Sin(_t * 0.6 + ph);
                    double tw = 0.35 + 0.35 * Math.Sin(_t * 1.7 + ph);
                    dc.DrawEllipse((ph > 3.1 ? T.Select : T.Accent).A(tw * 0.55).Brush(), null, new Point(x * w, y * h), sz, sz);
                }
            // a vignette pulls the eye to the middle
            var vig = new RadialGradientBrush { RadiusX = 0.75, RadiusY = 0.75 };
            vig.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55)); vig.GradientStops.Add(new GradientStop(T.Bg0.A(T.Light ? 0.2 : 0.7), 1)); vig.Freeze();
            dc.DrawRectangle(vig, null, new Rect(s));
        }
    }

    /// <summary>
    /// Floating glass: chamfered (cut top-left + bottom-right), a gradient fill, scan lines, a top sheen, the rim and
    /// brackets on the two square corners. Lit 0..1 = hover / selected (brighter rim, glow).
    /// </summary>
    public sealed class Glass : FrameworkElement
    {
        private double _lit; public double Lit { get => _lit; set { _lit = value; InvalidateVisual(); } }
        public Color? Tint;
        public double Cut = -1;
        public bool Brackets = true;
        /// <summary>A near-opaque base under the glass (drawers, popups over busy content).</summary>
        public bool Solid;
        public Glass() { IsHitTestVisible = false; ThemeService.I.Changed += InvalidateVisual; }

        protected override void OnRender(DrawingContext dc)
        {
            var T = Cz.T; double w = ActualWidth, h = ActualHeight; if (w < 4 || h < 4) return;
            double cut = Cut >= 0 ? Cut : T.Cut;
            var tint = Tint ?? T.Accent;
            var shape = Cz.Chamfer(w, h, cut);
            if (Solid) dc.DrawGeometry(T.Bg0.Mix(T.Bg1, 0.35).A(0.95).Brush(), null, shape);
            var fill = new LinearGradientBrush(T.Panel.Mix(tint.A(T.Panel.A / 255.0), 0.06 + 0.06 * _lit), T.Panel.Mix(T.Bg0.A(T.Panel.A / 255.0), 0.45), 90); fill.Freeze();
            dc.DrawGeometry(fill, null, shape);
            dc.PushClip(shape);
            if (T.Scan > 0 && !T.Light)
            {
                var lines = new DrawingBrush(new GeometryDrawing(tint.A(0.05 * T.Scan + 0.02 * _lit).Brush(), null, new RectangleGeometry(new Rect(0, 0, 4, 1))))
                { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 4, 3), ViewportUnits = BrushMappingMode.Absolute }; lines.Freeze();
                dc.DrawRectangle(lines, null, new Rect(0, 0, w, h));
            }
            var sheen = new LinearGradientBrush(Colors.White.A(T.Light ? 0.25 : 0.05 + 0.03 * _lit), Colors.Transparent, 90); sheen.Freeze();
            dc.DrawRectangle(sheen, null, new Rect(0, 0, w, Math.Min(h, 46)));
            // a soft light where the energy sits (top-left), stronger when lit
            var spot = new RadialGradientBrush { Center = new Point(0.1, 0), GradientOrigin = new Point(0.1, 0), RadiusX = 0.8, RadiusY = 1.1 };
            spot.GradientStops.Add(new GradientStop(tint.A(0.07 + 0.08 * _lit), 0)); spot.GradientStops.Add(new GradientStop(tint.A(0), 1)); spot.Freeze();
            dc.DrawRectangle(spot, null, new Rect(0, 0, w, h));
            dc.Pop();
            var rim = new Pen(T.PanelEdge.Mix(tint, 0.25 + 0.5 * _lit).A(Math.Min(1, T.PanelEdge.A / 255.0 + 0.45 * _lit)).Brush(), 1); rim.Freeze();
            dc.DrawGeometry(null, rim, shape);
            if (Brackets)
            {
                var bp = new Pen(tint.A(0.55 + 0.45 * _lit).Brush(), 2) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square }; bp.Freeze();
                double L = 12 + 6 * _lit;
                dc.DrawLine(bp, new Point(w - L, 1), new Point(w - 1, 1)); dc.DrawLine(bp, new Point(w - 1, 1), new Point(w - 1, L));
                dc.DrawLine(bp, new Point(1, h - L), new Point(1, h - 1)); dc.DrawLine(bp, new Point(1, h - 1), new Point(L, h - 1));
                // the cut corners get a small tick
                var tp = new Pen(tint.A(0.35 + 0.4 * _lit).Brush(), 1.4); tp.Freeze();
                double c2 = Math.Min(cut, Math.Min(w, h) / 3);
                dc.DrawLine(tp, new Point(2, c2 + 3), new Point(c2 + 3, 2));
                dc.DrawLine(tp, new Point(w - c2 - 3, h - 2), new Point(w - 2, h - c2 - 3));
            }
        }
    }

    /// <summary>A chamfered word button (glyph + label). Primary = select.</summary>
    public sealed class CzButton : Grid
    {
        private readonly Path _bg = new Path { StrokeThickness = 1 };
        private readonly TextBlock _label, _glyph;
        private bool _hot, _down; private readonly bool _primary;
        /// <summary>What a click does (settable after construction).</summary>
        public Action Click;
        /// <summary>Change the caption (e.g. "WORKING...").</summary>
        public void SetLabel(string text) { if (_label != null) _label.Text = text; }
        public CzButton(string label, Action click, string glyph = null, bool primary = false, double height = 28, string tip = null)
        {
            Click = click; _primary = primary; Height = height; Cursor = Cursors.Hand; ToolTip = tip; Background = Brushes.Transparent;
            Children.Add(Cz.Under(_bg));
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(label == null ? 7 : 10, 0, label == null ? 7 : 11, 0), VerticalAlignment = VerticalAlignment.Center };
            if (glyph != null) { _glyph = Cz.Icon(glyph, 11.5, primary ? "CzSelectBrush" : "CzAccentBrush"); sp.Children.Add(_glyph); }
            if (label != null) { _label = Cz.Text(label, 11.5, primary ? "CzSelectBrush" : "CzTextBrush", "CzFontHead", FontWeights.SemiBold); _label.Margin = new Thickness(glyph != null ? 7 : 0, 0, 0, 0); sp.Children.Add(_label); }
            Children.Add(sp);
            SizeChanged += (s, e) => Look();
            ThemeService.I.Changed += Look;
            MouseEnter += (s, e) => { _hot = true; Look(); };
            MouseLeave += (s, e) => { _hot = false; _down = false; Look(); };
            MouseLeftButtonDown += (s, e) => { _down = true; CaptureMouse(); Look(); e.Handled = true; };
            MouseLeftButtonUp += (s, e) =>
            {
                bool was = _down; _down = false; ReleaseMouseCapture(); Look(); e.Handled = true;
                if (was && IsMouseOver && IsEnabled) Click?.Invoke();
            };
        }
        private void Look()
        {
            var T = Cz.T; var c = _primary || _down ? T.Select : T.Accent;
            _bg.Data = Cz.Chamfer(Math.Max(4, ActualWidth), Math.Max(4, ActualHeight), 5);
            _bg.Fill = c.A(_down ? 0.32 : _hot ? 0.2 : _primary ? 0.12 : 0.05).Brush();
            _bg.Stroke = c.A(_hot || _primary ? 0.9 : 0.35).Brush();
            if (_label != null) _label.SetResourceReference(TextBlock.ForegroundProperty, _hot || _primary ? (_primary || _down ? "CzSelectBrush" : "CzAccentBrush") : "CzTextBrush");
            if (_hot) Cz.Glow(_bg, c, 10, 0.55); else _bg.Effect = null;
        }
    }

    /// <summary>A round-less glyph button (window tools).</summary>
    public sealed class CzIconButton : Grid
    {
        public CzIconButton(string glyph, Action click, string tip = null, double size = 32, double glyphSize = 14)
        {
            Width = size; Height = size; Background = Brushes.Transparent; Cursor = Cursors.Hand; ToolTip = tip;
            var g = Cz.Icon(glyph, glyphSize, "CzTextDimBrush"); g.HorizontalAlignment = HorizontalAlignment.Center;
            Children.Add(g);
            MouseEnter += (s, e) => { g.SetResourceReference(TextBlock.ForegroundProperty, "CzAccentBrush"); Cz.Glow(g, Cz.T.Accent, 10, 0.8); };
            MouseLeave += (s, e) => { g.SetResourceReference(TextBlock.ForegroundProperty, "CzTextDimBrush"); g.Effect = null; };
            MouseLeftButtonUp += (s, e) => { e.Handled = true; click?.Invoke(); };
        }
    }

    /// <summary>A chamfered toggle: track + knob, select when on.</summary>
    public sealed class CzToggle : StackPanel
    {
        private bool _on; private readonly Path _track = new Path(), _knob = new Path();
        private readonly TranslateTransform _kx = new TranslateTransform();
        public bool On { get => _on; set { _on = value; Look(true); } }
        public CzToggle(bool on, Action<bool> changed, string label = null)
        {
            Orientation = Orientation.Horizontal; Cursor = Cursors.Hand; Background = Brushes.Transparent; _on = on;
            var g = new Grid { Width = 38, Height = 18, VerticalAlignment = VerticalAlignment.Center };
            _track.Data = Cz.Chamfer(38, 18, 5); _knob.Data = Cz.Chamfer(16, 12, 3.5); _knob.Margin = new Thickness(3, 3, 0, 0); _knob.HorizontalAlignment = HorizontalAlignment.Left; _knob.VerticalAlignment = VerticalAlignment.Top;
            _knob.RenderTransform = _kx;
            g.Children.Add(_track); g.Children.Add(_knob);
            Children.Add(g);
            if (label != null) { var t = Cz.Text(label, 12.5); t.Margin = new Thickness(10, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; Children.Add(t); }
            ThemeService.I.Changed += () => Look(false);
            MouseLeftButtonUp += (s, e) => { e.Handled = true; _on = !_on; Look(true); changed?.Invoke(_on); };
            Look(false);
        }
        private void Look(bool anim)
        {
            var T = Cz.T; var c = _on ? T.Select : T.Accent;
            _track.Fill = c.A(_on ? 0.22 : 0.06).Brush(); _track.Stroke = c.A(_on ? 0.9 : 0.4).Brush(); _track.StrokeThickness = 1;
            _knob.Fill = (_on ? T.Select : T.TextDim).Brush();
            double x = _on ? 16 : 0;
            if (anim && !Cz.Still) _kx.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { _kx.BeginAnimation(TranslateTransform.XProperty, null); _kx.X = x; }
            if (_on) Cz.Glow(_knob, T.Select, 8, 0.8); else _knob.Effect = null;
        }
    }

    /// <summary>A status light (breathes when <see cref="Pulse"/>).</summary>
    public sealed class Led : FrameworkElement
    {
        public Color Color; private double _t; private bool _pulse; private readonly Action _wake;
        public bool Pulse { get => _pulse; set { if (_pulse == value) return; _pulse = value; if (value) _wake?.Invoke(); } }
        public Led(Color c, double size = 8) { Color = c; Width = Height = size; IsHitTestVisible = false; _wake = Cz.Tick(this, dt => { if (!_pulse) return false; _t += dt; InvalidateVisual(); return true; }, 20, Pace.Pulse); }
        protected override void OnRender(DrawingContext dc)
        {
            double r = ActualWidth / 2; var c = new Point(r, r);
            double a = Pulse && Visuals.Fps(Pace.Pulse, 20) > 0 ? 0.55 + 0.45 * Math.Sin(_t * 3) : 1;
            if (!Cz.T.Light) { var halo = new RadialGradientBrush(Color.A(0.45 * a), Color.A(0)); halo.Freeze(); dc.DrawEllipse(halo, null, c, r * 2.4, r * 2.4); }
            dc.DrawEllipse(Color.A(a).Brush(), null, c, r * 0.72, r * 0.72);
            dc.DrawEllipse(Colors.White.A(0.55 * a).Brush(), null, new Point(r - r * 0.18, r - r * 0.2), r * 0.22, r * 0.22);
        }
    }

    /// <summary>Text that decodes in: random glyphs settle into the real letters, left to right.</summary>
    public static class Decode
    {
        private const string G = "01<>/\\|[]{}#%&*+=-_~:;ΞΣΔΛΠΦΨΩ";
        private static readonly Random R = new Random();
        public static void Play(TextBlock tb, string text, double delay = 0, double duration = 0.5)
        {
            if (Cz.Still || string.IsNullOrEmpty(text)) { tb.Text = text; return; }
            var start = DateTime.UtcNow.AddSeconds(delay);
            tb.Text = new string(' ', text.Length);
            var t = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            t.Tick += (s, e) =>
            {
                double k = (DateTime.UtcNow - start).TotalSeconds / duration;
                if (k < 0) return;
                if (k >= 1) { tb.Text = text; t.Stop(); return; }
                int done = (int)(text.Length * k);
                var c = text.ToCharArray();
                for (int i = done; i < c.Length; i++) if (c[i] != ' ') c[i] = i < done + 4 ? G[R.Next(G.Length)] : ' ';
                tb.Text = new string(c);
            };
            t.Start();
        }
    }

    /// <summary>Commits per day as glowing bars (oldest left, today brightest), with the peak marked.</summary>
    public sealed class ActivityBars : FrameworkElement
    {
        public int[] Days = Array.Empty<int>();
        public Color? Tint;
        private double _grow = 1;
        private readonly Action _wake;
        public ActivityBars()
        {
            IsHitTestVisible = false; ThemeService.I.Changed += InvalidateVisual;
            _wake = Cz.Tick(this, dt => { if (Cz.Still) _grow = 1; _grow = Math.Min(1, _grow + dt * 2.2); InvalidateVisual(); return _grow < 1; }, 30);
        }
        public void Set(int[] days)
        {
            Days = days ?? Array.Empty<int>(); _grow = Cz.Still ? 1 : 0;
            if (_grow < 1) _wake();
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc)
        {
            var T = Cz.T; var c = Tint ?? T.Accent; double w = ActualWidth, h = ActualHeight; int n = Math.Max(1, Days.Length);
            dc.DrawRectangle(T.Line.Brush(), null, new Rect(0, h - 1, w, 1));
            if (Days.Length == 0) return;
            int max = Math.Max(1, Days.Max()); double bw = w / n;
            double ease = 1 - Math.Pow(1 - _grow, 3);
            for (int i = 0; i < Days.Length; i++)
            {
                double v = Math.Sqrt(Days[i] / (double)max) * ease;
                double bh = Days[i] == 0 ? 1.5 : Math.Max(3, v * (h - 4));
                var r = new Rect(i * bw + 1.2, h - 1 - bh, Math.Max(1, bw - 2.4), bh);
                bool today = i == Days.Length - 1;
                var b = new LinearGradientBrush(c.Mix(T.Select, 0.5).A(today ? 1 : 0.85), c.A(Days[i] == 0 ? 0.18 : today ? 0.75 : 0.45), 90); b.Freeze();
                dc.DrawRectangle(b, null, r);
                if (Days[i] == max && max > 0 && !T.Light) { var glow = new RadialGradientBrush(c.A(0.45), c.A(0)); glow.Freeze(); dc.DrawEllipse(glow, null, new Point(r.X + r.Width / 2, r.Y), bw * 1.2, 6); dc.DrawRectangle(Colors.White.A(0.9).Brush(), null, new Rect(r.X, r.Y - 2, r.Width, 1.5)); }
            }
        }
    }

    /// <summary>
    /// A status chip: a light and a short mono text in a small chamfered outline, tinted by its tone. Clickable when it
    /// has a click (it lifts and glows on hover). Card badges and the header's "n HELPERS" are chips.
    /// </summary>
    public sealed class Chip : Grid
    {
        private readonly Path _bg = new Path { StrokeThickness = 1 };
        private readonly Led _led;
        private readonly TextBlock _text;
        private Tone _tone = Tone.Dim;
        private bool _hot;
        public Action<Chip> Click;
        public Action<Chip> RightClick;

        public Chip(double height = 24)
        {
            Height = height; Background = Brushes.Transparent;
            Children.Add(Cz.Under(_bg));
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(9, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            _led = new Led(Cz.T.TextDim, 8) { VerticalAlignment = VerticalAlignment.Center };
            _text = Cz.Mono("", 10, "CzTextDimBrush"); _text.Margin = new Thickness(7, 0, 0, 0); _text.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(_led); sp.Children.Add(_text);
            Children.Add(sp);
            SizeChanged += (s, e) => Look();
            ThemeService.I.Changed += Look;
            MouseEnter += (s, e) => { _hot = Click != null; Look(); };
            MouseLeave += (s, e) => { _hot = false; Look(); };
            MouseLeftButtonUp += (s, e) => { if (Click == null) return; e.Handled = true; Click(this); };
            MouseRightButtonUp += (s, e) => { if (RightClick == null) return; e.Handled = true; RightClick(this); };
        }

        public void Set(string text, Tone tone, bool pulse, string tip)
        {
            _tone = tone;
            if (_text.Text != text) { if (string.IsNullOrEmpty(_text.Text)) Decode.Play(_text, text, 0, 0.3); else _text.Text = text; }
            _text.SetResourceReference(TextBlock.ForegroundProperty, Cz.ToneBrush(tone));
            _led.Color = Cz.ToneColor(tone); _led.Pulse = pulse; _led.InvalidateVisual();
            ToolTip = tip;
            Cursor = Click != null ? Cursors.Hand : null;
            Look();
        }

        private void Look()
        {
            var c = Cz.ToneColor(_tone);
            _led.Color = c; _led.InvalidateVisual();
            _bg.Data = Cz.Chamfer(Math.Max(4, ActualWidth), Math.Max(4, ActualHeight), 5);
            _bg.Fill = c.A(_hot ? 0.18 : 0.07).Brush();
            _bg.Stroke = c.A(_hot ? 0.85 : 0.4).Brush();
            if (_hot) Cz.Glow(_bg, c, 10, 0.5); else _bg.Effect = null;
        }
    }

    /// <summary>Minimize / maximize / close, drawn.</summary>
    public sealed class WinButton : FrameworkElement
    {
        public enum Kind { Min, Max, Close }
        private readonly Kind _k; private bool _hot;
        public Window Window;
        public WinButton(Kind k, Action click)
        {
            _k = k; Width = 44; Height = 32; Cursor = Cursors.Hand;
            MouseEnter += (s, e) => { _hot = true; InvalidateVisual(); };
            MouseLeave += (s, e) => { _hot = false; InvalidateVisual(); };
            MouseLeftButtonUp += (s, e) => { e.Handled = true; click(); };
            ThemeService.I.Changed += InvalidateVisual;
        }
        protected override void OnRender(DrawingContext dc)
        {
            var T = Cz.T; var c = _k == Kind.Close && _hot ? T.Danger : _hot ? T.Accent : T.TextDim;
            dc.DrawRectangle(_hot ? c.A(0.14).Brush() : Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            var p = new Pen(c.Brush(), 1.3); double x = ActualWidth / 2, y = ActualHeight / 2;
            if (_k == Kind.Min) dc.DrawLine(p, new Point(x - 6, y), new Point(x + 6, y));
            else if (_k == Kind.Close) { dc.DrawLine(p, new Point(x - 5.5, y - 5.5), new Point(x + 5.5, y + 5.5)); dc.DrawLine(p, new Point(x + 5.5, y - 5.5), new Point(x - 5.5, y + 5.5)); }
            else if (Window?.WindowState == WindowState.Maximized)
            {
                // restore: a chamfered square in front, the corner of a second one behind it
                dc.DrawGeometry(null, p, Cz.Chamfer(10, 10, 3).CloneCurrentValue().Also(g => g.Transform = new TranslateTransform(x - 6.5, y - 3.5)));
                dc.DrawLine(p, new Point(x - 3.5, y - 6.5), new Point(x + 6.5, y - 6.5));
                dc.DrawLine(p, new Point(x + 6.5, y - 6.5), new Point(x + 6.5, y + 3.5));
            }
            else dc.DrawGeometry(null, p, Cz.Chamfer(12, 12, 3.5).CloneCurrentValue().Also(g => g.Transform = new TranslateTransform(x - 6, y - 6)));
        }
    }

    public static class Fx
    {
        public static T Also<T>(this T x, Action<T> a) { a(x); return x; }
    }

    /// <summary>A Catalyzer menu (glass, never a native context menu).</summary>
    public sealed class CzMenu
    {
        public sealed class Item { public string Text, Glyph, Hint; public Action Click; public bool Separator, Check, Danger; }
        private Popup _pop;
        /// <summary>The menu went away (picked, clicked outside, or Esc).</summary>
        public event Action Closed;
        public PlacementMode Placement = PlacementMode.MousePoint;

        /// <summary>A plugin's menu lines (SDK) as menu items.</summary>
        public static List<Item> From(IEnumerable<MenuEntry> entries, string what)
            => (entries ?? Enumerable.Empty<MenuEntry>()).Where(e => e != null).Select(e => new Item
            {
                Text = e.Text, Glyph = e.Glyph, Hint = e.Hint, Separator = e.Separator, Danger = e.Danger,
                Click = e.Run == null ? null : () => { try { e.Run(); } catch (Exception ex) { Log.Write(what + ": " + ex.Message, show: true); } },
            }).ToList();
        public void Show(UIElement anchor, string header, IEnumerable<Item> items)
        {
            _pop?.SetValue(Popup.IsOpenProperty, false);
            var panel = new StackPanel { Margin = new Thickness(6, 8, 6, 8), MinWidth = 230 };
            if (header != null) { var h = Cz.Mono(header.ToUpperInvariant(), 10, "CzTextFaintBrush"); h.Margin = new Thickness(10, 0, 10, 6); panel.Children.Add(h); }
            foreach (var it in items)
            {
                if (it.Separator) { var r = new Rectangle { Height = 1, Margin = new Thickness(8, 5, 8, 5) }; r.SetResourceReference(Shape.FillProperty, "CzLineBrush"); panel.Children.Add(r); continue; }
                var g = new Grid { Height = 30, Background = Brushes.Transparent, Cursor = Cursors.Hand };
                var hl = new Path { Opacity = 0 }; g.Children.Add(Cz.Under(hl));
                g.SizeChanged += (s, e) => { hl.Data = Cz.Chamfer(e.NewSize.Width, e.NewSize.Height, 4); hl.Fill = Cz.T.Accent.A(0.16).Brush(); hl.Stroke = Cz.T.Accent.A(0.5).Brush(); };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                var ic = Cz.Icon(it.Check ? "" : it.Glyph ?? "", 12, it.Danger ? "CzDangerBrush" : it.Check ? "CzSelectBrush" : "CzAccentBrush"); ic.Width = 18; sp.Children.Add(ic);
                var tx = Cz.Text(it.Text, 13, it.Danger ? "CzDangerBrush" : "CzTextBrush"); tx.Margin = new Thickness(8, 0, 0, 0); sp.Children.Add(tx);
                if (it.Hint != null) { var hn = Cz.Mono("  " + it.Hint, 10, "CzTextFaintBrush"); hn.VerticalAlignment = VerticalAlignment.Center; sp.Children.Add(hn); }
                g.Children.Add(sp);
                g.MouseEnter += (s, e) => hl.Opacity = 1; g.MouseLeave += (s, e) => hl.Opacity = 0;
                var act = it.Click;
                g.MouseLeftButtonUp += (s, e) => { _pop.IsOpen = false; act?.Invoke(); };
                panel.Children.Add(g);
            }
            var host = new Grid();
            host.Children.Add(new Glass { Cut = 8, Solid = true });
            host.Children.Add(panel);
            var shadow = new Border { Child = host, Margin = new Thickness(14) };
            Cz.Glow(host, Cz.T.Accent, 18, 0.35);
            _pop = new Popup { Child = shadow, AllowsTransparency = true, StaysOpen = false, PlacementTarget = anchor, Placement = Placement, PopupAnimation = Cz.Still ? PopupAnimation.None : PopupAnimation.Fade };
            _pop.Closed += (s, e) => Closed?.Invoke();
            _pop.IsOpen = true;
        }
    }
}
