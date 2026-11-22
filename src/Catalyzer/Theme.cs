using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// The theme layer. Every control reads Cz* resources (brushes, fonts) or <see cref="Current"/> - never a literal
    /// colour - so a theme (built in or from a plugin) reskins everything live.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        public static ThemeService I { get; } = new ThemeService();
        private readonly List<CatalyzerTheme> _all = new List<CatalyzerTheme>();
        public CatalyzerTheme Current { get; private set; }
        public IReadOnlyList<CatalyzerTheme> All => _all;
        public event Action Changed;

        private ThemeService()
        {
            foreach (var t in BuiltIn()) _all.Add(t);
            Current = _all[0];
        }

        public void Register(CatalyzerTheme theme)
        {
            if (theme == null || string.IsNullOrWhiteSpace(theme.Id)) return;
            Fill(theme);
            int i = _all.FindIndex(t => t.Id == theme.Id);
            if (i >= 0) _all[i] = theme; else _all.Add(theme);
            // the theme you picked arrived (a plugin's, at start) or a live theme updated itself: show it
            if (Current?.Id == theme.Id || (Prefs.Value.Theme == theme.Id && Current?.Id != theme.Id)) Set(theme);
            else Changed?.Invoke();   // the list grew (settings show it)
        }

        public void Apply(string id)
        {
            var t = _all.FirstOrDefault(x => x.Id == id);
            if (t == null) return;
            Prefs.Save(p => p.Theme = id);
            Set(t);
        }

        /// <summary>At start: your saved choice if it's here (a plugin's theme arrives later and takes over then).</summary>
        public void Init()
        {
            var t = _all.FirstOrDefault(x => x.Id == Prefs.Value.Theme) ?? _all[0];
            Set(t);
        }

        private void Set(CatalyzerTheme t)
        {
            Current = t;
            var r = Application.Current.Resources;
            void B(string key, Color c) { var b = new SolidColorBrush(c); b.Freeze(); r[key] = b; }
            B("CzBg0Brush", t.Bg0); B("CzBg1Brush", t.Bg1); B("CzPanelBrush", t.Panel); B("CzEdgeBrush", t.PanelEdge);
            B("CzLineBrush", t.Line); B("CzTextBrush", t.Text); B("CzTextDimBrush", t.TextDim); B("CzTextFaintBrush", t.TextFaint);
            B("CzAccentBrush", t.Accent); B("CzSelectBrush", t.Select); B("CzOkBrush", t.Ok); B("CzWarnBrush", t.Warn); B("CzDangerBrush", t.Danger);
            B("CzAccentSoftBrush", t.Accent.A(0.14)); B("CzSelectSoftBrush", t.Select.A(0.16));
            r["CzFontUi"] = t.FontUi; r["CzFontHead"] = t.FontHead; r["CzFontMono"] = t.FontMono;
            Changed?.Invoke();
        }

        /// <summary>A plugin's theme may leave things out: Catalyzer's defaults fill the gaps.</summary>
        private static void Fill(CatalyzerTheme t)
        {
            var d = BuiltIn()[0];
            t.FontUi ??= d.FontUi; t.FontHead ??= d.FontHead; t.FontMono ??= d.FontMono;
            if (t.Panel.A == 0) t.Panel = t.Bg1.A(0.72);
            if (t.PanelEdge.A == 0) t.PanelEdge = t.Accent.A(0.35);
            if (t.Line.A == 0) t.Line = t.Accent.A(0.22);
            if (t.Ok.A == 0) t.Ok = d.Ok; if (t.Warn.A == 0) t.Warn = d.Warn; if (t.Danger.A == 0) t.Danger = d.Danger;
            if (string.IsNullOrEmpty(t.Name)) t.Name = t.Id;
        }

        private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        /// <summary>Catalyzer's own themes (no plugin needed).</summary>
        public static List<CatalyzerTheme> BuiltIn()
        {
            var ui = new FontFamily("Segoe UI Variable Display, Segoe UI");
            var head = new FontFamily("Bahnschrift SemiBold, Segoe UI Semibold");
            var mono = new FontFamily("Cascadia Mono, Cascadia Code, Consolas");
            CatalyzerTheme T(string id, string name, string bg0, string bg1, string text, string dim, string faint, string acc, string sel) => new CatalyzerTheme
            {
                Id = id, Name = name, Source = "Catalyzer",
                Bg0 = C(bg0), Bg1 = C(bg1), Text = C(text), TextDim = C(dim), TextFaint = C(faint), Accent = C(acc), Select = C(sel),
                Panel = C(bg1).A(0.74), PanelEdge = C(acc).A(0.34), Line = C(acc).A(0.2),
                Ok = C("#3DFF9A"), Warn = C("#FFC447"), Danger = C("#FF3D63"),
                FontUi = ui, FontHead = head, FontMono = mono,
            };
            return new List<CatalyzerTheme>
            {
                // reactor: the catalyst glow - teal energy, violet for what matters
                T("catalyst", "Catalyst", "#04060C", "#0B1222", "#E4F4FF", "#86A0C2", "#33445F", "#24F0C6", "#9C7BFF"),
                // plasma: magenta field, cyan picks
                T("plasma", "Plasma", "#07040C", "#160B22", "#FBE9FF", "#B593C9", "#4D3460", "#FF4FD8", "#3FD8FF"),
                // ember: forge orange, ice-blue picks
                T("ember", "Ember", "#0A0503", "#1C0E07", "#FFF1E4", "#C9A084", "#5E3E2A", "#FF8A2E", "#4FC3FF"),
                // ion: clean blue-white
                T("ion", "Ion", "#03060A", "#0A1420", "#EAF6FF", "#8EA9C2", "#34475C", "#4FB8FF", "#7CFFB2"),
            };
        }
    }

    public static class ColorX
    {
        public static Color A(this Color c, double a) => Color.FromArgb((byte)Math.Clamp(a * 255, 0, 255), c.R, c.G, c.B);
        public static Color Mix(this Color a, Color b, double t) => Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        public static SolidColorBrush Brush(this Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    }
}
