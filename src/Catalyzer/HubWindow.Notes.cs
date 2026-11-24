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
    /// NOTES: each card's docs/NOTES.md (Catalyzer Development Principles 5.0.0, 07): Aaron's own notes.
    /// The card's NOTES chip shows how many there are; clicking it slides in the notes drawer: type a note + Enter (it
    /// lands at the top, stamped with the time), right-click to copy or remove one, RAW to edit the whole file, SYNC to
    /// commit + pull + push just NOTES.md, WIPE + SYNC for a fresh slate once a chat has pulled them in, BACKUPS for
    /// every version NotesCache kept. Changes on disk show up live, and each new version is backed up.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Grid _notesHost = new Grid { Visibility = Visibility.Collapsed };
        private readonly TranslateTransform _notesX = new TranslateTransform();
        private readonly StackPanel _notesList = new StackPanel();
        private readonly ScrollViewer _notesScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        private readonly WrapPanel _notesButtons = new WrapPanel();
        private readonly Chip _notesChip = new Chip(24) { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        private readonly TextBox _noteInput = new TextBox();
        private readonly TextBox _noteRaw = new TextBox();
        private readonly Grid _noteInputRow = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        private TextBlock _notesTitle;
        private Project _notesFor;
        private Notes _notes;
        private bool _raw;
        private const double NotesWidth = 640;
        private readonly Dictionary<string, (DateTime when, int open, int inbox)> _noteCounts = new Dictionary<string, (DateTime, int, int)>(StringComparer.OrdinalIgnoreCase);
        private readonly List<FileSystemWatcher> _noteWatch = new List<FileSystemWatcher>();
        private DispatcherTimer _notesSoon;
        /// <summary>Where each project's NOTES.md stands against GitHub (filled in the background).</summary>
        private readonly Dictionary<string, Notes.SyncState> _syncOf = new Dictionary<string, Notes.SyncState>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _syncBusy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _syncSaid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>What changed through the panel since the last sync (for the commit message).</summary>
        private readonly Dictionary<string, Dictionary<string, int>> _noteWhat = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DispatcherTimer> _autoSync = new Dictionary<string, DispatcherTimer>(StringComparer.OrdinalIgnoreCase);
        private DispatcherTimer _fetchTimer;
        private const int AutoSyncSeconds = 60;

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
            PluginHost.I?.AddBadge(new ProjectBadge
            {
                Id = "notes-sync", Order = -9,
                State = SyncBadge,
                Click = p => SyncProject(p, auto: false),
                Menu = p => new[]
                {
                    new MenuEntry { Text = "Sync notes now", Glyph = "\uE895", Hint = "commit NOTES.md, pull notes, push", Run = () => SyncProject(p, auto: false) },
                    new MenuEntry { Text = "Open notes", Glyph = "\uE70B", Run = () => OpenNotes(p) },
                },
            });
            _board.Changed += WatchNotes;
            // GitHub is asked now and then, so notes written elsewhere show up as "new" on the card
            _fetchTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
            _fetchTimer.Tick += (s, e) => { foreach (var p in _board.Items) RefreshSync(p.Folder, fetch: true); };
            if (!Prefs.Offline) _fetchTimer.Start();
        }

        /// <summary>Ask git (in the background) where a project's notes stand; the chips redraw when it answers.</summary>
        private async void RefreshSync(string folder, bool fetch)
        {
            if (Prefs.Offline || _syncBusy.Contains(folder)) return;
            var n = new Notes(folder);
            if (!n.Exists) { _syncOf.Remove(folder); return; }
            try
            {
                var st = await System.Threading.Tasks.Task.Run(() => n.Status(fetch));
                _syncOf[folder] = st;
                PluginHost.I?.UpdateBadges();
            }
            catch (Exception ex) { Log.Write("notes status " + folder + ": " + ex.Message); }
        }

        private BadgeState SyncBadge(Project p)
        {
            if (_syncBusy.Contains(p.Folder)) return new BadgeState { Text = "SYNCING", Tone = Tone.Select, Pulse = true, Tip = "NOTES.MD  //  COMMIT, PULL, PUSH" };
            if (!_syncOf.TryGetValue(p.Folder, out var st)) return null;
            return st.Kind switch
            {
                Notes.SyncKind.Unsynced => new BadgeState { Text = "SYNC  \u2191", Tone = Tone.Warn, Pulse = true, Tip = "YOUR NOTES AREN'T ON GITHUB YET  //  CLICK TO PUSH" + (Prefs.Value.NotesAutoSync ? "\n(AUTO-SYNC PUSHES THEM A MINUTE AFTER YOUR LAST EDIT)" : "") },
                Notes.SyncKind.Incoming => new BadgeState { Text = "SYNC  \u2193", Tone = Tone.Accent, Pulse = true, Tip = "GITHUB HAS NEWER NOTES (PHONE / ANOTHER PC)  //  CLICK TO BRING THEM IN" },
                Notes.SyncKind.Both => new BadgeState { Text = "SYNC  \u2195", Tone = Tone.Warn, Pulse = true, Tip = "NEW NOTES HERE AND ON GITHUB  //  CLICK TO MERGE AND PUSH" },
                _ => null,
            };
        }

        /// <summary>SYNC for one project: the card chip, the drawer button, and auto-sync all come here.</summary>
        private async void SyncProject(Project p, bool auto, string what = null)
        {
            if (p == null || _syncBusy.Contains(p.Folder)) return;
            if (_notesFor == p && _raw && !ConfirmLeaveRaw()) return;
            if (_autoSync.TryGetValue(p.Folder, out var t)) t.Stop();
            _syncBusy.Add(p.Folder);
            PluginHost.I?.UpdateBadges(); if (_notesFor == p && NotesOpen) NotesRefresh(false);
            what ??= WhatChanged(p.Folder);
            (bool ok, string say) r;
            try { r = await new Notes(p.Folder).Sync(what, auto); }
            catch (Exception ex) { r = (false, "SYNC FAILED  //  " + ex.Message); }
            _syncBusy.Remove(p.Folder);
            if (r.ok) _noteWhat.Remove(p.Folder);
            _syncSaid[p.Folder] = r.say;
            Log.Write(p.Name + " notes: " + r.say, show: !auto || !r.ok);
            RefreshSync(p.Folder, fetch: false);
            if (_notesFor == p && NotesOpen) NotesRefresh(true);
            await Projects.LoadGit(p); _board.Touch(p);
        }

        /// <summary>"+2 inbox, 1 done": what the panel did since the last sync, for a readable commit message.</summary>
        private string WhatChanged(string folder)
        {
            if (!_noteWhat.TryGetValue(folder, out var w) || w.Count == 0) return "updated";
            return string.Join(", ", w.Select(kv => kv.Key == "added" ? "+" + kv.Value : kv.Value + " " + kv.Key));
        }

        private void Count(string folder, string what)
        {
            if (!_noteWhat.TryGetValue(folder, out var w)) _noteWhat[folder] = w = new Dictionary<string, int>();
            w[what] = w.TryGetValue(what, out var n) ? n + 1 : 1;
        }

        /// <summary>An edit through the panel: auto-sync a minute after the last one (if it's on).</summary>
        private void AutoSyncSoon(Project p)
        {
            RefreshSync(p.Folder, fetch: false);
            if (!Prefs.Value.NotesAutoSync || Prefs.Offline) return;
            if (!_autoSync.TryGetValue(p.Folder, out var t))
            {
                t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(AutoSyncSeconds) };
                var pr = p;
                t.Tick += (s, e) => { t.Stop(); SyncProject(pr, auto: true); };
                _autoSync[p.Folder] = t;
            }
            t.Stop(); t.Start();
        }

        // ================================================================== the chip

        private BadgeState NotesBadge(Project p)
        {
            var n = new Notes(p.Folder);
            if (!n.Exists)
                return n.Legacy
                    ? new BadgeState { Text = "NOTES  ·  BACKLOG", Tone = Tone.Warn, Tip = "THIS REPO STILL USES BACKLOG.MD (PRINCIPLES < 2.0.0)\nASK ITS CHAT: UPDATE TO THE LATEST PRINCIPLES" }
                    : new BadgeState { Text = "NOTES  +", Tone = Tone.Faint, Tip = "NO DOCS\\NOTES.MD YET  //  CLICK TO START YOUR NOTES" };
            int open = 0, inbox = 0;
            try
            {
                var when = File.GetLastWriteTimeUtc(n.Path);
                if (_noteCounts.TryGetValue(p.Folder, out var c) && c.when == when) { open = c.open; inbox = c.inbox; }
                else { n.Load(); (open, inbox) = n.Count(); _noteCounts[p.Folder] = (when, open, inbox); }
            }
            catch { }
            if (_syncOf.TryGetValue(p.Folder, out var ss) && (ss.Kind == Notes.SyncKind.Unsynced || ss.Kind == Notes.SyncKind.Both) && !_syncBusy.Contains(p.Folder))
                return new BadgeState { Text = "NOTES " + open + (inbox > 0 ? "  ·  " + inbox + " NEW" : ""), Tone = inbox > 0 ? Tone.Accent : Tone.Dim, Tip = "NOT ON GITHUB YET  //  SEE THE SYNC CHIP" };
            return inbox > 0
                ? new BadgeState { Text = "NOTES " + open + "  ·  " + inbox + " NEW", Tone = Tone.Accent, Tip = inbox + " IN THE INBOX, " + open + " OPEN  //  CLICK TO OPEN" }
                : new BadgeState { Text = "NOTES " + open, Tone = open > 0 ? Tone.Accent : Tone.Dim, Tip = open + " NOTE(S)  //  CLICK TO OPEN" };
        }

        /// <summary>Each project's docs folder, for NOTES.md only: a chat or an editor changing it updates the chip and the drawer.</summary>
        private void WatchNotes()
        {
            if (Prefs.Offline) return;
            int i = 0;
            foreach (var p in _board.Items.ToList())
            {
                var f = p.Folder; bool fetch = true;
                var d = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400 + 250 * i++) };   // staggered: never a burst of git
                d.Tick += (s, e) => { d.Stop(); RefreshSync(f, fetch); };
                d.Start();
            }
            var folders = _board.Items.Select(p => p.Folder).ToList();
            if (!_board.Scanning)
                System.Threading.Tasks.Task.Run(() =>
                {
                    NotesCache.Purge(KeepBackups);
                    foreach (var f in folders) NotesCache.Snapshot(f, "seen");
                });
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

        /// <summary>Keep a project's notes backups while it's on the board, hidden, or its drive is away (offline drives
        /// aren't a removal). They go when the folder is gone or no longer under a project folder in Settings.</summary>
        private bool KeepBackups(string folder)
        {
            static bool Same(string a, string b) => string.Equals(a?.TrimEnd('\\', '/'), b?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            if (_board.Items.ToList().Any(p => Same(p.Folder, folder))) return true;
            var pr = Prefs.Value;
            if (pr.Hidden.Any(h => Same(h, folder))) return true;
            var parent = System.IO.Path.GetDirectoryName(folder.TrimEnd('\\', '/'));
            if (parent == null || !Directory.Exists(parent)) return true;
            if (!Directory.Exists(folder)) return false;
            return pr.Roots.Any(r => Same(r, parent)) || pr.Extra.Any(e => Same(e, folder));
        }

        private void NotesTouched(string folder)
        {
            System.Threading.Tasks.Task.Run(() => NotesCache.Snapshot(folder, "changed"));
            if (_notesSoon == null)
            {
                _notesSoon = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _notesSoon.Tick += (s, e) => { _notesSoon.Stop(); PluginHost.I?.UpdateBadges(); if (NotesOpen && !_raw) NotesRefresh(true); };
            }
            RefreshSync(folder, fetch: false);
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
            _noteInput.ToolTip = "A NEW NOTE  //  ENTER ADDS IT AT THE TOP, STAMPED WITH THE TIME";
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
            PullOnOpen(p);
            Dispatcher.BeginInvoke(new Action(() => { if (_notes.Exists) _noteInput.Focus(); }), DispatcherPriority.Input);
            if (Cz.Still) { _notesX.X = 0; return; }
            _notesX.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(NotesWidth + 20, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        /// <summary>Opening the notes brings in notes written elsewhere, but only when that's all GitHub has.</summary>
        private async void PullOnOpen(Project p)
        {
            if (Prefs.Offline || _syncBusy.Contains(p.Folder) || !new Notes(p.Folder).Exists) return;
            _syncBusy.Add(p.Folder);
            string said = null;
            try { said = await new Notes(p.Folder).PullIfSafe(); } catch (Exception ex) { Log.Write("notes pull: " + ex.Message); }
            _syncBusy.Remove(p.Folder);
            if (said != null) { _syncSaid[p.Folder] = said; Say(said); }
            RefreshSync(p.Folder, fetch: false);
            if (_notesFor == p && NotesOpen && !_raw) NotesRefresh(true);
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
            if (_notes != null && _noteRaw.Text != _notes.Raw) { try { _notes.SaveRaw(_noteRaw.Text); Say("NOTES SAVED"); if (_notesFor != null) { Count(_notesFor.Folder, "edited by hand"); AutoSyncSoon(_notesFor); } } catch (Exception ex) { Log.Write("notes: " + ex.Message, show: true); return false; } }
            _raw = false; return true;
        }

        private void NotesRefresh(bool animate)
        {
            var n = _notes; var p = _notesFor; if (n == null || p == null) return;
            try { n.Load(); } catch (Exception ex) { Log.Write("notes " + n.Path + ": " + ex.Message, show: true); }
            var st = NotesBadge(p);
            _notesChip.Set(st.Text, st.Tone, _syncBusy.Contains(p.Folder), n.Exists ? "DOCS\\NOTES.MD" : null);

            _notesButtons.Children.Clear();
            if (n.Exists)
            {
                bool busy = _syncBusy.Contains(p.Folder);
                var gap = new Thickness(0, 0, 8, 6);
                _notesButtons.Children.Add(new CzButton(busy ? "SYNCING..." : "SYNC", () => SyncProject(p, auto: false), "", primary: true, height: 28, tip: "COMMIT NOTES.MD, PULL, PUSH  //  NOTES FROM YOUR PHONE ARRIVE, YOURS LEAVE") { Margin = gap });
                _notesButtons.Children.Add(new CzButton("WIPE + SYNC", () => WipeNotes(p), "\uE74D", height: 28, tip: "A FRESH SLATE: CLEAR EVERY NOTE (BACKED UP FIRST) AND SYNC  //  AFTER A CHAT HAS PULLED THEM IN") { Margin = gap, IsEnabled = !busy });
                _notesButtons.Children.Add(new CzButton(_raw ? "LIST" : "RAW", ToggleRaw, _raw ? "" : "", height: 28, tip: _raw ? "BACK TO THE LIST (SAVES)" : "EDIT THE WHOLE FILE") { Margin = gap });
                _notesButtons.Children.Add(new CzButton("EDITOR", () => Launch.Shell(n.Path, null), "", height: 28, tip: "OPEN NOTES.MD IN YOUR EDITOR") { Margin = gap });
                _notesButtons.Children.Add(new CzButton("BACKUPS", () => OpenBackups(p), "\uE81C", height: 28, tip: "EVERY VERSION OF THESE NOTES CATALYZER KEPT  //  " + NotesCache.DirFor(p.Folder).ToUpperInvariant()) { Margin = gap });
            }
            if (n.Exists)
            {
                string line = _syncSaid.TryGetValue(p.Folder, out var said) ? said : null;
                if (_syncOf.TryGetValue(p.Folder, out var ss))
                    line ??= ss.Kind switch
                    {
                        Notes.SyncKind.Synced => "IN STEP WITH GITHUB",
                        Notes.SyncKind.Unsynced => Prefs.Value.NotesAutoSync ? "NOT ON GITHUB YET  //  AUTO-SYNC IN A MINUTE" : "NOT ON GITHUB YET  //  PRESS SYNC",
                        Notes.SyncKind.Incoming => "GITHUB HAS NEWER NOTES  //  PRESS SYNC",
                        Notes.SyncKind.Both => "NEW NOTES HERE AND ON GITHUB  //  PRESS SYNC",
                        Notes.SyncKind.NoUpstream => "NOT ON GITHUB (NO UPSTREAM)",
                        _ => null,
                    };
                if (line != null)
                {
                    var tb = Cz.Mono(line, 9.5, "CzTextFaintBrush"); tb.VerticalAlignment = VerticalAlignment.Center; tb.Margin = new Thickness(2, 6, 0, 6);
                    tb.TextTrimming = TextTrimming.CharacterEllipsis; tb.MaxWidth = NotesWidth - 60; tb.ToolTip = line;
                    _notesButtons.Children.Add(tb);
                }
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
                    ? "This repo still keeps its list in docs\\BACKLOG.md. The Catalyzer Development Principles (2.0.0 and later) merge it into docs\\NOTES.md. Ask the project's chat to \"update to the latest principles\", or start a fresh NOTES.md here (BACKLOG.md stays as it is)."
                    : "No docs\\NOTES.md in this repo yet. Start one: your own notepad for this project. Every note is stamped with the time; chats read it when you ask and never edit it.", 13.5, "CzTextDimBrush");
                msg.TextWrapping = TextWrapping.Wrap; msg.TextTrimming = TextTrimming.None; msg.Margin = new Thickness(0, 6, 0, 16);
                _notesList.Children.Add(msg);
                _notesList.Children.Add(new CzButton("START NOTES.MD", () => { try { n.Create(p.Name); NotesRefresh(true); PluginHost.I?.UpdateBadges(); _noteInput.Focus(); } catch (Exception ex) { Log.Write("notes: " + ex.Message, show: true); } }, "", primary: true, height: 32) { HorizontalAlignment = HorizontalAlignment.Left });
                return;
            }

            var items = n.Items();
            var order = n.SectionsInFile();
            if (order.Count == 0) order.Add(Notes.Main);
            if (items.Any(i => i.Section == "")) order.Insert(0, "");
            int delay = 0;
            foreach (var sec in order)
            {
                var mine = items.Where(i => Notes.Is(i.Section, sec) || (sec == "" && i.Section == "")).ToList();
                bool done = Notes.Is(sec, "Done");
                bool main = Notes.Is(sec, Notes.Main) || Notes.Is(sec, "Inbox");
                if (mine.Count == 0 && !main) continue;
                var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, _notesList.Children.Count == 0 ? 0 : 16, 0, 6) };
                head.Children.Add(Cz.Mono((sec == "" ? "TOP" : sec).ToUpperInvariant(), 10.5, main && mine.Count > 0 ? "CzAccentBrush" : "CzTextDimBrush"));
                head.Children.Add(Cz.Mono("  ·  " + mine.Count(i => !i.Done), 10.5, "CzTextFaintBrush"));
                _notesList.Children.Add(head);
                if (mine.Count == 0) { _notesList.Children.Add(Cz.Text(main ? "A fresh slate. Type above and press Enter." : "—", 12.5, "CzTextFaintBrush").Also(t => t.Margin = new Thickness(30, 0, 0, 4))); continue; }
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

        /// <summary>One note: its stamp and text (wraps), a drawn check box only for old "- [ ]" items, hover glow;
        /// right-click copies or removes it (and moves it, in an old file with several sections).</summary>
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
            box.MouseLeftButtonUp += (s, e) => { e.Handled = true; NoteEdit(() => _notes.Toggle(it), it.Done ? "REOPENED" : "DONE  //  MOVED TO DONE", it.Done ? "reopened" : "done"); };
            if (it.Check || it.Done) row.Children.Add(box);
            else
            {
                var dot = new System.Windows.Shapes.Path { Data = Cz.Chamfer(6, 6, 2), Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(9, it.Stamp != null ? 7 : 11, 0, 0), IsHitTestVisible = false };
                dot.SetResourceReference(Shape.FillProperty, "CzAccentBrush");
                row.Children.Add(dot);
            }

            var col = new StackPanel { Margin = new Thickness(0, 4, 0, 5) };
            if (it.Stamp != null) col.Children.Add(Cz.Mono(it.Stamp, 9.5, "CzTextFaintBrush").Also(s => s.Margin = new Thickness(0, 0, 0, 1)));
            var text = Cz.Text(it.Text, 13.5, it.Done ? "CzTextFaintBrush" : "CzTextBrush");
            text.TextWrapping = TextWrapping.Wrap; text.TextTrimming = TextTrimming.None;
            if (it.Done) text.TextDecorations = null;
            col.Children.Add(text);
            Grid.SetColumn(col, 1); row.Children.Add(col);

            row.MouseEnter += (s, e) => wash.BeginAnimation(OpacityProperty, new DoubleAnimation(0.08, TimeSpan.FromMilliseconds(Cz.Still ? 0 : 120)));
            row.MouseLeave += (s, e) => wash.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(Cz.Still ? 0 : 200)));
            row.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                var items = _notes.SectionsInFile().Where(sec => !Notes.Is(sec, it.Section) && !Notes.Is(sec, "Done"))
                    .Select(sec => new CzMenu.Item { Text = "Move to " + sec, Glyph = "", Click = () => NoteEdit(() => _notes.Move(it, sec), "MOVED TO " + sec.ToUpperInvariant(), "moved") }).ToList();
                if (items.Count > 0) items.Add(new CzMenu.Item { Separator = true });
                if (it.Check || it.Done) items.Add(new CzMenu.Item { Text = it.Done ? "Not done" : "Done", Glyph = it.Done ? "" : "", Click = () => NoteEdit(() => _notes.Toggle(it), it.Done ? "REOPENED" : "DONE", it.Done ? "reopened" : "done") });
                items.Add(new CzMenu.Item { Text = "Copy", Glyph = "", Click = () => { try { Clipboard.SetText(it.Text); } catch { } } });
                items.Add(new CzMenu.Item { Text = "Remove", Glyph = "\uE74D", Click = () => NoteEdit(() => { NotesCache.Snapshot(_notes.Folder, "before-remove"); _notes.Remove(it); }, "REMOVED  //  IT'S IN BACKUPS", "removed") });
                new CzMenu().Show(row, "NOTE", items);
            };
            return row;
        }

        private void NoteEdit(Action edit, string say, string what = "edited")
        {
            try { edit(); Say(say); if (_notesFor != null) { Count(_notesFor.Folder, what); _syncSaid.Remove(_notesFor.Folder); AutoSyncSoon(_notesFor); } }
            catch (Exception ex) { Log.Write("notes: " + ex.Message, show: true); }
            NotesRefresh(true);
            PluginHost.I?.UpdateBadges();
        }

        private void AddNote()
        {
            var t = _noteInput.Text.Trim(); if (t == "" || _notes == null) return;
            _noteInput.Text = "";
            NoteEdit(() => _notes.Add(t), "NOTED", "added");
        }

        /// <summary>WIPE + SYNC: confirm, then bring in anything new from GitHub (if something arrives, stop: Aaron hasn't
        /// seen it), back the file up, clear it to the fresh slate, and sync.</summary>
        private void WipeNotes(Project p)
        {
            if (p == null || _syncBusy.Contains(p.Folder)) return;
            if (_notesFor == p && _raw && !ConfirmLeaveRaw()) return;
            var n = new Notes(p.Folder);
            try { n.Load(); } catch (Exception ex) { Log.Write("notes: " + ex.Message, show: true); return; }
            int count = n.Items().Count;
            if (count == 0 && n.SectionsInFile().All(s => Notes.Is(s, Notes.Main))) { Say("NOTHING TO WIPE  //  ALREADY A FRESH SLATE"); return; }
            bool backups = Prefs.Value.NotesCacheMB > 0;
            var d = Dialog("Wipe " + p.Name + " notes", 600, onEsc: null);
            d.Body.Children.Add(Para(count + " note(s) will be cleared from docs\\NOTES.md, and the fresh file synced to GitHub. Do this once a chat has pulled them in.", 13.5, "CzTextBrush"));
            d.Body.Children.Add(Para(backups
                ? "A copy goes to the notes backups first (BACKUPS in the drawer), and git history keeps every pushed version."
                : "Notes backups are OFF in Settings, so only git history keeps the old version.", 12.5, "CzTextDimBrush"));
            d.Buttons.Children.Add(Btn("KEEP THEM", d.Close));
            var go = Btn("WIPE + SYNC", null, "\uE74D", primary: true);
            go.Click = () => { d.Close(); DoWipe(p, count); };
            d.Buttons.Children.Add(go);
        }

        private async void DoWipe(Project p, int count)
        {
            if (_syncBusy.Contains(p.Folder)) { Say("A SYNC IS RUNNING  //  TRY AGAIN IN A MOMENT"); return; }
            _syncBusy.Add(p.Folder);
            PluginHost.I?.UpdateBadges(); if (_notesFor == p && NotesOpen) NotesRefresh(false);
            string arrived = null;
            try { if (!Prefs.Offline) arrived = await new Notes(p.Folder).PullIfSafe(); }
            catch (Exception ex) { arrived = "COULDN'T CHECK GITHUB  //  " + ex.Message; }
            if (arrived != null)
            {
                _syncBusy.Remove(p.Folder);
                Say("NOT WIPED  //  " + arrived + "  //  LOOK, THEN WIPE AGAIN");
                RefreshSync(p.Folder, fetch: false);
                if (_notesFor == p && NotesOpen) NotesRefresh(true);
                return;
            }
            try
            {
                await System.Threading.Tasks.Task.Run(() => { NotesCache.Snapshot(p.Folder, "before-wipe"); new Notes(p.Folder).Wipe(p.Name); });
            }
            catch (Exception ex) { _syncBusy.Remove(p.Folder); Log.Write("notes wipe: " + ex.Message, show: true); return; }
            _syncBusy.Remove(p.Folder);
            _noteWhat.Remove(p.Folder);
            Log.Write(p.Name + " notes: wiped " + count + " (backed up)");
            if (_notesFor == p && NotesOpen) NotesRefresh(true);
            PluginHost.I?.UpdateBadges();
            SyncProject(p, auto: false, what: "wiped " + count + " note(s) for a fresh slate (backed up)");
        }

        private void OpenBackups(Project p)
        {
            var dir = NotesCache.DirFor(p.Folder);
            if (!Directory.Exists(dir)) NotesCache.Snapshot(p.Folder, "seen");
            if (!Directory.Exists(dir)) { Say(Prefs.Value.NotesCacheMB > 0 ? "NO BACKUPS YET" : "NOTES BACKUPS ARE OFF  //  SETTINGS"); return; }
            Launch.Shell("explorer.exe", "\"" + dir + "\"");
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

        /// <summary>--snapshot: the notes drawer for the first project that has notes.</summary>
        internal void NotesForSnapshot()
        {
            CloseFeed(); ClosePrompt();
            var p = _board.Items.FirstOrDefault(x => new Notes(x.Folder).Exists) ?? _board.Items.FirstOrDefault();
            if (p != null) OpenNotes(p);
        }
    }
}
