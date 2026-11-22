using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    /// <summary>Settings: a glass drawer sliding in from the right - GENERAL, PROJECT FOLDERS, THEME, PLUGINS, ABOUT.</summary>
    public sealed partial class HubWindow
    {
        private readonly Grid _drawerHost = new Grid { Visibility = Visibility.Collapsed };
        private readonly StackPanel _drawerBody = new StackPanel { Margin = new Thickness(26, 22, 24, 26) };
        private readonly TranslateTransform _drawerX = new TranslateTransform();
        private bool _drawerOpen, _restartNeeded;

        internal void OpenSettingsForSnapshot() => Drawer(true);

        private void Drawer(bool open)
        {
            if (_drawerHost.Children.Count == 0) BuildDrawerShell();
            _drawerOpen = open;
            if (open) { BuildSettings(); _drawerHost.Visibility = Visibility.Visible; }
            double to = open ? 0 : 520;
            if (Cz.Still) { _drawerX.X = to; if (!open) _drawerHost.Visibility = Visibility.Collapsed; return; }
            var a = new DoubleAnimation(open ? 520 : 0, to, TimeSpan.FromMilliseconds(open ? 280 : 200)) { EasingFunction = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn } };
            if (!open) a.Completed += (s, e) => { if (!_drawerOpen) _drawerHost.Visibility = Visibility.Collapsed; };
            _drawerX.BeginAnimation(TranslateTransform.XProperty, a);
        }

        private void BuildDrawerShell()
        {
            // a dim veil over the board (click it to close), the drawer on the right
            var veil = new Rectangle { Fill = Brushes.Black, Opacity = 0.5 };
            veil.MouseLeftButtonDown += (s, e) => Drawer(false);
            _drawerHost.Children.Add(veil);
            var drawer = new Grid { Width = 500, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 10, 10), RenderTransform = _drawerX };
            drawer.Children.Add(new Glass { Cut = 16, Solid = true });
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _drawerBody, Focusable = false };
            drawer.Children.Add(sv);
            Cz.Glow(drawer, Cz.T.Accent, 30, 0.35);
            _drawerHost.Children.Add(drawer);
        }

        private static FrameworkElement Section(string label, string right = null)
        {
            var g = new Grid { Margin = new Thickness(0, 22, 0, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dia = new Rectangle { Width = 6, Height = 6, Margin = new Thickness(1, 0, 10, 0), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(45), VerticalAlignment = VerticalAlignment.Center };
            dia.SetResourceReference(Shape.FillProperty, "CzAccentBrush");
            g.Children.Add(dia);
            var t = Cz.Head(label, 13, "CzAccentBrush"); Grid.SetColumn(t, 1); g.Children.Add(t);
            var line = new Rectangle { Height = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            line.Fill = new LinearGradientBrush(Cz.T.Accent.A(0.45), Cz.T.Accent.A(0), 0);
            var lg = new Grid(); lg.Children.Add(line);
            if (right != null) { var r = Cz.Mono(right, 10, "CzTextFaintBrush"); r.HorizontalAlignment = HorizontalAlignment.Right; r.Background = Brushes.Transparent; lg.Children.Add(r); }
            Grid.SetColumn(lg, 1); g.Children.Add(lg);
            Grid.SetColumn(t, 1); Grid.SetColumn(lg, 2); g.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star); g.ColumnDefinitions[1].Width = GridLength.Auto;
            return g;
        }

        private static TextBlock Note(string text) { var t = Cz.Text(text, 11.5, "CzTextFaintBrush"); t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new Thickness(0, 2, 0, 8); return t; }

        private void BuildSettings()
        {
            var b = _drawerBody; b.Children.Clear();
            var top = new Grid();
            top.Children.Add(Cz.Head("Settings", 22, "CzTextBrush"));
            var x = new CzIconButton("", () => Drawer(false), "CLOSE  //  ESC") { HorizontalAlignment = HorizontalAlignment.Right };
            top.Children.Add(x);
            b.Children.Add(top);

            // ---------------- GENERAL
            b.Children.Add(Section("General"));
            b.Children.Add(new CzToggle(Launch.AtStartup, on => Launch.AtStartup = on, "Run at Windows start") { Margin = new Thickness(0, 4, 0, 10) });
            b.Children.Add(new CzToggle(Prefs.Value.ReduceMotion, on => { Prefs.Save(p => p.ReduceMotion = on); }, "Reduce motion") { Margin = new Thickness(0, 0, 0, 4) });
            b.Children.Add(Note("Starting with Windows opens Catalyzer minimized."));
            b.Children.Add(Cz.Mono("CLOSING WHILE HELPERS RUN", 10, "CzTextDimBrush").Also(t => t.Margin = new Thickness(0, 10, 0, 8)));
            var close = new WrapPanel();
            foreach (var (id, label, glyph) in new[] { ("ask", "ASK", "\uE897"), ("stop", "STOP ALL & EXIT", "\uE71A"), ("tray", "KEEP IN TRAY", "\uE70D") })
            {
                var v = id;
                close.Children.Add(new CzButton(label, () => { Prefs.Save(p => p.CloseWithWork = v); BuildSettings(); }, glyph, primary: Prefs.Value.CloseWithWork == v) { Margin = new Thickness(0, 0, 8, 8) });
            }
            b.Children.Add(close);
            b.Children.Add(Note("Helpers always stop if Catalyzer itself stops - nothing is left running behind your back."));

            // ---------------- FOLDERS
            b.Children.Add(Section("Project folders", Prefs.Value.Roots.Count + Prefs.Value.Extra.Count + " FOLDERS"));
            b.Children.Add(Note("Every git repo (or folder with a catalyzer.json) inside these shows as a card. 'archives' is never scanned."));
            foreach (var r in Prefs.Value.Roots.ToList()) b.Children.Add(PathRow(r, "SCAN", () => { Prefs.Save(p => p.Roots.Remove(r)); _board.Rescan(); BuildSettings(); }));
            foreach (var r in Prefs.Value.Extra.ToList()) b.Children.Add(PathRow(r, "PROJECT", () => { Prefs.Save(p => p.Extra.Remove(r)); _board.Rescan(); BuildSettings(); }));
            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            addRow.Children.Add(new CzButton("ADD FOLDER", () => { AddFolder(); BuildSettings(); }, "") { Margin = new Thickness(0, 0, 8, 0) });
            if (Prefs.Value.Hidden.Count > 0)
                addRow.Children.Add(new CzButton("SHOW " + Prefs.Value.Hidden.Count + " HIDDEN", () => { Prefs.Save(p => p.Hidden.Clear()); _board.Rescan(); BuildSettings(); }, ""));
            b.Children.Add(addRow);

            // ---------------- THEME
            b.Children.Add(Section("Theme", ThemeService.I.All.Count + " THEMES"));
            var themes = new WrapPanel();
            foreach (var t in ThemeService.I.All)
            {
                var th = t;
                themes.Children.Add(ThemeChip(th));
            }
            b.Children.Add(themes);
            b.Children.Add(Note("Plugins can bring themes (the Nexus plugin follows your live Nexus theme)."));

            // ---------------- PLUGINS
            var host = PluginHost.I;
            b.Children.Add(Section("Plugins", (host?.Plugins.Count(p => p.Running) ?? 0) + " RUNNING"));
            if (host == null || host.Plugins.Count == 0) b.Children.Add(Note("No plugins yet. A plugin is a .dll built on Catalyzer.Sdk (docs\\PLUGINS.md)."));
            else foreach (var p in host.Plugins) b.Children.Add(PluginRow(p));
            var prow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            prow.Children.Add(new CzButton("ADD / IMPORT", ImportPlugin, "", primary: true, tip: "A PLUGIN .DLL (ITS FOLDER COMES ALONG) OR A .ZIP") { Margin = new Thickness(0, 0, 8, 8) });
            prow.Children.Add(new CzButton("PLUGINS FOLDER", () => { Directory.CreateDirectory(PluginHost.UserFolder); Launch.Shell("explorer.exe", "\"" + PluginHost.UserFolder + "\""); }, "") { Margin = new Thickness(0, 0, 8, 8) });
            if (_restartNeeded) prow.Children.Add(new CzButton("RESTART CATALYZER", Restart, "", tip: "PLUGIN CHANGES TAKE EFFECT AFTER A RESTART") { Margin = new Thickness(0, 0, 8, 8) });
            b.Children.Add(prow);
            if (_restartNeeded) b.Children.Add(Note("Plugin changes take effect after a restart."));

            // ---------------- ABOUT
            b.Children.Add(Section("About"));
            b.Children.Add(Cz.Mono("CATALYZER  V" + (host?.AppVersion ?? "0.1") + "   //   PLUGIN API " + Api.Version, 11, "CzTextDimBrush"));
            var about = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            about.Children.Add(new CzButton("LOG", () => Launch.Shell("explorer.exe", "\"" + System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Catalyzer") + "\""), "") { Margin = new Thickness(0, 0, 8, 8) });
            about.Children.Add(new CzButton("SETTINGS FILE", () => Launch.Shell("explorer.exe", "\"" + Prefs.Folder + "\""), "") { Margin = new Thickness(0, 0, 8, 8) });
            b.Children.Add(about);
        }

        private FrameworkElement PathRow(string path, string kind, Action remove)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2), Height = 28 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) }); g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var k = Cz.Mono(kind, 9.5, "CzAccentBrush"); k.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(k);
            var t = Cz.Mono(path.ToUpperInvariant(), 10.5, "CzTextBrush"); t.VerticalAlignment = VerticalAlignment.Center; t.ToolTip = path; Grid.SetColumn(t, 1); g.Children.Add(t);
            var x = new CzIconButton("", remove, "REMOVE", 26, 10); Grid.SetColumn(x, 2); g.Children.Add(x);
            return g;
        }

        private FrameworkElement ThemeChip(CatalyzerTheme t)
        {
            bool on = Cz.T.Id == t.Id;
            var g = new Grid { Width = 140, Height = 66, Margin = new Thickness(0, 0, 10, 10), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = t.Name + "  //  FROM " + t.Source.ToUpperInvariant() };
            var glass = new Glass { Cut = 8, Tint = t.Accent, Lit = on ? 1 : 0 };
            g.Children.Add(glass);
            var sp = new StackPanel { Margin = new Thickness(12, 9, 10, 8) };
            var swatch = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            foreach (var c in new[] { t.Bg1, t.Accent, t.Select, t.Text })
                swatch.Children.Add(new Rectangle { Width = 16, Height = 8, Margin = new Thickness(0, 0, 4, 0), Fill = c.Brush(), Stroke = t.PanelEdge.Brush(), StrokeThickness = 0.6 });
            sp.Children.Add(swatch);
            var n = Cz.Text(t.Name.ToUpperInvariant(), 12, on ? "CzSelectBrush" : "CzTextBrush", "CzFontHead", FontWeights.SemiBold); sp.Children.Add(n);
            sp.Children.Add(Cz.Mono(t.Source.ToUpperInvariant(), 9, "CzTextFaintBrush"));
            g.Children.Add(sp);
            g.MouseEnter += (s, e) => { if (!on) glass.Lit = 0.5; };
            g.MouseLeave += (s, e) => { if (!on) glass.Lit = 0; };
            g.MouseLeftButtonUp += (s, e) => { ThemeService.I.Apply(t.Id); BuildSettings(); };
            return g;
        }

        private FrameworkElement PluginRow(PluginInfo p)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            var tg = new CzToggle(p.On && p.Error == null, on => { if (p.Id == null) return; PluginHost.I.Switch(p, on); _restartNeeded = true; BuildSettings(); }) { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0) };
            if (p.Error != null) tg.IsEnabled = false;
            g.Children.Add(tg);
            var sp = new StackPanel();
            var name = new StackPanel { Orientation = Orientation.Horizontal };
            name.Children.Add(Cz.Text(p.Name ?? "?", 14, "CzTextBrush", "CzFontHead", FontWeights.SemiBold));
            var meta = Cz.Mono("   V" + (p.Version ?? "?") + (p.Author != null ? "  //  " + p.Author.ToUpperInvariant() : "") + "  //  " + (p.Bundled ? "BUNDLED" : "ADDED") + (p.Running ? "  //  RUNNING" : ""), 9.5, p.Running ? "CzOkBrush" : "CzTextFaintBrush");
            meta.VerticalAlignment = VerticalAlignment.Center;
            name.Children.Add(meta);
            sp.Children.Add(name);
            if (!string.IsNullOrWhiteSpace(p.Description)) { var d = Cz.Text(p.Description, 12, "CzTextDimBrush"); d.TextWrapping = TextWrapping.Wrap; d.TextTrimming = TextTrimming.None; d.Margin = new Thickness(0, 2, 0, 0); sp.Children.Add(d); }
            if (p.Error != null) { var e = Cz.Text(p.Error, 11.5, "CzWarnBrush"); e.TextWrapping = TextWrapping.Wrap; e.TextTrimming = TextTrimming.None; sp.Children.Add(e); }
            var f = Cz.Mono(p.Folder.ToUpperInvariant(), 9, "CzTextFaintBrush"); f.ToolTip = p.Folder; f.Margin = new Thickness(0, 3, 0, 0); sp.Children.Add(f);
            Grid.SetColumn(sp, 1); g.Children.Add(sp);
            return g;
        }

        private void ImportPlugin()
        {
            var d = new Microsoft.Win32.OpenFileDialog { Title = "A Catalyzer plugin", Filter = "Catalyzer plugin (*.dll;*.zip)|*.dll;*.zip" };
            if (d.ShowDialog(this) != true) return;
            if (PluginHost.I.Import(d.FileName) != null) { _restartNeeded = true; Say("PLUGIN ADDED  //  RESTART TO LOAD IT"); BuildSettings(); }
        }

        private void Restart()
        {
            try { Process.Start(new ProcessStartInfo(Environment.ProcessPath, "--restart") { UseShellExecute = false }); } catch (Exception ex) { Log.Write("restart: " + ex.Message, show: true); return; }
            _exiting = true;
            Application.Current.Shutdown();
        }
    }
}
