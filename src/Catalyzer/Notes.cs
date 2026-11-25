using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// A project's docs/NOTES.md: Aaron's own notes (Catalyzer Development Principles 5.0.0, 07-APP-REPO). One
    /// <c>## Notes</c> section, newest first, each line stamped: <c>- 2026-11-24 14:57 — the note</c>. Chats read it
    /// when asked and never edit it; Aaron wipes it for a fresh slate (WIPE + SYNC), and <see cref="NotesCache"/>
    /// keeps every version. Older files (Inbox / Next up / Ideas / Bugs / Done, "- [ ]" items) still read and edit
    /// fine. Catalyzer keeps whatever else is in the file exactly as it was.
    /// </summary>
    public sealed class Notes
    {
        public const string RelPath = "docs\\NOTES.md";
        /// <summary>The one section of a 5.0.0 NOTES.md.</summary>
        public const string Main = "Notes";
        /// <summary>The stamp every note starts with.</summary>
        public const string StampFormat = "yyyy-MM-dd HH:mm";
        public const string Intro =
            "Aaron's notes: ideas, features, things to remember. Only Aaron writes here (by hand, or from Catalyzer's notes\n" +
            "panel). A chat reads them when he asks, carries what it takes into ROADMAP or HANDOFF, and never edits this file.\n" +
            "Aaron wipes it for a fresh slate (Catalyzer: WIPE + SYNC); Catalyzer keeps a backup of every version.\n" +
            "Every note starts with when it was written: `- 2026-11-24 14:57 — the note`.";

        public sealed class Item
        {
            public string Section; public int Line; public string Text; public bool Done; public bool Check;
            /// <summary>"2026-11-24 14:57" (or just the date) when the note starts with a stamp; Text is then the rest.</summary>
            public string Stamp;
        }

        private static readonly System.Text.RegularExpressions.Regex StampRx =
            new System.Text.RegularExpressions.Regex(@"^(\d{4}-\d{2}-\d{2}(?:[ T]\d{1,2}:\d{2})?)\s+[—–-]\s+(.*)$");

        /// <summary>The sections in the order the file has them ("" first if there are items above any heading).</summary>
        public List<string> SectionsInFile()
        {
            var list = new List<string>();
            foreach (var l in _lines) { var t = l.Trim(); if (t.StartsWith("## ")) { var s = t.Substring(3).Trim(); if (!list.Any(x => Is(x, s))) list.Add(s); } }
            return list;
        }

        public string Folder { get; }
        public string Path => System.IO.Path.Combine(Folder, RelPath);
        public bool Exists => File.Exists(Path);
        /// <summary>The repo is still on the old BACKLOG.md (principles before 2.0.0).</summary>
        public bool Legacy => !Exists && File.Exists(System.IO.Path.Combine(Folder, "docs", "BACKLOG.md"));

        private List<string> _lines = new List<string>();
        private string _nl = "\n";

        public Notes(string folder) { Folder = folder; }

        public void Load()
        {
            _lines.Clear();
            if (!Exists) return;
            var text = File.ReadAllText(Path, Encoding.UTF8);
            _nl = text.Contains("\r\n") ? "\r\n" : "\n";
            _lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            if (_lines.Count > 0 && _lines[^1] == "") _lines.RemoveAt(_lines.Count - 1);
        }

        public string Raw => string.Join(_nl, _lines) + _nl;

        /// <summary>Every list line, with the section it sits in ("" before the first heading).</summary>
        public List<Item> Items()
        {
            var list = new List<Item>(); string sec = ""; bool inComment = false;
            for (int i = 0; i < _lines.Count; i++)
            {
                var l = _lines[i]; var t = l.Trim();
                if (t.StartsWith("<!--")) inComment = !t.Contains("-->");
                else if (inComment) { if (t.Contains("-->")) inComment = false; continue; }
                if (inComment || t.StartsWith("<!--")) continue;
                if (t.StartsWith("## ")) { sec = t.Substring(3).Trim(); continue; }
                if (!l.StartsWith("- ")) continue;   // top-level items only; indented lines belong to the one above
                var body = l.Substring(2);
                bool check = body.StartsWith("[ ]") || body.StartsWith("[x]") || body.StartsWith("[X]");
                bool done = body.StartsWith("[x]") || body.StartsWith("[X]");
                var text = (check ? body.Substring(3) : body).Trim();
                // a note that wraps onto indented lines (not a sub-list) is still one note
                for (int j = i + 1; j < _lines.Count && (_lines[j].StartsWith("  ") || _lines[j].StartsWith("\t")) && !_lines[j].TrimStart().StartsWith("- "); j++)
                    text += " " + _lines[j].Trim();
                string stamp = null;
                var m = StampRx.Match(text);
                if (m.Success) { stamp = m.Groups[1].Value; text = m.Groups[2].Value; }
                list.Add(new Item { Section = sec, Line = i, Check = check, Done = done, Text = text, Stamp = stamp });
            }
            return list;
        }

        /// <summary>How many notes (not ticked, not in an old Done section), and how many sit in an old Inbox.</summary>
        public (int open, int inbox) Count()
        {
            var it = Items().Where(i => !i.Done && !Is(i.Section, "Done")).ToList();
            return (it.Count, it.Count(i => Is(i.Section, "Inbox")));
        }

        /// <summary>Where a new note goes: <c>## Notes</c>, or an old file's Inbox, or a new <c>## Notes</c>.</summary>
        private string AddTo() => SectionsInFile().Any(s => Is(s, Main)) ? Main : SectionsInFile().Any(s => Is(s, "Inbox")) ? "Inbox" : Main;

        public static bool Is(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- edits (each one reads, changes, writes)

        /// <summary>A new note at the top, stamped with the date and time.</summary>
        public void Add(string text)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (text == "") return;
            Load();
            Put(AddTo(), new List<string> { "- " + DateTime.Now.ToString(StampFormat) + " — " + text });
            Save();
        }

        /// <summary>Take one note (and its indented lines) out of the file.</summary>
        public void Remove(Item it)
        {
            Load();
            if (it.Line >= _lines.Count || !_lines[it.Line].StartsWith("- ")) return;
            int n = 1;
            for (int j = it.Line + 1; j < _lines.Count && (_lines[j].StartsWith("  ") || _lines[j].StartsWith("\t")); j++) n++;
            _lines.RemoveRange(it.Line, n);
            Save();
        }

        /// <summary>The fresh slate: the file's intro (or the standard one) and an empty <c>## Notes</c>. Every other
        /// section and note goes; the caller backs the file up first.</summary>
        public void Wipe(string appName)
        {
            Load();
            int h = _lines.FindIndex(l => l.Trim().StartsWith("## "));
            var keep = h < 0 ? _lines.ToList() : _lines.Take(h).ToList();
            while (keep.Count > 0 && keep[^1].Trim() == "") keep.RemoveAt(keep.Count - 1);
            if (keep.Count == 0 || !keep[0].TrimStart().StartsWith("# ")) keep.InsertRange(0, new[] { "# NOTES — " + appName, "", Intro });
            // an old shared-notepad intro is replaced with the 5.0.0 one
            if (!keep.Any(l => l.Contains("Only Aaron writes here"))) { keep = keep.Take(1).ToList(); keep.Add(""); keep.AddRange(Intro.Split('\n')); }
            keep.Add(""); keep.Add("## " + Main); keep.Add("");
            _lines = keep;
            Save();
        }

        /// <summary>Tick: the line moves to the top of Done with today's date. Untick from Done: back to the Inbox.</summary>
        public void Toggle(Item it)
        {
            Load();
            if (it.Line >= _lines.Count || !_lines[it.Line].StartsWith("- ")) return;
            var line = _lines[it.Line];
            var body = line.Substring(2);
            if (body.StartsWith("[ ]") || body.StartsWith("[x]") || body.StartsWith("[X]")) body = body.Substring(3).TrimStart();
            var block = new List<string> { line };   // the item plus any indented lines under it
            for (int j = it.Line + 1; j < _lines.Count && (_lines[j].StartsWith("  ") || _lines[j].StartsWith("\t")); j++) block.Add(_lines[j]);
            _lines.RemoveRange(it.Line, block.Count);
            if (!it.Done)
            {
                var stamp = " — done " + DateTime.Now.ToString("yyyy-MM-dd");
                block[0] = "- [x] " + body + (body.Contains(" — done ") ? "" : stamp);
                Put("Done", block);
            }
            else
            {
                int k = body.LastIndexOf(" — done ", StringComparison.Ordinal);
                block[0] = "- [ ] " + (k > 0 ? body.Substring(0, k) : body);
                Put(AddTo(), block);
            }
            Save();
        }

        /// <summary>Move a line (and its indented lines) to the top of another section.</summary>
        public void Move(Item it, string section)
        {
            Load();
            if (it.Line >= _lines.Count) return;
            var block = new List<string> { _lines[it.Line] };
            for (int j = it.Line + 1; j < _lines.Count && (_lines[j].StartsWith("  ") || _lines[j].StartsWith("\t")); j++) block.Add(_lines[j]);
            _lines.RemoveRange(it.Line, block.Count);
            Put(section, block);
            Save();
        }

        public void SaveRaw(string text)
        {
            _nl = text.Contains("\r\n") ? "\r\n" : _nl;
            _lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();
            Save();
        }

        /// <summary>A fresh NOTES.md, as the principles' template has it.</summary>
        public void Create(string appName)
        {
            if (Exists) return;
            _nl = "\n";
            _lines = new List<string> { "# NOTES — " + appName, "" };
            _lines.AddRange(Intro.Split('\n'));
            _lines.Add(""); _lines.Add("## " + Main); _lines.Add("");
            Save();
        }

        /// <summary>Put lines at the top of a section: under its heading, its blank line and any comment (the section is
        /// made at the end if it's missing). Keeps a blank line before the next heading.</summary>
        private void Put(string section, List<string> block)
        {
            int h = _lines.FindIndex(l => l.Trim().StartsWith("## ") && Is(l.Trim().Substring(3), section));
            if (h < 0)
            {
                if (_lines.Count > 0 && _lines[^1].Trim() != "") _lines.Add("");
                _lines.Add("## " + section); _lines.Add("");
                h = _lines.Count - 2;
            }
            int at = h + 1;
            if (at < _lines.Count && _lines[at].Trim() == "") at++;
            if (at < _lines.Count && _lines[at].TrimStart().StartsWith("<!--"))
            {
                while (at < _lines.Count && !_lines[at].Contains("-->")) at++;
                at = Math.Min(at + 1, _lines.Count);
            }
            _lines.InsertRange(at, block);
            int after = at + block.Count;
            if (after < _lines.Count && _lines[after].Trim().StartsWith("## ")) _lines.Insert(after, "");
        }

        private void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, Raw, new UTF8Encoding(false));
            File.Move(tmp, Path, true);
        }

        // ---------------------------------------------------------------- GitHub (hidden git, NOTES.md only)

        /// <summary>Where NOTES.md stands against GitHub (after the last fetch).</summary>
        public enum SyncKind { NoRepo, NoUpstream, Synced, Unsynced, Incoming, Both }

        public sealed class SyncState
        {
            public SyncKind Kind;
            /// <summary>NOTES.md has edits not committed yet.</summary>
            public bool Dirty;
            /// <summary>Commits here that GitHub doesn't have (any file).</summary>
            public int Ahead;
            /// <summary>Commits on GitHub we don't have (any file).</summary>
            public int Behind;
            /// <summary>Every commit GitHub has and we don't touches only NOTES.md (safe to bring in).</summary>
            public bool IncomingNotesOnly;
            /// <summary>Every commit we have and GitHub doesn't touches only NOTES.md (safe to push).</summary>
            public bool OutgoingNotesOnly;
            public DateTime When = DateTime.Now;
        }

        private static readonly string GitPath = RelPath.Replace('\\', '/');

        /// <summary>Where we stand. <paramref name="fetch"/> asks GitHub first (network); otherwise uses the last fetch.</summary>
        public SyncState Status(bool fetch)
        {
            var st = new SyncState();
            if (Git("rev-parse --is-inside-work-tree").code != 0) { st.Kind = SyncKind.NoRepo; return st; }
            st.Dirty = Git("status --porcelain -- \"" + GitPath + "\"").output.Trim() != "";
            if (Git("rev-parse --abbrev-ref --symbolic-full-name @{u}").code != 0) { st.Kind = SyncKind.NoUpstream; return st; }
            if (fetch) Git("fetch --quiet", 30000);
            var lr = Git("rev-list --left-right --count @{u}...HEAD").output.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (lr.Length == 2) { int.TryParse(lr[0], out st.Behind); int.TryParse(lr[1], out st.Ahead); }
            st.IncomingNotesOnly = st.Behind > 0 && OnlyNotes(Git("diff --name-only HEAD...@{u}").output);
            st.OutgoingNotesOnly = st.Ahead == 0 || OnlyNotes(Git("log @{u}..HEAD --name-only --format=").output);
            bool outNotes = st.Dirty || (st.Ahead > 0 && st.OutgoingNotesOnly) || (st.Ahead > 0 && Git("log @{u}..HEAD --format=%h -- \"" + GitPath + "\"").output.Trim() != "");
            bool inNotes = st.Behind > 0 && Git("log HEAD..@{u} --format=%h -- \"" + GitPath + "\"").output.Trim() != "";
            st.Kind = outNotes && inNotes ? SyncKind.Both : outNotes ? SyncKind.Unsynced : inNotes ? SyncKind.Incoming : SyncKind.Synced;
            return st;
        }

        private static bool OnlyNotes(string names)
        {
            var files = (names ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();
            return files.Count > 0 && files.All(f => string.Equals(f, GitPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// SYNC (the button, or automatic after edits). Only ever about NOTES.md:
        /// 1. commit NOTES.md alone if it changed (message: what happened + where + when, so it's readable and unique);
        /// 2. fetch; bring in GitHub's new commits only if they touch nothing but NOTES.md (fast-forward, or a merge
        ///    when both sides moved; a conflict is aborted and reported, nothing lost);
        /// 3. push only if every commit we'd send touches nothing but NOTES.md, so a chat's unpushed work never
        ///    leaves by accident (the notes commit then rides along with that chat's own push).
        /// </summary>
        public Task<(bool ok, string say)> Sync(string what, bool auto) => Task.Run(() =>
        {
            if (Git("rev-parse --is-inside-work-tree").code != 0) return (false, "NOT A GIT REPO  //  NOTES ARE SAVED ON DISK ONLY");
            foreach (var busy in new[] { "MERGE_HEAD", "rebase-merge", "rebase-apply" })
                if (Git("rev-parse --git-path " + busy).output.Trim() is string gp && gp != "" && (File.Exists(System.IO.Path.Combine(Folder, gp)) || Directory.Exists(System.IO.Path.Combine(Folder, gp))))
                    return (false, "THE REPO IS IN THE MIDDLE OF A MERGE OR REBASE  //  NOTES WAIT");
            bool committed = false;
            if (Git("status --porcelain -- \"" + GitPath + "\"").output.Trim() != "")
            {
                var msg = "Notes: " + (string.IsNullOrWhiteSpace(what) ? "updated" : what) + " (Catalyzer, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")";
                var c = Git("commit -m \"" + msg.Replace("\"", "'") + "\" -- \"" + GitPath + "\"");
                if (c.code != 0) return (false, "COMMIT FAILED  //  " + First(c.output));
                committed = true;
            }
            if (Git("rev-parse --abbrev-ref --symbolic-full-name @{u}").code != 0)
                return (true, committed ? "NOTES COMMITTED  //  THIS BRANCH ISN'T ON GITHUB, SO NOTHING WAS PUSHED" : "NOTHING TO SYNC  //  NOT ON GITHUB");
            var f = Git("fetch --quiet", 30000);
            if (f.code != 0) return (committed, (committed ? "NOTES COMMITTED, " : "") + "GITHUB UNREACHABLE  //  " + First(f.output));
            var st = Status(fetch: false);
            string pulled = "";
            if (st.Behind > 0)
            {
                if (!st.IncomingNotesOnly)
                    pulled = "GITHUB ALSO HAS OTHER NEW WORK: PULL IT IN THE PROJECT'S CHAT";
                else if (st.Ahead == 0)
                {
                    var m = Git("merge --ff-only @{u}");
                    if (m.code != 0) return (false, "COULDN'T BRING IN NEW NOTES  //  " + First(m.output));
                    pulled = "NEW NOTES IN";
                }
                else
                {
                    var m = Git("merge --no-edit @{u}");
                    if (m.code != 0) { Git("merge --abort"); return (false, "NOTES CHANGED HERE AND ON GITHUB IN THE SAME LINES  //  OPEN RAW AND MERGE BY HAND, OR ASK A CHAT"); }
                    pulled = "NEW NOTES MERGED IN";
                }
                st = Status(fetch: false);
            }
            if (st.Ahead == 0) return (true, Join(committed ? "NOTES COMMITTED" : null, pulled, "UP TO DATE"));
            if (!st.OutgoingNotesOnly) return (true, Join(committed ? "NOTES COMMITTED HERE" : null, pulled, "NOTES WAIT FOR " + st.Ahead + " UNPUSHED COMMIT(S)  //  PUSH ON THE CARD'S GIT CHIP AND THEY GO TOO"));
            var p = Git("push --quiet", 60000);
            if (p.code != 0) return (false, "PUSH FAILED  //  " + First(p.output));
            return (true, Join(auto ? "NOTES AUTO-SYNCED" : "NOTES SYNCED", pulled, "PUSHED " + DateTime.Now.ToString("HH:mm")));
        });

        /// <summary>Opening the notes: bring in notes from elsewhere (phone, another PC) if that's all GitHub has.</summary>
        public Task<string> PullIfSafe() => Task.Run(() =>
        {
            var st = Status(fetch: true);
            if (st.Kind == SyncKind.NoRepo || st.Kind == SyncKind.NoUpstream || st.Behind == 0 || !st.IncomingNotesOnly) return (string)null;
            if (st.Dirty) return "GITHUB HAS NEWER NOTES  //  SYNC TO BRING THEM IN WITH YOURS";
            var m = st.Ahead == 0 ? Git("merge --ff-only @{u}") : Git("merge --no-edit @{u}");
            if (m.code != 0) { if (st.Ahead > 0) Git("merge --abort"); return "COULDN'T BRING IN NEWER NOTES  //  " + First(m.output); }
            return "NEWER NOTES FROM GITHUB ARE IN";
        });

        private static string Join(params string[] parts) => string.Join("  //  ", parts.Where(x => !string.IsNullOrEmpty(x)));

        private static string First(string s) => (s ?? "").Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0 && !x.StartsWith("hint:")) ?? "";

        private (int code, string output) Git(string args, int ms = 15000)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "-C \"" + Folder + "\" " + args)
                {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                };
                psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(ms)) { try { p.Kill(true); } catch { } return (124, "timed out"); }
                return (p.ExitCode, o.Result + e.Result);
            }
            catch (Exception ex) { return (127, ex.Message); }
        }
    }
}
