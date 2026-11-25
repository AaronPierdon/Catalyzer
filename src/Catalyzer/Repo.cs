using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catalyzer
{
    /// <summary>
    /// Git for the whole repo, the safe one-click cases only (Catalyzer is a hub, not a git client):
    /// where a repo stands against GitHub, what to do next, and pull / push / commit when that's safe.
    /// - <b>Pull</b> is fetch + fast-forward only, never with uncommitted tracked changes.
    /// - <b>Push</b> never forces, never runs while GitHub is ahead, and fetches again first.
    /// - <b>Commit</b> commits exactly the files chosen, nothing else that happens to be staged.
    /// Anything odd (a conflict, a merge or rebase in progress, diverged branches, a detached HEAD, git busy, a link
    /// request running) blocks the buttons and points to Visual Studio's Git window.
    /// </summary>
    public sealed class Repo
    {
        public enum Kind { NotRepo, Busy, Conflict, Detached, Gone, Diverged, Pull, Push, Changes, NoRemote, NoUpstream, InStep }

        public sealed class Change
        {
            public string Path;
            /// <summary>The two-letter porcelain code ("M ", " M", "??", "UU"...).</summary>
            public string Code;
            public bool Untracked => Code == "??";
            public bool Conflict;
            /// <summary>Why it shouldn't go in without a look (a secret, a big file, scratch, only line endings), or null.</summary>
            public string Risk;
            /// <summary>What the drawer pre-ticks: tracked and not risky.</summary>
            public bool Suggested => !Untracked && Risk == null && !Conflict;
            public string What => Conflict ? "CONFLICT" : Untracked ? "NEW" : Code.Contains('D') ? "DELETED" : Code.Contains('R') ? "RENAMED" : Code.Contains('A') ? "ADDED" : "CHANGED";
        }

        public sealed class Commit { public string Hash, Subject, When; }

        public sealed class State
        {
            public Kind Kind = Kind.NotRepo;
            public string Branch, Upstream, InProgress;
            public bool HasOrigin, HasUpstream;
            public int Ahead, Behind;
            public List<Change> Changes = new List<Change>();
            public List<Commit> Incoming = new List<Commit>(), Outgoing = new List<Commit>();
            /// <summary>When GitHub was last asked (FETCH_HEAD), or null.</summary>
            public DateTime? Fetched;
            public DateTime When = DateTime.Now;
            /// <summary>A chat (the link) or Catalyzer itself is running something in this repo.</summary>
            public string LockedBy;
            public bool CanPull, CanPush, CanCommit;
            /// <summary>One line: where things stand and what to do.</summary>
            public string Why = "";
            /// <summary>Why pull / push are off (shown as the button's tip).</summary>
            public string PullBlock, PushBlock;
            public int Tracked => Changes.Count(c => !c.Untracked);
        }

        public string Folder { get; }
        public Repo(string folder) { Folder = folder; }

        // ================================================================== reading

        /// <summary>Where the repo stands. <paramref name="fetch"/> asks GitHub first (network). <paramref name="lockedBy"/>
        /// is set by the caller when something else is running in the repo.</summary>
        public State Read(bool fetch, string lockedBy = null)
        {
            var s = new State { LockedBy = lockedBy ?? LinkBusy() };
            if (Git("rev-parse --is-inside-work-tree").output.Trim() != "true") { s.Kind = Kind.NotRepo; s.Why = "NOT A GIT REPO"; return s; }
            s.HasOrigin = Git("remote").output.Split('\n').Any(r => r.Trim() == "origin");
            if (File.Exists(GitPath("index.lock"))) { s.Kind = Kind.Busy; s.Why = "GIT IS BUSY (INDEX.LOCK)  //  SOMETHING ELSE IS USING THIS REPO"; return Finish(s); }
            if (fetch && s.HasOrigin) Git("fetch --quiet origin", 45000);
            var fh = GitPath("FETCH_HEAD"); if (File.Exists(fh)) s.Fetched = File.GetLastWriteTime(fh);

            foreach (var (file, name) in new[] { ("MERGE_HEAD", "A MERGE"), ("rebase-merge", "A REBASE"), ("rebase-apply", "A REBASE"), ("CHERRY_PICK_HEAD", "A CHERRY-PICK"), ("REVERT_HEAD", "A REVERT"), ("BISECT_LOG", "A BISECT") })
                if (File.Exists(GitPath(file)) || Directory.Exists(GitPath(file))) { s.InProgress = name; break; }

            var st = Git("status --porcelain=v1 -b -z --untracked-files=all");
            var parts = st.output.Split('\0');
            for (int i = 0; i < parts.Length; i++)
            {
                var e = parts[i]; if (e.Length < 3) continue;
                if (e.StartsWith("## ")) { Head(e.Substring(3), s); continue; }
                var code = e.Substring(0, 2); var path = e.Substring(3);
                if (code[0] == 'R' || code[0] == 'C') i++;   // -z: the rename's source path follows
                bool conflict = code == "DD" || code == "AU" || code == "UD" || code == "UA" || code == "DU" || code == "AA" || code == "UU";
                s.Changes.Add(new Change { Path = path, Code = code, Conflict = conflict });
            }
            if (Git("symbolic-ref -q HEAD").code != 0 && s.InProgress == null) s.Branch = null;
            Risks(s);
            if (s.HasUpstream)
            {
                if (s.Behind > 0) s.Incoming = Log("HEAD..@{u}");
                if (s.Ahead > 0) s.Outgoing = Log("@{u}..HEAD");
            }
            else if (s.Branch != null) s.Outgoing = Log("HEAD --not --remotes", 20);
            return Finish(s);
        }

        private void Head(string h, State s)
        {
            // "main...origin/main [ahead 1, behind 2]" · "main" · "No commits yet on main" · "HEAD (no branch)" · "main...origin/main [gone]"
            if (h.StartsWith("No commits yet on ")) { s.Branch = h.Substring(18).Split(new[] { "...", " " }, StringSplitOptions.None)[0]; return; }
            if (h.StartsWith("HEAD (no branch)")) { s.Branch = null; return; }
            var sp = h.Split(new[] { "..." }, 2, StringSplitOptions.None);
            s.Branch = sp[0].Split(' ')[0];
            if (sp.Length < 2) return;
            var rest = sp[1];
            s.Upstream = rest.Split(' ')[0];
            s.HasUpstream = !rest.Contains("[gone]");
            if (!s.HasUpstream) s.Upstream += " (GONE)";
            s.Ahead = Num(rest, "ahead "); s.Behind = Num(rest, "behind ");
        }

        private static readonly string[] SecretNames = { ".env", "id_rsa", "id_ed25519", "credentials.json", "secrets.json", "appsettings.secrets.json" };
        private static readonly string[] SecretExt = { ".pfx", ".p12", ".pem", ".key", ".snk", ".publishsettings", ".keystore", ".jks" };
        public const long BigFile = 50L * 1024 * 1024;

        private void Risks(State s)
        {
            int eolChecks = 0;
            foreach (var c in s.Changes)
            {
                if (c.Conflict) continue;
                var name = Path.GetFileName(c.Path).ToLowerInvariant();
                var full = Path.Combine(Folder, c.Path.Replace('/', Path.DirectorySeparatorChar));
                if (c.Path.StartsWith("_devSpace/", StringComparison.OrdinalIgnoreCase)) c.Risk = "SCRATCH (_DEVSPACE)";
                else if (SecretNames.Contains(name) || name.StartsWith(".env.") || SecretExt.Any(x => name.EndsWith(x)) || name.Contains("secret") || name.Contains("password")) c.Risk = "MIGHT HOLD A SECRET";
                else if (File.Exists(full) && new FileInfo(full).Length > BigFile) c.Risk = "BIG FILE (" + (new FileInfo(full).Length >> 20) + " MB)";
                else if (c.Code == " M" && eolChecks++ < 150 && Git("diff --ignore-cr-at-eol --quiet -- \"" + c.Path + "\"").code == 0) c.Risk = "ONLY LINE ENDINGS CHANGED";
            }
        }

        private State Finish(State s)
        {
            bool tracked = s.Tracked > 0;
            bool conflict = s.InProgress != null || s.Changes.Any(c => c.Conflict);
            string pullNo = null, pushNo = null;
            if (s.Kind == Kind.Busy) { pullNo = pushNo = "GIT IS BUSY"; }
            else if (conflict) { s.Kind = Kind.Conflict; s.Why = (s.InProgress != null ? s.InProgress + " IS IN PROGRESS" : "CONFLICTS") + "  //  FINISH IT IN VISUAL STUDIO'S GIT WINDOW"; pullNo = pushNo = "RESOLVE THE CONFLICT FIRST"; }
            else if (s.Branch == null) { s.Kind = Kind.Detached; s.Why = "NOT ON A BRANCH (DETACHED HEAD)  //  PICK A BRANCH IN VISUAL STUDIO"; pullNo = pushNo = "NOT ON A BRANCH"; }
            else if (!s.HasOrigin) { s.Kind = tracked || s.Changes.Count > 0 ? Kind.Changes : Kind.NoRemote; s.Why = "NOT ON GITHUB  //  PUBLISH IT FROM THE GITHUB PANEL"; pullNo = pushNo = "NO GITHUB REMOTE"; }
            else if (s.Upstream != null && !s.HasUpstream && s.Outgoing.Count == 0)
            {
                // GitHub had this branch and deleted it (say, merged), and nothing here is new: pushing would bring it back
                s.Kind = Kind.Gone; s.Why = "THIS BRANCH WAS DELETED ON GITHUB  //  SWITCH BRANCH IN VISUAL STUDIO"; pullNo = pushNo = "THE BRANCH IS GONE ON GITHUB";
            }
            else if (!s.HasUpstream)
            {
                s.Kind = s.Changes.Count > 0 ? Kind.Changes : Kind.NoUpstream;
                s.Why = "BRANCH " + s.Branch.ToUpperInvariant() + " ISN'T ON GITHUB YET  //  PUSH PUBLISHES IT";
                pullNo = "NOTHING TO PULL: THE BRANCH ISN'T ON GITHUB";
            }
            else if (s.Ahead > 0 && s.Behind > 0)
            {
                s.Kind = Kind.Diverged; s.Why = "BOTH SIDES MOVED (" + s.Ahead + " HERE, " + s.Behind + " ON GITHUB)  //  MERGE IN VISUAL STUDIO'S GIT WINDOW";
                pullNo = pushNo = "DIVERGED: MERGE IN VISUAL STUDIO";
            }
            else if (s.Behind > 0)
            {
                s.Kind = Kind.Pull;
                s.Why = s.Behind + " NEW ON GITHUB" + (tracked ? "  //  COMMIT OR UNDO YOUR " + s.Tracked + " CHANGE(S) FIRST, THEN PULL" : "  //  PULL");
                pushNo = "GITHUB IS AHEAD: PULL FIRST";
                if (tracked) pullNo = "UNCOMMITTED CHANGES: COMMIT THEM FIRST";
            }
            else if (s.Ahead > 0) { s.Kind = Kind.Push; s.Why = s.Ahead + " TO PUSH" + (s.Changes.Count > 0 ? "  //  " + s.Changes.Count + " CHANGE(S) STAY LOCAL" : "  //  PUSH"); pullNo = "NOTHING TO PULL"; }
            else if (s.Changes.Count > 0) { s.Kind = Kind.Changes; s.Why = s.Changes.Count + " CHANGE(S) NOT COMMITTED  //  COMMIT"; pullNo = "NOTHING TO PULL"; pushNo = "NOTHING TO PUSH: COMMIT FIRST"; }
            else { s.Kind = Kind.InStep; s.Why = "IN STEP WITH GITHUB"; pullNo = "NOTHING TO PULL"; pushNo = "NOTHING TO PUSH"; }

            if (s.Kind == Kind.NoUpstream || (s.Kind == Kind.Changes && !s.HasUpstream && s.HasOrigin && s.Branch != null && !conflict))
                pushNo = s.Outgoing.Count == 0 ? "NOTHING TO PUSH: COMMIT FIRST" : null;
            if (s.Kind == Kind.Changes && s.HasUpstream && s.Behind == 0 && s.Ahead == 0) pushNo = "NOTHING TO PUSH: COMMIT FIRST";
            if (s.LockedBy != null) { pullNo = s.LockedBy; pushNo = s.LockedBy; }

            s.PullBlock = pullNo; s.PushBlock = pushNo;
            s.CanPull = pullNo == null && s.Kind == Kind.Pull;
            s.CanPush = pushNo == null && s.Branch != null && s.HasOrigin;
            s.CanCommit = s.Changes.Count > 0 && s.Kind != Kind.Busy && !conflict && s.Branch != null && s.LockedBy == null;
            return s;
        }

        /// <summary>A link request still running in this repo (a chat is working), as a block reason, or null.</summary>
        public string LinkBusy()
        {
            try
            {
                var outDir = Path.Combine(Folder, ".catalyzer", "link", "out");
                if (!Directory.Exists(outDir)) return null;
                foreach (var log in Directory.GetFiles(outDir, "*.log"))
                    if (!File.Exists(Path.ChangeExtension(log, ".done")) && DateTime.Now - File.GetLastWriteTime(log) < TimeSpan.FromHours(2))
                        return "A CHAT IS RUNNING SOMETHING THROUGH THE LINK  //  WAIT FOR IT";
            }
            catch { }
            return null;
        }

        private List<Commit> Log(string range, int max = 30)
        {
            var o = Git("log " + range + " -n " + max + " --format=%h%x1f%s%x1f%cr").output;
            return o.Split('\n').Where(l => l.Contains('\x1f')).Select(l => l.Split('\x1f')).Select(f => new Commit { Hash = f[0], Subject = f[1], When = f.Length > 2 ? f[2] : "" }).ToList();
        }

        // ================================================================== doing (each one re-reads first and refuses if it isn't safe)

        /// <summary>Fetch, then fast-forward to GitHub. Refuses anything but the plain "GitHub is ahead, I'm clean" case.</summary>
        public Task<(bool ok, string say)> Pull() => Task.Run(() =>
        {
            var s = Read(fetch: true);
            if (!s.CanPull) return (false, "NOT PULLED  //  " + (s.PullBlock ?? s.Why));
            var m = Git("merge --ff-only --quiet @{u}", 60000);
            if (m.code != 0) return (false, "PULL STOPPED, NOTHING CHANGED  //  " + First(m.output));
            return (true, "PULLED " + s.Behind + " COMMIT(S)");
        });

        /// <summary>Fetch, then push what's ahead (publishing the branch if GitHub doesn't have it). Never forces.</summary>
        public Task<(bool ok, string say)> Push() => Task.Run(() =>
        {
            var s = Read(fetch: true);
            if (!s.CanPush) return (false, "NOT PUSHED  //  " + (s.PushBlock ?? s.Why));
            if (s.HasUpstream)
            {
                var p = Git("push --quiet", 90000);
                return p.code == 0 ? (true, "PUSHED " + s.Ahead + " COMMIT(S)") : (false, "PUSH FAILED  //  " + First(p.output));
            }
            // a new branch: only if GitHub really doesn't have one by that name (never overwrite)
            var ls = Git("ls-remote --heads origin \"" + s.Branch + "\"", 30000);
            if (ls.code != 0) return (false, "GITHUB UNREACHABLE  //  " + First(ls.output));
            if (ls.output.Trim() != "") return (false, "NOT PUSHED  //  GITHUB ALREADY HAS A BRANCH CALLED " + s.Branch.ToUpperInvariant() + ": SORT IT OUT IN VISUAL STUDIO");
            var pu = Git("push --quiet -u origin \"" + s.Branch + "\"", 90000);
            return pu.code == 0 ? (true, "PUBLISHED BRANCH " + s.Branch.ToUpperInvariant() + " TO GITHUB") : (false, "PUSH FAILED  //  " + First(pu.output));
        });

        /// <summary>Commit exactly <paramref name="paths"/> (new, changed or deleted), whatever else is staged.</summary>
        public Task<(bool ok, string say)> CommitFiles(IList<string> paths, string message) => Task.Run(() =>
        {
            message = (message ?? "").Trim();
            if (message == "") return (false, "WRITE A COMMIT MESSAGE FIRST");
            if (paths == null || paths.Count == 0) return (false, "TICK THE FILES TO COMMIT FIRST");
            var s = Read(fetch: false);
            if (!s.CanCommit) return (false, "NOT COMMITTED  //  " + (s.LockedBy ?? s.Why));
            var known = new HashSet<string>(s.Changes.Select(c => c.Path), StringComparer.Ordinal);
            var list = paths.Where(known.Contains).Distinct().ToList();
            if (list.Count == 0) return (false, "THOSE FILES HAVE NO CHANGES ANY MORE  //  REFRESH");
            var tmp = Path.Combine(Path.GetTempPath(), "catalyzer-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(tmp + ".paths", string.Join("\0", list) + "\0", new UTF8Encoding(false));
                File.WriteAllText(tmp + ".msg", message + "\n", new UTF8Encoding(false));
                var a = Git("add -A --pathspec-from-file=\"" + tmp + ".paths\" --pathspec-file-nul");
                if (a.code != 0) return (false, "COULDN'T STAGE THE FILES  //  " + First(a.output));
                var c = Git("commit --quiet -F \"" + tmp + ".msg\" --pathspec-from-file=\"" + tmp + ".paths\" --pathspec-file-nul");
                if (c.code != 0) return (false, "COMMIT FAILED  //  " + First(c.output));
                return (true, "COMMITTED " + list.Count + " FILE(S)");
            }
            finally { try { File.Delete(tmp + ".paths"); File.Delete(tmp + ".msg"); } catch { } }
        });

        /// <summary>"Update Notes.cs, Prefs.cs and 3 more" from the chosen files (Aaron edits it).</summary>
        public static string SuggestMessage(IList<string> paths)
        {
            if (paths == null || paths.Count == 0) return "";
            var names = paths.Select(p => Path.GetFileName(p.TrimEnd('/'))).Distinct().ToList();
            var head = string.Join(", ", names.Take(3));
            return "Update " + head + (names.Count > 3 ? " and " + (names.Count - 3) + " more" : "");
        }

        // ================================================================== git

        private string GitPath(string name)
        {
            var r = Git("rev-parse --git-path \"" + (name == "" ? "." : name) + "\"").output.Trim();
            return r == "" ? Path.Combine(Folder, ".git", name) : Path.IsPathRooted(r) ? r : Path.Combine(Folder, r);
        }

        private static int Num(string s, string key)
        {
            int i = s.IndexOf(key, StringComparison.Ordinal);
            return i >= 0 && int.TryParse(new string(s.Substring(i + key.Length).TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0;
        }

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
                psi.Environment["GCM_INTERACTIVE"] = "never";
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(ms)) { try { p.Kill(true); } catch { } return (124, "timed out"); }
                return (p.ExitCode, o.Result + (p.ExitCode != 0 ? e.Result : ""));
            }
            catch (Exception ex) { return (127, ex.Message); }
        }
    }
}
