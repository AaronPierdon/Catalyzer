using System;
using System.Collections.Generic;
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
    /// Catalyzer's own dialog (never a stock one): a glass panel that rises over a dimmed board, a decoding title, a
    /// body the caller fills, and chamfered buttons. Used by the welcome, NEW PROJECT, publish and cleanup.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Grid _dialogHost = new Grid { Visibility = Visibility.Collapsed };

        public sealed class DialogParts
        {
            public StackPanel Body;
            public StackPanel Buttons;
            public TextBlock Title;
            /// <summary>Close the dialog (slides away).</summary>
            public Action Close;
        }

        private bool DialogOpen => _dialogHost.Visibility == Visibility.Visible;

        /// <summary>--snapshot: 1 = the welcome, 2 = NEW PROJECT.</summary>
        internal void SnapshotDialogs(int which)
        {
            CloseNotes(); CloseFeed(); ClosePrompt();
            if (which == 1) Welcome(); else NewProject();
        }
        private Action _dialogEsc;

        /// <summary>Open a dialog. <paramref name="onEsc"/> runs for Esc or a click on the veil (null = they do nothing).</summary>
        private DialogParts Dialog(string title, double width = 620, Action onEsc = null)
        {
            _dialogHost.Children.Clear();
            var veil = new Rectangle { Fill = Brushes.Black, Opacity = 0 };
            _dialogHost.Children.Add(veil);
            var panel = new Grid { Width = width, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxHeight = Math.Max(300, ActualHeight - 80) };
            var lift = new TranslateTransform(); panel.RenderTransform = lift;
            panel.Children.Add(new Glass { Cut = 14, Solid = true });
            Cz.Glow(panel, Cz.T.Accent, 30, 0.35);
            var outer = new Grid { Margin = new Thickness(28, 24, 28, 22) };
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var head = Cz.Head("", 19, "CzTextBrush");
            Decode.Play(head, Cz.T.HeadCaps ? title.ToUpperInvariant() : title, 0.08, 0.45);
            outer.Children.Add(head);
            var body = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false };
            Grid.SetRow(scroll, 1); outer.Children.Add(scroll);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            Grid.SetRow(buttons, 2); outer.Children.Add(buttons);
            panel.Children.Add(outer);
            _dialogHost.Children.Add(panel);
            _dialogHost.Visibility = Visibility.Visible;

            var parts = new DialogParts { Body = body, Buttons = buttons, Title = head };
            parts.Close = () =>
            {
                _dialogEsc = null;
                if (Cz.Still) { _dialogHost.Visibility = Visibility.Collapsed; return; }
                var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160));
                a.Completed += (s, e) => { _dialogHost.Visibility = Visibility.Collapsed; _dialogHost.BeginAnimation(OpacityProperty, null); _dialogHost.Opacity = 1; };
                _dialogHost.BeginAnimation(OpacityProperty, a);
            };
            _dialogEsc = onEsc;
            veil.MouseLeftButtonDown += (s, e) => _dialogEsc?.Invoke();

            if (Cz.Still) { veil.Opacity = 0.55; return parts; }
            veil.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.55, TimeSpan.FromMilliseconds(220)));
            panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            return parts;
        }

        private static TextBlock Para(string text, double size = 13.5, string brush = "CzTextDimBrush")
        {
            var t = Cz.Text(text, size, brush); t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new Thickness(0, 0, 0, 10);
            return t;
        }

        private static CzButton Btn(string label, Action click, string glyph = null, bool primary = false, string tip = null)
            => new CzButton(label, click, glyph, primary: primary, height: 34, tip: tip) { Margin = new Thickness(8, 0, 0, 0) };

        /// <summary>A text field in Catalyzer's look: a chamfered frame, the box floating inside, a faint hint.</summary>
        private static (Grid row, TextBox box) Field(string hint, string value = "", double height = 40)
        {
            var row = new Grid { Height = height, Margin = new Thickness(0, 0, 0, 10) };
            var frame = new System.Windows.Shapes.Path { StrokeThickness = 1 };
            frame.SetResourceReference(Shape.StrokeProperty, "CzLineBrush");
            row.Children.Add(Cz.Under(frame));
            row.SizeChanged += (s, e) => frame.Data = Cz.Chamfer(Math.Max(4, row.ActualWidth), Math.Max(4, row.ActualHeight), 7);
            var box = new TextBox { Text = value ?? "", Margin = new Thickness(14, 0, 12, 0), VerticalContentAlignment = VerticalAlignment.Center };
            Field(box, 13.5, "CzFontUi");
            var h = Cz.Text(hint, 13, "CzTextFaintBrush"); h.Margin = new Thickness(16, 0, 0, 0); h.VerticalAlignment = VerticalAlignment.Center; h.IsHitTestVisible = false;
            h.Visibility = box.Text == "" ? Visibility.Visible : Visibility.Collapsed;
            box.TextChanged += (s, e) => h.Visibility = box.Text == "" ? Visibility.Visible : Visibility.Collapsed;
            box.GotKeyboardFocus += (s, e) => frame.SetResourceReference(Shape.StrokeProperty, "CzSelectBrush");
            box.LostKeyboardFocus += (s, e) => frame.SetResourceReference(Shape.StrokeProperty, "CzLineBrush");
            row.Children.Add(h); row.Children.Add(box);
            return (row, box);
        }

        /// <summary>A labelled line: small mono caption over a value.</summary>
        private static FrameworkElement Caption(string label) => Cz.Mono(label.ToUpperInvariant(), 10, "CzTextDimBrush").Also(t => t.Margin = new Thickness(0, 6, 0, 6));

        /// <summary>A progress line in a dialog: a light and a text, updated as steps finish.</summary>
        private static (StackPanel row, Action<string, Tone, bool> set) Step(string text)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            var led = new Led(Cz.T.TextFaint, 8) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 10, 0) };
            var t = Cz.Mono(text, 11, "CzTextDimBrush");
            row.Children.Add(led); row.Children.Add(t);
            return (row, (s, tone, pulse) => { t.Text = s; t.SetResourceReference(TextBlock.ForegroundProperty, Cz.ToneBrush(tone)); led.Color = Cz.ToneColor(tone); led.Pulse = pulse; led.InvalidateVisual(); });
        }
    }
}
