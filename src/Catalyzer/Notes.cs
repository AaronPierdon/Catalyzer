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
    /// A project's docs/NOTES.md: the shared notepad of the Nexus Development Principles (2.0.0, 07-APP-REPO). Aaron,
    /// Claude and Catalyzer all write to it. Sections: Inbox / Next up / Ideas / Bugs / Done; items are "- [ ]" lines.
    /// Catalyzer only ever adds lines, flips a checkbox, or moves a line between sections. It never deletes your words,
    /// and it keeps whatever else is in the file exactly as it was.
    /// </summary>
    public sealed class Notes
    {
        public const string RelPath = "docs\\NOTES.md";
        public static readonly string[] Sections = { "Inbox", "Next up", "Ideas", "Bugs", "Done" };

        public sealed class Item { public string Section; public int Line; public string Text; public bool Done; public bool Check; }

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
                list.Add(new Item { Section = sec, Line = i, Check = check, Done = done, Text = (check ? body.Substring(3) : body).Trim() });
            }
            return list;
        }

        /// <summary>Open items in the sections that mean "to do", and how many are still waiting in the Inbox.</summary>
        public (int open, int inbox) Count()
        {
            var it = Items().Where(i => !i.Done && !Is(i.Section, "Done") && !Is(i.Section, "Ideas")).ToList();
            return (it.Count, it.Count(i => Is(i.Section, "Inbox")));
        }

        public static bool Is(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- edits (each one reads, changes, writes)

        /// <summary>A new note at the top of the Inbox, marked as Aaron's.</summary>
        public void Add(string text)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (text == "") return;
            Load();
            Put("Inbox", new List<string> { "- [ ] " + text + " (Aaron, " + DateTime.Now.ToString("yyyy-MM-dd") + ")" });
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
                Put("Inbox", block);
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

        /// <summary>A fresh NOTES.md from the principles' template (the headings every tool expects).</summary>
        public void Create(string appName)
        {
            if (Exists) return;
            _nl = "\n";
            _lines = new List<string>
            {
                "# NOTES — " + appName, "",
                "The shared notepad: Aaron, Claude and Catalyzer's notes panel all write here. New things go in *Inbox*; chats sort",
                "them into the other sections. Never delete Aaron's words. Done items move to *Done* with a date.", "",
            };
            foreach (var s in Sections) { _lines.Add("## " + s); _lines.Add(""); }
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

        // ---------------------------------------------------------------- GitHub (one click, hidden git)

        /// <summary>
        /// SYNC: commit NOTES.md if it changed, pull (rebase, autostash) so notes written elsewhere arrive, push. Only
        /// NOTES.md is ever committed: anything else in progress in the repo is left alone.
        /// </summary>
        public Task<(bool ok, string say)> Sync() => Task.Run(() =>
        {
            if (Git("rev-parse --is-inside-work-tree").code != 0) return (false, "NOT A GIT REPO  //  NOTES SAVED TO DISK ONLY");
            var changed = Git("status --porcelain -- \"" + RelPath.Replace('\\', '/') + "\"").output.Trim() != "";
            if (changed)
            {
                var add = Git("add -- \"" + RelPath.Replace('\\', '/') + "\"");
                if (add.code != 0) return (false, "GIT ADD FAILED  //  " + First(add.output));
                var c = Git("commit -m \"Notes: updated from Catalyzer\" -- \"" + RelPath.Replace('\\', '/') + "\"");
                if (c.code != 0) return (false, "COMMIT FAILED  //  " + First(c.output));
            }
            if (Git("rev-parse --abbrev-ref --symbolic-full-name @{u}").code != 0)
                return (true, changed ? "COMMITTED  //  THIS BRANCH HAS NO UPSTREAM, SO NOTHING WAS PUSHED" : "NOTHING TO SYNC  //  NO UPSTREAM");
            var pull = Git("pull --rebase --autostash", 60000);
            if (pull.code != 0) { Git("rebase --abort"); return (false, "PULL FAILED  //  " + First(pull.output) + "  //  NOTES ARE SAFE ON DISK"); }
            var push = Git("push", 60000);
            if (push.code != 0) return (false, "PUSH FAILED  //  " + First(push.output));
            return (true, changed ? "NOTES SYNCED  //  COMMITTED, PULLED, PUSHED" : "NOTES UP TO DATE  //  PULLED, PUSHED");
        });

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
