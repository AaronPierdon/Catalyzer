using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// The feed drawer (API 2): a plugin's live log on glass, sliding in from the right. Mono lines coloured by tone,
    /// new lines fade in, it follows the end unless you scroll up. One feed at a time; Esc or the veil closes it.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Grid _feedHost = new Grid { Visibility = Visibility.Collapsed };
        private readonly StackPanel _feedLines = new StackPanel();
        private readonly ScrollViewer _feedScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        private readonly TranslateTransform _feedX = new TranslateTransform();
        private readonly StackPanel _feedButtons = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly Chip _feedState = new Chip(24) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        private TextBlock _feedTitle;
        private IFeed _feed;
        private int _feedShown;
        private DispatcherTimer _feedSoon;
        private const double FeedWidth = 720;

        private bool FeedOpen => _feedHost.Visibility == Visibility.Visible && _feed != null;

        /// <summary>--snapshot: the feed drawer with a typical build in it (render-only sample lines).</summary>
        internal void FeedForSnapshot() { ClosePrompt(); OpenFeed(new SampleFeed()); }

        private sealed class SampleFeed : IFeed
        {
            public string Title => "Catalyzer  //  Link";
            public BadgeState State => new BadgeState { Text = "LINKED  ·  RUNNING", Tone = Tone.Select, Pulse = true };
            public event Action Changed { add { } remove { } }
            public IEnumerable<MenuEntry> Actions() => new[] { new MenuEntry { Text = "Unlink", Glyph = "\uE71B" }, new MenuEntry { Text = "Cancel", Glyph = "\uE71A" }, new MenuEntry { Text = "Mailbox", Glyph = "\uE8B7" }, new MenuEntry { Text = "Clear", Glyph = "\uE894" } };
            public IReadOnlyList<FeedLine> Lines() => new List<FeedLine>
            {
                new FeedLine { Text = "LINKED  ·  linked from the card  ·  C:\\Users\\Aaron\\source\\repos\\Catalyzer", Tone = Tone.Accent },
                new FeedLine { Text = "▸ 20261122-161204-8812  git status --short", Tone = Tone.Select },
                new FeedLine { Text = " M src/Catalyzer/HubWindow.cs", Tone = Tone.Dim },
                new FeedLine { Text = "■ 20261122-161204-8812  exit 0  ·  0.6 s", Tone = Tone.Ok },
                new FeedLine { Text = "▸ 20261122-161230-3127  & \".\\tools\\snap.ps1\"", Tone = Tone.Select },
                new FeedLine { Text = "BUILD OK", Tone = Tone.Dim },
                new FeedLine { Text = "! git : To https://github.com/AaronPierdon/Catalyzer.git", Tone = Tone.Warn },
                new FeedLine { Text = "snap catalyzer-nexus.png 481737", Tone = Tone.Dim },
                new FeedLine { Text = "■ 20261122-161230-3127  exit 1  ·  41.2 s", Tone = Tone.Danger },
            };
        }

        private void InitFeed()
        {
            if (PluginHost.I != null) PluginHost.I.FeedRequested += OpenFeed;
        }

        private void BuildFeedShell()
        {
            var veil = new Rectangle { Fill = Brushes.Black, Opacity = 0.45 };
            veil.MouseLeftButtonDown += (s, e) => CloseFeed();
            _feedHost.Children.Add(veil);
            var drawer = new Grid { Width = FeedWidth, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 10, 10), RenderTransform = _feedX };
            drawer.Children.Add(new Glass { Cut = 16, Solid = true });
            var body = new Grid { Margin = new Thickness(24, 20, 18, 18) };
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition());
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _feedTitle = Cz.Head("", 20, "CzTextBrush"); _feedTitle.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(_feedTitle);
            Grid.SetColumn(_feedState, 1); _feedState.HorizontalAlignment = HorizontalAlignment.Left; top.Children.Add(_feedState);
            var x = new CzIconButton("", CloseFeed, "CLOSE  //  ESC"); Grid.SetColumn(x, 2); top.Children.Add(x);
            body.Children.Add(top);
            _feedButtons.Margin = new Thickness(0, 12, 0, 10);
            Grid.SetRow(_feedButtons, 1); body.Children.Add(_feedButtons);
            _feedLines.Margin = new Thickness(0, 0, 8, 0);
            _feedScroll.Content = _feedLines;
            var rule = new Rectangle { Height = 1, VerticalAlignment = VerticalAlignment.Top };
            rule.SetResourceReference(Shape.FillProperty, "CzLineBrush");
            var area = new Grid(); area.Children.Add(rule); _feedScroll.Margin = new Thickness(0, 8, 0, 0); area.Children.Add(_feedScroll);
            Grid.SetRow(area, 2); body.Children.Add(area);
            drawer.Children.Add(body);
            Cz.Glow(drawer, Cz.T.Accent, 30, 0.35);
            _feedHost.Children.Add(drawer);
        }

        private void OpenFeed(IFeed f)
        {
            if (_feedHost.Children.Count == 0) BuildFeedShell();
            if (_drawerOpen) Drawer(false);
            if (_feed != null) _feed.Changed -= FeedChanged;
            _feed = f; _feedShown = 0; _feedLines.Children.Clear();
            f.Changed += FeedChanged;
            Decode.Play(_feedTitle, (Cz.T.HeadCaps ? (f.Title ?? "").ToUpperInvariant() : f.Title ?? ""), 0.05, 0.4);
            FeedRefresh(animate: false);
            _feedHost.Visibility = Visibility.Visible;
            if (Cz.Still) { _feedX.X = 0; return; }
            _feedX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(FeedWidth + 20, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private void CloseFeed()
        {
            if (_feed != null) _feed.Changed -= FeedChanged;
            _feed = null;
            if (_feedHost.Visibility != Visibility.Visible) return;
            if (Cz.Still) { _feedHost.Visibility = Visibility.Collapsed; return; }
            var a = new DoubleAnimation(0, FeedWidth + 20, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            a.Completed += (s, e) => { if (_feed == null) _feedHost.Visibility = Visibility.Collapsed; };
            _feedX.BeginAnimation(TranslateTransform.XProperty, a);
        }

        /// <summary>Changes arrive in bursts (a build prints hundreds of lines): redraw at most ~8 times a second.</summary>
        private void FeedChanged() => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_feedSoon == null)
            {
                _feedSoon = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                _feedSoon.Tick += (s, e) => { _feedSoon.Stop(); if (FeedOpen) FeedRefresh(animate: true); };
            }
            if (!_feedSoon.IsEnabled) _feedSoon.Start();
        }));

        private void FeedRefresh(bool animate)
        {
            var f = _feed; if (f == null) return;
            BadgeState st = null; IReadOnlyList<FeedLine> lines; List<MenuEntry> acts;
            try { st = f.State; lines = f.Lines() ?? new List<FeedLine>(); acts = (f.Actions() ?? Enumerable.Empty<MenuEntry>()).Where(a => a != null && !a.Separator).ToList(); }
            catch (Exception ex) { Log.Write("feed: " + ex.Message); return; }
            if (st != null) { _feedState.Visibility = Visibility.Visible; _feedState.Set(st.Text ?? "", st.Tone, st.Pulse, st.Tip); } else _feedState.Visibility = Visibility.Collapsed;

            _feedButtons.Children.Clear();
            foreach (var a in acts)
            {
                var act = a;
                _feedButtons.Children.Add(new CzButton(a.Text.ToUpperInvariant(), () => { try { act.Run?.Invoke(); } catch (Exception ex) { Log.Write(act.Text + ": " + ex.Message, show: true); } }, a.Glyph, height: 28, tip: a.Hint?.ToUpperInvariant()) { Margin = new Thickness(0, 0, 8, 0) });
            }

            bool follow = _feedScroll.VerticalOffset >= _feedScroll.ScrollableHeight - 24;
            if (lines.Count < _feedShown) { _feedLines.Children.Clear(); _feedShown = 0; }   // the feed was trimmed or cleared
            if (_feedShown == 0 && _feedLines.Children.Count == 1 && (_feedLines.Children[0] as FrameworkElement)?.Tag as string == "empty") _feedLines.Children.Clear();
            if (lines.Count - _feedShown > 400) { _feedLines.Children.Clear(); _feedShown = lines.Count - 400; }   // don't build thousands of rows at once
            for (int i = _feedShown; i < lines.Count; i++)
            {
                var l = lines[i];
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) }); row.ColumnDefinitions.Add(new ColumnDefinition());
                var t = Cz.Mono(l.When.ToString("HH:mm:ss"), 10, "CzTextFaintBrush"); t.VerticalAlignment = VerticalAlignment.Top; t.Margin = new Thickness(0, 1, 0, 0);
                row.Children.Add(t);
                var tx = Cz.Mono(l.Text ?? "", 11.5, Cz.ToneBrush(l.Tone)); tx.TextWrapping = TextWrapping.Wrap; tx.TextTrimming = TextTrimming.None;
                Grid.SetColumn(tx, 1); row.Children.Add(tx);
                if (animate && !Cz.Still) row.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
                _feedLines.Children.Add(row);
            }
            _feedShown = lines.Count;
            if (_feedLines.Children.Count == 0) _feedLines.Children.Add(Cz.Mono("NOTHING YET", 11, "CzTextFaintBrush").Also(t => { t.Margin = new Thickness(64, 6, 0, 0); t.Tag = "empty"; }));
            while (_feedLines.Children.Count > 1500) _feedLines.Children.RemoveAt(0);
            if (follow || !animate) _feedScroll.ScrollToEnd();
        }
    }
}
