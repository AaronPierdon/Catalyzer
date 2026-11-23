using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// NOTES: each card's docs/NOTES.md (Nexus Development Principles 2.0.0), the notepad Aaron and every chat share.
    /// The card's NOTES chip shows what's open (and what's new in the Inbox); clicking it slides in the notes drawer:
    /// type a note + Enter (it lands in the Inbox), tick to move it to Done, right-click to move it to another section,
    /// RAW to edit the whole file, SYNC to commit + pull + push just NOTES.md, so notes written from the phone arrive
    /// and yours leave. Changes on disk (a chat editing the file) show up live.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Grid _notesHost = new Grid { Visibility = Visibility.Collapsed };
        private readonly TranslateTransform _notesX = new TranslateTransform();
        private readonly StackPanel _notesList = new StackPanel();
        private readonly ScrollViewer _notesScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        private readonly StackPanel _notesButtons = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly Chip _notesChip = new Chip(24) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        private readonly TextBox _noteInput = new TextBox();
        private readonly TextBox _noteRaw = new TextBox();
        private readonly Grid _noteInputRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        private TextBlock _notesTitle;
        private Project _notesFor;
        private Notes _notes;
        private bool _raw, _syncing;
        private const double NotesWidth = 640;
        private readonly Dictionary<string, (DateTime when, int open, int inbox)> _noteCounts = new Dictionary<string, (DateTime, int, int)>(StringComparer.OrdinalIgnoreCase);
        private readonly List<FileSystemWatcher> _noteWatch = new List<FileSystemWatcher>();
        private DispatcherTimer _notesSoon;

        private bool NotesOpen => _notesHost.Visibility == Visibility.Visible && _notesFor != null;

        private void InitNotes()
        {
            PluginHost.I?.AddBadge(new ProjectBadge
            {
                Id = "notes", Order = -10,
                State = NotesBadge,
                Click = OpenNotes,
                Menu = p => new[]
                {
                    new MenuEntry { Text = "Open notes", Glyph = "", Run = () => OpenNotes(p) },
                    new MenuEntry { Text = "Open NOTES.md in the editor", Glyph = "", Hint = "docs\\NOTES.md", Run = () => { var n = new Notes(p.Folder); if (n.Exists) Launch.Shell(n.Path, null); else OpenNotes(p); } },
                },
            });
            _board.Changed += WatchNotes;
        }

        // ================================================================== the chip

        private BadgeState NotesBadge(Project p)
        {
            var n = new Notes(p.Folder);
            if (!n.Exists)
                return n.Legacy
                    ? new BadgeState { Text = "NOTES  ·  BACKLOG", Tone = Tone.Warn, Tip = "THIS REPO STILL USES BACKLOG.MD (PRINCIPLES < 2.0.0)\nASK ITS CHAT: UPDATE TO THE LATEST PRINCIPLES" }
                    : new BadgeState { Text = "NOTES  +", Tone = Tone.Faint, Tip = "NO DOCS\\NOTES.MD YET  //  CLICK TO START ONE" };
            int open = 0, inbox = 0;
            try
            {
                var when = File.GetLastWriteTimeUtc(n.Path);
                if (_noteCounts.TryGetValue(p.Folder, out var c) && c.when == when) { open = c.open; inbox = c.inbox; }
                else { n.Load(); (open, inbox) = n.Count(); _noteCounts[p.Folder] = (when, open, inbox); }
            }
            catch { }
            return inbox > 0
                ? new BadgeState { Text = "NOTES " + open + "  ·  " + inbox + " NEW", Tone = Tone.Accent, Tip = inbox + " IN THE INBOX, " + open + " OPEN  //  CLICK TO OPEN" }
                : new BadgeState { Text = "NOTES " + open, Tone = Tone.Dim, Tip = open + " OPEN  //  CLICK TO OPEN" };
        }

        /// <summary>Each project's docs folder, for NOTES.md only: a chat or an editor changing it updates the chip and the drawer.</summary>
        private void WatchNotes()
        {
            if (Prefs.Offline) return;
            foreach (var w in _noteWatch) { try { w.EnableRaisingEvents = false; w.Dispose(); } catch { } }
            _noteWatch.Clear();
            foreach (var p in _board.Items)
            {
                var docs = System.IO.Path.Combine(p.Folder, "docs");
                if (!Directory.Exists(docs)) continue;
                try
                {
                    var w = new FileSystemWatcher(docs, "NOTES.md") { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
                    var folder = p.Folder;
                    FileSystemEventHandler on = (s, e) => Dispatcher.BeginInvoke(new Action(() => NotesTouched(folder)));
                    w.Changed += on; w.Created += on; w.Deleted += on; w.Renamed += (s, e) => Dispatcher.BeginInvoke(new Action(() => NotesTouched(folder)));
                    w.EnableRaisingEvents = true;
                    _noteWatch.Add(w);
                }
                catch (Exception ex) { Log.Write("watch notes " + docs + ": " + ex.Message); }
            }
        }

        private void NotesTouched(string folder)
        {
            if (_notesSoon == null)
            {
                _notesSoon = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _notesSoon.Tick += (s, e) => { _notesSoon.Stop(); PluginHost.I?.UpdateBadges(); if (NotesOpen && !_raw) NotesRefresh(true); };
            }
            _notesSoon.Stop(); _notesSoon.Start();
        }

        // ================================================================== the drawer

        private void BuildNotesShell()
        {
            var veil = new Rectangle { Fill = Brushes.Black, Opacity = 0.45 };
            veil.MouseLeftButtonDown += (s, e) => CloseNotes();
            _notesHost.Children.Add(veil);
            var drawer = new Grid { Width = NotesWidth, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 10, 10), RenderTransform = _notesX };
            drawer.Children.Add(new Glass { Cut = 16, Solid = true });
            var body = new Grid { Margin = new Thickness(24, 20, 18, 18) };
            for (int i = 0; i < 3; i++) body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.RowDefinitions.Add(new RowDefinition());

            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _notesTitle = Cz.Head("", 20, "CzTextBrush"); _notesTitle.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(_notesTitle);
            Grid.SetColumn(_notesChip, 1); _notesChip.HorizontalAlignment = HorizontalAlignment.Left; top.Children.Add(_notesChip);
            var x = new CzIconButton("", CloseNotes, "CLOSE  //  ESC"); Grid.SetColumn(x, 2); top.Children.Add(x);
            body.Children.Add(top);

            _notesButtons.Margin = new Thickness(0, 12, 0, 12);
            Grid.SetRow(_notesButtons, 1); body.Children.Add(_notesButtons);

            // the add-a-note field: a chamfered frame with the TextBox floating inside
            var frame = new System.Windows.Shapes.Path { StrokeThickness = 1 };
            frame.SetResourceReference(Shape.StrokeProperty, "CzLineBrush");
            _noteInputRow.Height = 40;
            _noteInputRow.Children.Add(Cz.Under(frame));
            _noteInputRow.SizeChanged += (s, e) => frame.Data = Cz.Chamfer(Math.Max(4, _noteInputRow.ActualWidth), Math.Max(4, _noteInputRow.ActualHeight), 7);
            var plus = Cz.Text("", 13, "CzAccentBrush", "CzFontIcons"); plus.Margin = new Thickness(14, 0, 0, 0); plus.VerticalAlignment = VerticalAlignment.Center; plus.HorizontalAlignment = HorizontalAlignment.Left;
            _noteInputRow.Children.Add(plus);
            Field(_noteInput, 13.5, "CzFontUi");
            _noteInput.Margin = new Thickness(40, 0, 12, 0); _noteInput.VerticalContentAlignment = VerticalAlignment.Center;
            _noteInput.ToolTip = "A NEW NOTE  //  ENTER ADDS IT TO THE INBOX";
            _noteInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) { AddNote(); e.Handled = true; } };
            var hint = Cz.Text("Note this...  (Enter)", 13, "CzTextFaintBrush"); hint.Margin = new Thickness(42, 0, 0, 0); hint.VerticalAlignment = VerticalAlignment.Center; hint.IsHitTestVisible = false;
            _noteInput.TextChanged += (s, e) => hint.Visibility = _noteInput.Text == "" ? Visibility.Visible : Visibility.Collapsed;
            _noteInputRow.Children.Add(hint);
            _noteInputRow.Children.Add(_noteInput);
            Grid.SetRow(_noteInputRow, 2); body.Children.Add(_noteInputRow);

            var area = new Grid();
            _notesScroll.Content = _notesList; _notesList.Margin = new Thickness(0, 0, 8, 0);
            area.Children.Add(_notesScroll);
            Field(_noteRaw, 12, "CzFontMono");
            _noteRaw.AcceptsReturn = true; _noteRaw.AcceptsTab = true; _noteRaw.TextWrapping = TextWrapping.Wrap;
            _noteRaw.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; _noteRaw.Visibility = Visibility.Collapsed;
            _noteRaw.VerticalContentAlignment = VerticalAlignment.Top;
            area.Children.Add(_noteRaw);
            Grid.SetRow(area, 3); body.Children.Add(area);
            drawer.Children.Add(body);
            Cz.Glow(drawer, Cz.T.Accent, 30, 0.35);
            _notesHost.Children.Add(drawer);
        }

        private static void Field(TextBox t, double size, string font)
        {
            t.Background = Brushes.Transparent; t.BorderThickness = new Thickness(0); t.FontSize = size; t.FocusVisualStyle = null;
            t.SetResourceReference(ForegroundProperty, "CzTextBrush");
            t.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, "CzSelectBrush");
            t.SetResourceReference(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty, "CzSelectBrush");
            t.SetResourceReference(FontFamilyProperty, font);
        }

        private void OpenNotes(Project p)
        {
            if (_notesHost.Children.Count == 0) BuildNotesShell();
            if (_drawerOpen) Drawer(false);
            if (FeedOpen) CloseFeed();
            _notesFor = p; _notes = new Notes(p.Folder); _raw = false;
            Decode.Play(_notesTitle, (p.Name + "  //  NOTES").ToUpperInvariant(), 0.05, 0.4);
            NotesRefresh(false);
            _notesHost.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(() => { if (_notes.Exists) _noteInput.Focus(); }), DispatcherPriority.Input);
            if (Cz.Still) { _notesX.X = 0; return; }
            _notesX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(NotesWidth + 20, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private void CloseNotes()
        {
            if (_raw && !ConfirmLeaveRaw()) return;
            _notesFor = null;
            if (_notesHost.Visibility != Visibility.Visible) return;
            if (Cz.Still) { _notesHost.Visibility = Visibility.Collapsed; return; }
            var a = new DoubleAnimation(0, NotesWidth + 20, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            a.Completed += (s, e) => { if (_notesFor == null) _notesHost.Visibility = Visibility.Collapsed; };
            _notesX.BeginAnimation(TranslateTransform.XProperty, a);
        }

        /// <summary>RAW with unsaved edits: closing keeps them by saving (never throws your typing away).</summary>
        private bool ConfirmLeaveRaw()
        {
            if (_notes != null && _noteRaw.Text != _notes.Raw) { try { _notes.SaveRaw(_noteRaw.Text); Say("NOTES SAVED"); } catch (Exception ex) { Log.Write("notes: " + ex.Message, show: true); return false; } }
            _raw = false; return true;
        }

        private void NotesRefresh(bool animate)
        {
            var n = _notes; var p = _notesFor; if (n == null || p == null) return;
            try { n.Load(); } catch (Exception ex) { Log.Write("notes " + n.Path + ": " + ex.Message, show: true); }
            var st = NotesBadge(p);
            _notesChip.Set(st.Text, st.Tone, _syncing, n.Exists ? "DOCS\\NOTES.MD" : null);

            _notesButtons.Children.Clear();
            if (n.Exists)
            {
                _notesButtons.Children.Add(new CzButton(_syncing ? "SYNCING..." : "SYNC", SyncNotes, "", primary: true, height: 28, tip: "COMMIT NOTES.MD, PULL, PUSH  //  NOTES FROM YOUR PHONE ARRIVE, YOURS LEAVE") { Margin = new Thickness(0, 0, 8, 0) });
                _notesButtons.Children.Add(new CzButton(_raw ? "LIST" : "RAW", ToggleRaw, _raw ? "" : "", height: 28, tip: _raw ? "BACK TO THE LIST (SAVES)" : "EDIT THE WHOLE FILE") { Margin = new Thickness(0, 0, 8, 0) });
                _notesButtons.Children.Add(new CzButton("EDITOR", () => Launch.Shell(n.Path, null), "", height: 28, tip: "OPEN NOTES.MD IN YOUR EDITOR"));
            }
            _noteInputRow.Visibility = n.Exists && !_raw ? Visibility.Visible : Visibility.Collapsed;
            _noteRaw.Visibility = _raw ? Visibility.Visible : Visibility.Collapsed;
            _notesScroll.Visibility = _raw ? Visibility.Collapsed : Visibility.Visible;
            if (_raw) return;

            double keep = _notesScroll.VerticalOffset;
            _notesList.Children.Clear();
            if (!n.Exists)
            {
                var msg = Cz.Text(n.Legacy
                    ? "This repo still keeps its list in docs\\BACKLOG.md. The Nexus Development Principles 2.0.0 merge it into docs\\NOTES.md. Ask the project's chat to \"update to the latest principles\", or start a fresh NOTES.md here (BACKLOG.md stays as it is)."
                    : "No docs\\NOTES.md in this repo yet. Start one: it gets the Inbox / Next up / Ideas / Bugs / Done sections every chat and Catalyzer understand.", 13.5, "CzTextDimBrush");
                msg.TextWrapping = TextWrapping.Wrap; msg.TextTrimming = TextTrimming.None; msg.Margin = new Thickness(0, 6, 0, 16);
                _notesList.Children.Add(msg);
                _notesList.Children.Add(new CzButton("START NOTES.MD", () => { try { n.Create(p.Name); NotesRefresh(true); PluginHost.I?.UpdateBadges(); _noteInput.Focus(); } catch (Exception ex) { Log.Write("notes: " + ex.Message, show: true); } }, "", primary: true, height: 32) { HorizontalAlignment = HorizontalAlignment.Left });
                return;
            }

            var items = n.Items();
            var order = Notes.Sections.Concat(items.Select(i => i.Section).Where(s => s != "" && !Notes.Sections.Any(x => Notes.Is(x, s))).Distinct()).ToList();
            if (items.Any(i => i.Section == "")) order.Insert(0, "");
            int delay = 0;
            foreach (var sec in order)
            {
                var mine = items.Where(i => Notes.Is(i.Section, sec) || (sec == "" && i.Section == "")).ToList();
                bool done = Notes.Is(sec, "Done");
                if (mine.Count == 0 && (done || Notes.Is(sec, "Ideas") || Notes.Is(sec, "Bugs") || Notes.Is(sec, "Next up"))) continue;
                var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, _notesList.Children.Count == 0 ? 0 : 16, 0, 6) };
                head.Children.Add(Cz.Mono((sec == "" ? "TOP" : sec).ToUpperInvariant(), 10.5, Notes.Is(sec, "Inbox") && mine.Count > 0 ? "CzAccentBrush" : "CzTextDimBrush"));
                head.Children.Add(Cz.Mono("  ·  " + mine.Count(i => !i.Done), 10.5, "CzTextFaintBrush"));
                _notesList.Children.Add(head);
                if (mine.Count == 0) { _notesList.Children.Add(Cz.Text(Notes.Is(sec, "Inbox") ? "Empty. Type above and press Enter." : "—", 12.5, "CzTextFaintBrush").Also(t => t.Margin = new Thickness(30, 0, 0, 4))); continue; }
                foreach (var it in done ? mine.Take(20) : mine)
                {
                    var row = NoteRow(it);
                    if (animate && !Cz.Still) { row.Opacity = 0; row.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = TimeSpan.FromMilliseconds(Math.Min(delay, 300)) }); delay += 12; }
                    _notesList.Children.Add(row);
                }
                if (done && mine.Count > 20) _notesList.Children.Add(Cz.Mono("+" + (mine.Count - 20) + " MORE IN THE FILE", 10, "CzTextFaintBrush").Also(t => t.Margin = new Thickness(30, 4, 0, 0)));
            }
            _notesScroll.ScrollToVerticalOffset(keep);
        }

        /// <summary>One note: a drawn check box, the text (wraps), hover glow; right-click moves it between sections.</summary>
        private FrameworkElement NoteRow(Notes.Item it)
        {
            var row = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 2, 0, 2), MinHeight = 30 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            var wash = new Rectangle { Opacity = 0, IsHitTestVisible = false, RadiusX = 2, RadiusY = 2 };
            wash.SetResourceReference(Shape.FillProperty, "CzSelectBrush");
            Grid.SetColumnSpan(wash, 2); row.Children.Add(wash);

            var box = new Grid { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(4, 6, 0, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, ToolTip = it.Done ? "BACK TO THE INBOX" : "DONE  //  MOVES TO DONE WITH TODAY'S DATE" };
            var frame = new System.Windows.Shapes.Path { Data = Cz.Chamfer(16, 16, 4), StrokeThickness = 1 };
            frame.SetResourceReference(Shape.StrokeProperty, it.Done ? "CzOkBrush" : "CzTextDimBrush");
            box.Children.Add(frame);
            if (it.Done) { var tick = Cz.Text("", 10, "CzOkBrush", "CzFontIcons"); tick.HorizontalAlignment = HorizontalAlignment.Center; tick.VerticalAlignment = VerticalAlignment.Center; box.Children.Add(tick); }
            if (!it.Check && !it.Done) frame.Opacity = 0.5;
            box.MouseLeftButtonUp += (s, e) => { e.Handled = true; NoteEdit(() => _notes.Toggle(it), it.Done ? "BACK IN THE INBOX" : "DONE  //  MOVED TO DONE"); };
            row.Children.Add(box);

            var text = Cz.Text(it.Text, 13.5, it.Done ? "CzTextFaintBrush" : "CzTextBrush");
            text.TextWrapping = TextWrapping.Wrap; text.TextTrimming = TextTrimming.None; text.Margin = new Thickness(0, 5, 0, 5);
            if (it.Done) text.TextDecorations = null;
            Grid.SetColumn(text, 1); row.Children.Add(text);

            row.MouseEnter += (s, e) => wash.BeginAnimation(OpacityProperty, new DoubleAnimation(0.08, TimeSpan.FromMilliseconds(Cz.Still ? 0 : 120)));
            row.MouseLeave += (s, e) => wash.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(Cz.Still ? 0 : 200)));
            row.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                var items = Notes.Sections.Where(sec => !Notes.Is(sec, it.Section) && !Notes.Is(sec, "Done"))
                    .Select(sec => new CzMenu.Item { Text = "Move to " + sec, Glyph = "", Click = () => NoteEdit(() => _notes.Move(it, sec), "MOVED TO " + sec.ToUpperInvariant()) }).ToList();
                items.Add(new CzMenu.Item { Separator = true });
                items.Add(new CzMenu.Item { Text = it.Done ? "Not done" : "Done", Glyph = it.Done ? "" : "", Click = () => NoteEdit(() => _notes.Toggle(it), it.Done ? "BACK IN THE INBOX" : "DONE") });
                items.Add(new CzMenu.Item { Text = "Copy", Glyph = "", Click = () => { try { Clipboard.SetText(it.Text); } catch { } } });
                new CzMenu().Show(row, "NOTE", items);
            };
            return row;
        }

        private void NoteEdit(Action edit, string say)
        {
            try { edit(); Say(say); }
            catch (Exception ex) { Log.Write("notes: " + ex.Message, show: true); }
            NotesRefresh(true);
            PluginHost.I?.UpdateBadges();
        }

        private void AddNote()
        {
            var t = _noteInput.Text.Trim(); if (t == "" || _notes == null) return;
            _noteInput.Text = "";
            NoteEdit(() => _notes.Add(t), "NOTED  //  IN THE INBOX");
        }

        private void ToggleRaw()
        {
            if (_notes == null) return;
            if (_raw) { if (!ConfirmLeaveRaw()) return; NotesRefresh(true); PluginHost.I?.UpdateBadges(); return; }
            _notes.Load();
            _noteRaw.Text = _notes.Raw;
            _raw = true;
            NotesRefresh(false);
            _noteRaw.Focus();
        }

        private async void SyncNotes()
        {
            if (_notes == null || _syncing) return;
            if (_raw && !ConfirmLeaveRaw()) return;
            _syncing = true; NotesRefresh(false); Say("SYNCING NOTES...");
            var (ok, say) = await _notes.Sync();
            _syncing = false;
            Log.Write((_notesFor?.Name ?? "") + " notes sync: " + say, show: true);
            if (NotesOpen) NotesRefresh(true);
            PluginHost.I?.UpdateBadges();
            if (_notesFor != null) { var pr = _notesFor; await Projects.LoadGit(pr); _board.Touch(pr); }
        }

        /// <summary>--snapshot: the notes drawer for the first project that has notes.</summary>
        internal void NotesForSnapshot()
        {
            CloseFeed(); ClosePrompt();
            var p = _board.Items.FirstOrDefault(x => new Notes(x.Folder).Exists) ?? _board.Items.FirstOrDefault();
            if (p != null) OpenNotes(p);
        }
    }
}
