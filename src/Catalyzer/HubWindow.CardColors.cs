using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// Card colours (right-click a card): a primary and a secondary per card, kept in Catalyzer's settings (not the
    /// repo). The rim, wash and badge run from one to the other; when something works on the repo they shift through
    /// each other. The swatches are every theme's own colours, so nothing here is a literal.
    /// </summary>
    public sealed partial class HubWindow
    {
        private void CardColors(ProjectCard card)
        {
            var p = card.P;
            var cur = Prefs.ColorsFor(p.Folder);
            string primary = cur?.Primary, secondary = cur?.Secondary;
            var d = Dialog("Card colours  //  " + p.Name, 600, onEsc: null);
            d.Body.Children.Add(Para("Two colours for this card. At rest the card runs gently from the first to the second; while the link or git works on the repo, they shift through each other. Kept in Catalyzer's settings, not in the repo."));

            // a live preview: resting on the left, working on the right
            var prev = new Grid { Height = 64, Margin = new Thickness(0, 0, 0, 14) };
            prev.ColumnDefinitions.Add(new ColumnDefinition()); prev.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); prev.ColumnDefinitions.Add(new ColumnDefinition());
            var rest = new Glass { Cut = 10 }; var work = new Glass { Cut = 10, Energy = 1 };
            Grid.SetColumn(work, 2); prev.Children.Add(rest); prev.Children.Add(work);
            var restLabel = Cz.Mono("AT REST", 10, "CzTextDimBrush").Also(t => { t.Margin = new Thickness(14, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; });
            var workLabel = Cz.Mono("WORKING", 10, "CzTextDimBrush").Also(t => { t.Margin = new Thickness(14, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center; });
            prev.Children.Add(restLabel); Grid.SetColumn(workLabel, 2); prev.Children.Add(workLabel);
            Cz.Tick(work, dt => { work.Phase += dt * 1.75; work.InvalidateVisual(); return true; }, 30, Pace.Pulse);
            d.Body.Children.Add(prev);

            Color Fallback1() => p.Accent ?? Cz.T.Accent;
            void Show()
            {
                foreach (var g in new[] { rest, work }) { g.Tint = Prefs.Hex(primary) ?? Fallback1(); g.Tint2 = Prefs.Hex(secondary); g.InvalidateVisual(); }
            }

            var palette = Palette(p);
            Action redraw1 = null, redraw2 = null;
            d.Body.Children.Add(Caption("Primary" + (p.Accent != null ? "  //  default: catalyzer.json's accent" : "  //  default: the theme's accent")));
            d.Body.Children.Add(SwatchRow(palette, () => primary, v => { primary = v; Show(); redraw1?.Invoke(); }, out redraw1));
            d.Body.Children.Add(Caption("Secondary  //  default: none (working shifts to the theme's select colour)"));
            d.Body.Children.Add(SwatchRow(palette, () => secondary, v => { secondary = v; Show(); redraw2?.Invoke(); }, out redraw2));
            Show();

            d.Buttons.Children.Add(Btn("DEFAULT", () => { primary = null; secondary = null; Show(); redraw1(); redraw2(); }, "", tip: "BACK TO CATALYZER.JSON'S / THE THEME'S COLOUR"));
            d.Buttons.Children.Add(Btn("CANCEL", d.Close));
            d.Buttons.Children.Add(Btn("SAVE", () =>
            {
                Prefs.Save(x =>
                {
                    var key = p.Folder.ToLowerInvariant();
                    if (primary == null && secondary == null) x.CardColors.Remove(key);
                    else x.CardColors[key] = new CardColorPref { Primary = primary, Secondary = secondary };
                });
                card.Recolor(); d.Close();
                Say("CARD COLOURS  //  " + p.Name.ToUpperInvariant());
            }, "", primary: true));
        }

        /// <summary>The colours to pick from: the card's own accent, then every theme's roles (deduplicated).</summary>
        private static List<Color> Palette(Project p)
        {
            var all = new List<Color>();
            if (p.Accent is Color a) all.Add(a);
            foreach (var t in ThemeService.I.All)
                foreach (var c in new[] { t.Accent, t.Select, t.Ok, t.Warn, t.Danger })
                    if (c.A > 0) all.Add(Color.FromRgb(c.R, c.G, c.B));
            var keep = new List<Color>();
            foreach (var c in all)
                if (!keep.Any(k => Math.Abs(k.R - c.R) + Math.Abs(k.G - c.G) + Math.Abs(k.B - c.B) < 40)) keep.Add(c);
            return keep.Take(24).ToList();
        }

        private static string HexOf(Color c) => "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

        /// <summary>A row of swatches + DEFAULT + a #hex field. <paramref name="redraw"/> refreshes which one is ticked.</summary>
        private static FrameworkElement SwatchRow(List<Color> palette, Func<string> get, Action<string> set, out Action redraw)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var marks = new List<(Path ring, string hex)>();
            Path Swatch(Color? c, string hex, string tip)
            {
                var host = new Grid { Width = 30, Height = 30, Margin = new Thickness(0, 0, 8, 8), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = tip };
                var face = new Path { Data = Cz.Chamfer(26, 26, 6), Margin = new Thickness(2), StrokeThickness = 1 };
                if (c is Color col) { face.Fill = col.Brush(); face.Stroke = col.Mix(Colors.White, 0.3).Brush(); }
                else { face.SetResourceReference(Shape.StrokeProperty, "CzLineBrush"); face.Fill = Brushes.Transparent; }
                var ring = new Path { Data = Cz.Chamfer(30, 30, 7), StrokeThickness = 2, Visibility = Visibility.Hidden };
                ring.SetResourceReference(Shape.StrokeProperty, "CzSelectBrush");
                host.Children.Add(face); host.Children.Add(ring);
                if (c == null) host.Children.Add(Cz.Icon("", 11, "CzTextDimBrush").Also(i => i.HorizontalAlignment = HorizontalAlignment.Center));
                host.MouseLeftButtonUp += (s, e) => set(hex);
                wrap.Children.Add(host);
                marks.Add((ring, hex));
                return ring;
            }
            Swatch(null, null, "DEFAULT");
            foreach (var c in palette) Swatch(c, HexOf(c), HexOf(c));
            var (row, box) = Field("#RRGGBB", get() ?? "", 32);
            row.Width = 120; row.Margin = new Thickness(0, 0, 0, 8);
            bool typing = false;
            box.TextChanged += (s, e) => { if (typing) return; var h = box.Text.Trim(); if (h == "") return; if (!h.StartsWith("#")) h = "#" + h; if (Prefs.Hex(h) is Color col && h.Length == 7) { typing = true; set(HexOf(col)); typing = false; } };
            wrap.Children.Add(row);
            redraw = () =>
            {
                var v = get();
                foreach (var (ring, hex) in marks) ring.Visibility = string.Equals(hex, v, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Hidden;
                if (!typing) { typing = true; box.Text = v ?? ""; typing = false; }
            };
            redraw();
            return wrap;
        }
    }
}
