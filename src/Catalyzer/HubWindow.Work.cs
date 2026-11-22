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
    /// Background work (plugins' helpers): the header chip "n HELPERS", the close prompt, and the tray.
    ///   ✕ with nothing running exits as always.
    ///   ✕ with work running does what you chose ("ask" by default): STOP ALL &amp; EXIT, or KEEP IN TRAY.
    ///   From the tray: open, stop one, stop all, or exit (which stops all) - each one click.
    /// Helpers live in a kill-on-close job (HelpersPlugin), so if Catalyzer dies they die with it.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Chip _workChip = new Chip(32) { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly Grid _promptHost = new Grid { Visibility = Visibility.Collapsed };
        private System.Windows.Forms.NotifyIcon _tray;
        private bool _exiting;

        private int Running => PluginHost.I?.Running ?? 0;
        private List<IBackgroundWork> Work => PluginHost.I?.Work.ToList() ?? new List<IBackgroundWork>();

        /// <summary>What's running, e.g. "2 HELPERS" or "2 HELPERS · 1 WATCHER".</summary>
        private string WorkText()
        {
            var parts = Work.Select(w => { int n = 0; try { n = w.Running; } catch { } return (n, w.Name ?? "TASKS"); }).Where(x => x.n > 0)
                            .Select(x => x.n + " " + (x.n == 1 && x.Item2.EndsWith("S") ? x.Item2.Substring(0, x.Item2.Length - 1) : x.Item2));
            return string.Join("  ·  ", parts);
        }

        private void InitWork()
        {
            _workChip.Click = c => WorkMenu(c, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            if (PluginHost.I != null)
            {
                PluginHost.I.WorkChanged += WorkChanged;
                PluginHost.I.BadgesChanged += () => { foreach (var c in _cardOf.Values) c.Badges(); };
            }
            Application.Current.SessionEnding += (s, e) => _exiting = true;
            Application.Current.Exit += (s, e) => { try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { } };
            Closing += (s, e) =>
            {
                if (_exiting || Running == 0) return;
                e.Cancel = true;
                switch (Prefs.Value.CloseWithWork)
                {
                    case "stop": Exit(); break;
                    case "tray": ToTray(); break;
                    default: FromTray(); Prompt(Running, WorkText(), Work.SelectMany(w => { try { return w.Items() ?? Enumerable.Empty<MenuEntry>(); } catch { return Enumerable.Empty<MenuEntry>(); } }).Where(m => !m.Separator).Select(m => m.Text).ToList()); break;
                }
            };
            WorkChanged();
        }

        private void WorkChanged()
        {
            int n = Running;
            if (n > 0)
            {
                _workChip.Set(WorkText(), Tone.Ok, true, "RUNNING HIDDEN  //  CLICK TO SEE OR STOP THEM");
                if (_workChip.Visibility != Visibility.Visible)
                {
                    _workChip.Visibility = Visibility.Visible;
                    if (!Cz.Still) _workChip.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
                }
            }
            else _workChip.Visibility = Visibility.Collapsed;
            if (_tray != null) _tray.Text = Tip();
        }

        private string Tip() { var t = "Catalyzer" + (Running > 0 ? "  -  " + WorkText().ToLowerInvariant() + " running" : ""); return t.Length > 63 ? t.Substring(0, 63) : t; }

        /// <summary>Everything running, each with its own stop, then STOP ALL (header chip and tray).</summary>
        private List<CzMenu.Item> WorkItems(bool tray)
        {
            var items = new List<CzMenu.Item>();
            if (tray) { items.Add(new CzMenu.Item { Text = "Open Catalyzer", Glyph = "", Click = FromTray }); items.Add(new CzMenu.Item { Separator = true }); }
            foreach (var w in Work)
            {
                try { if (w.Running > 0) items.AddRange(CzMenu.From(w.Items(), w.Name)); } catch (Exception ex) { Log.Write("work " + w.Name + ": " + ex.Message); }
            }
            if (Running > 0) items.Add(new CzMenu.Item { Text = "Stop all", Glyph = "", Hint = WorkText().ToLowerInvariant(), Danger = true, Click = () => { PluginHost.I?.StopAllWork(); Say("STOPPED ALL"); } });
            if (tray) { items.Add(new CzMenu.Item { Separator = true }); items.Add(new CzMenu.Item { Text = "Exit Catalyzer", Glyph = "", Hint = Running > 0 ? "stops everything" : null, Click = Exit }); }
            return items;
        }

        private void WorkMenu(UIElement anchor, System.Windows.Controls.Primitives.PlacementMode at)
        {
            var items = WorkItems(false);
            if (items.Count > 0) new CzMenu { Placement = at }.Show(anchor, "RUNNING HIDDEN", items);
        }

        /// <summary>Stop everything and close.</summary>
        private void Exit()
        {
            _exiting = true;
            PluginHost.I?.StopAllWork();
            if (!IsVisible) { Application.Current.Shutdown(); return; }
            Close();
        }

        // ================================================================== the close prompt

        private void Prompt(int count, string what, List<string> lines)
        {
            if (_promptHost.Children.Count > 0 && _promptHost.Visibility == Visibility.Visible) return;
            _promptHost.Children.Clear();
            var veil = new Rectangle { Fill = Brushes.Black, Opacity = 0 };
            veil.MouseLeftButtonDown += (s, e) => ClosePrompt();
            _promptHost.Children.Add(veil);

            var panel = new Grid { Width = 520, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var lift = new TranslateTransform(); panel.RenderTransform = lift;
            panel.Children.Add(new Glass { Cut = 14, Solid = true });
            Cz.Glow(panel, Cz.T.Accent, 30, 0.35);
            var b = new StackPanel { Margin = new Thickness(28, 24, 28, 22) };
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(new Led(Cz.T.Ok, 10) { Pulse = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            var head = Cz.Head("", 19, "CzTextBrush"); top.Children.Add(head);
            Decode.Play(head, (what == "" ? count + " RUNNING" : what + " RUNNING").ToUpperInvariant(), 0.08, 0.45);
            b.Children.Add(top);
            var note = Cz.Text("They run hidden, inside Catalyzer. Closing stops them - or keep them running and Catalyzer waits in the tray.", 13, "CzTextDimBrush");
            note.TextWrapping = TextWrapping.Wrap; note.TextTrimming = TextTrimming.None; note.Margin = new Thickness(0, 10, 0, 10);
            b.Children.Add(note);
            foreach (var l in lines.Take(5))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 2, 0, 2) };
                var dia = new Rectangle { Width = 5, Height = 5, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(45), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                dia.SetResourceReference(Shape.FillProperty, "CzOkBrush");
                row.Children.Add(dia); row.Children.Add(Cz.Mono(l.ToUpperInvariant(), 10.5, "CzTextBrush"));
                b.Children.Add(row);
            }
            if (lines.Count > 5) b.Children.Add(Cz.Mono("   +" + (lines.Count - 5) + " MORE", 10, "CzTextFaintBrush"));

            bool remember = false;
            var bottom = new Grid { Margin = new Thickness(0, 20, 0, 0) };
            bottom.ColumnDefinitions.Add(new ColumnDefinition()); bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var rem = new CzToggle(false, on => remember = on, "Remember") { VerticalAlignment = VerticalAlignment.Center, ToolTip = "CHANGE IT LATER IN SETTINGS  //  GENERAL" };
            bottom.Children.Add(rem);
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            btns.Children.Add(new CzButton("CANCEL", ClosePrompt, null, height: 34, tip: "ESC") { Margin = new Thickness(0, 0, 8, 0) });
            btns.Children.Add(new CzButton("STOP ALL & EXIT", () => { if (remember) Prefs.Save(p => p.CloseWithWork = "stop"); ClosePrompt(); Exit(); }, "", height: 34) { Margin = new Thickness(0, 0, 8, 0) });
            btns.Children.Add(new CzButton("KEEP IN TRAY", () => { if (remember) Prefs.Save(p => p.CloseWithWork = "tray"); ClosePrompt(); ToTray(); }, "", primary: true, height: 34));
            Grid.SetColumn(btns, 1); bottom.Children.Add(btns);
            b.Children.Add(bottom);
            panel.Children.Add(b);
            _promptHost.Children.Add(panel);
            _promptHost.Visibility = Visibility.Visible;

            if (Cz.Still) { veil.Opacity = 0.55; return; }
            veil.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.55, TimeSpan.FromMilliseconds(220)));
            panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private bool PromptOpen => _promptHost.Visibility == Visibility.Visible;

        private void ClosePrompt()
        {
            if (!PromptOpen) return;
            if (Cz.Still) { _promptHost.Visibility = Visibility.Collapsed; return; }
            var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
            a.Completed += (s, e) => { _promptHost.Visibility = Visibility.Collapsed; _promptHost.BeginAnimation(OpacityProperty, null); _promptHost.Opacity = 1; };
            _promptHost.BeginAnimation(OpacityProperty, a);
        }

        /// <summary>--snapshot: the close prompt as it looks with two helpers running.</summary>
        internal void PromptForSnapshot() { Drawer(false); Prompt(2, "2 HELPERS", new List<string> { "Nexus  //  build agent", "Catalyzer  //  watch" }); }

        // ================================================================== the tray

        private void ToTray()
        {
            if (_tray == null)
            {
                _tray = new System.Windows.Forms.NotifyIcon();
                try
                {
                    var s = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/catalyzer.ico"))?.Stream;
                    if (s != null) using (s) _tray.Icon = new System.Drawing.Icon(s, System.Windows.Forms.SystemInformation.SmallIconSize);
                }
                catch (Exception ex) { Log.Write("tray icon: " + ex.Message); }
                _tray.MouseUp += (s, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left) FromTray();
                    else if (e.Button == System.Windows.Forms.MouseButtons.Right) TrayMenu();
                };
            }
            _tray.Text = Tip();
            _tray.Visible = true;
            Hide();
            Log.Write("to the tray: " + WorkText());
        }

        /// <summary>Back from the tray (tray click, or Catalyzer started again).</summary>
        internal void FromTray()
        {
            if (_tray != null) _tray.Visible = false;
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = Prefs.Value.Maximized ? WindowState.Maximized : WindowState.Normal;
            Activate();
        }

        /// <summary>The tray's menu is a Catalyzer menu (glass, never native), anchored on a tiny invisible window at the pointer.</summary>
        private void TrayMenu()
        {
            var pt = System.Windows.Forms.Control.MousePosition;
            var anchor = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false, Topmost = true,
                Width = 1, Height = 1, Left = pt.X, Top = pt.Y, ShowActivated = true, ResizeMode = ResizeMode.NoResize,
            };
            anchor.SetResourceReference(FontFamilyProperty, "CzFontUi");
            anchor.Show(); anchor.Activate();
            var m = new CzMenu();
            m.Closed += () => Dispatcher.BeginInvoke(new Action(anchor.Close));
            m.Show(anchor, "CATALYZER", WorkItems(true));
        }
    }
}
