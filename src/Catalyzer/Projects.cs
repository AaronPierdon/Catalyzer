using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// Finds projects: every git repo (or folder with a catalyzer.json) directly under each root folder, plus folders
    /// you added and those plugins bring. A project describes itself in its own catalyzer.json (docs/MANIFEST.md) - so
    /// a new project only touches its own repo, never Catalyzer.
    /// </summary>
    public static class Projects
    {
        public const string ManifestName = "catalyzer.json";
        /// <summary>Never scanned (Aaron's archives are off limits).</summary>
        private static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "archives", "archive", "node_modules", "temp", "tmp" };

        public static List<Project> Scan(IEnumerable<IProjectSource> sources)
        {
            var p = Prefs.Value;
            var folders = new List<(string dir, string origin)>();
            foreach (var root in p.Roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var d in Directory.GetDirectories(root))
                    {
                        var name = Path.GetFileName(d);
                        if (Skip.Contains(name) || name.StartsWith(".")) continue;
                        if (Directory.Exists(Path.Combine(d, ".git")) || File.Exists(Path.Combine(d, ".git")) || File.Exists(Path.Combine(d, ManifestName))) folders.Add((d, "scan"));
                    }
                }
                catch (Exception ex) { Log.Write("scan " + root + ": " + ex.Message); }
            }
            foreach (var d in p.Extra) if (Directory.Exists(d)) folders.Add((d, "manual"));
            foreach (var s in sources)
            {
                try { foreach (var d in s.Folders() ?? Enumerable.Empty<string>()) if (Directory.Exists(d)) folders.Add((d, s.Name)); }
                catch (Exception ex) { Log.Write("source " + s.Name + ": " + ex.Message); }
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<Project>();
            foreach (var (dir, origin) in folders)
            {
                var full = Path.GetFullPath(dir).TrimEnd('\\');
                if (!seen.Add(full) || p.Hidden.Contains(full, StringComparer.OrdinalIgnoreCase)) continue;
                var pr = Read(full); pr.Origin = origin;
                if (pr.Extra.TryGetValue("hidden", out var h) && h == "true") continue;
                list.Add(pr);
            }
            return list;
        }

        /// <summary>A project from its folder + catalyzer.json (everything optional).</summary>
        public static Project Read(string dir)
        {
            var pr = new Project { Id = dir.ToLowerInvariant(), Folder = dir, Name = Path.GetFileName(dir) };
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var mf = Path.Combine(dir, ManifestName);
            if (File.Exists(mf))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(mf), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    foreach (var prop in doc.RootElement.EnumerateObject().Where(x => x.Value.ValueKind != JsonValueKind.Null))
                        extra[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();
                    pr.HasManifest = true;
                }
                catch (Exception ex) { Log.Write("manifest " + mf + ": " + ex.Message, show: true); }
            }
            string X(string k) => extra.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
            pr.Name = X("name") ?? pr.Name;
            pr.Description = X("description") ?? "";
            pr.GitHub = X("github");
            pr.Glyph = X("glyph");
            if (X("accent") is string a) try { pr.Accent = (Color)ColorConverter.ConvertFromString(a); } catch { }
            pr.Solution = X("solution") is string sln ? Path.Combine(dir, sln) : FindSolution(dir);
            pr.Extra = extra;
            return pr;
        }

        private static string FindSolution(string dir)
        {
            try
            {
                var s = Directory.GetFiles(dir, "*.sln").Concat(Directory.GetFiles(dir, "*.slnx")).OrderBy(f => Path.GetFileNameWithoutExtension(f).Equals(Path.GetFileName(dir), StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
                return s;
            }
            catch { return null; }
        }

        // ------------------------------------------------------------------ git (git.exe, hidden, never a console window)

        public static Task LoadGit(Project p) => Task.Run(() =>
        {
            var g = new GitState();
            try
            {
                if (Run(p.Folder, "rev-parse --is-inside-work-tree") is not string inside || inside.Trim() != "true") { g.Loaded = true; p.Git = g; return; }
                g.IsRepo = true;
                // branch, ahead / behind, changed files in one go
                var st = Run(p.Folder, "status --porcelain=v1 -b") ?? "";
                var lines = st.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0 && lines[0].StartsWith("## "))
                {
                    var head = lines[0].Substring(3);
                    var br = head.Split(new[] { "...", " " }, StringSplitOptions.None)[0];
                    g.Branch = br.StartsWith("No commits yet on ") ? head.Substring(18) : br;
                    g.Ahead = Num(head, "ahead "); g.Behind = Num(head, "behind ");
                }
                g.Changed = lines.Count(l => !l.StartsWith("## "));
                var last = Run(p.Folder, "log -1 --format=%h%x1f%s%x1f%ct");
                if (!string.IsNullOrWhiteSpace(last))
                {
                    var f = last.Trim().Split('\x1f');
                    g.LastHash = f[0]; g.LastSubject = f.Length > 1 ? f[1] : "";
                    if (f.Length > 2 && long.TryParse(f[2], out var ts)) g.LastWhen = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
                }
                var remotes = Run(p.Folder, "remote -v") ?? "";
                g.Remote = remotes.Split('\n').Select(l => l.Split('\t', ' ')).Where(x => x.Length > 1).Select(x => x[1]).FirstOrDefault();
                // the last 21 days, a count per day (oldest first)
                var days = new int[21];
                var times = Run(p.Folder, "log --since=21.days --format=%ct") ?? "";
                var today = DateTime.Now.Date;
                foreach (var t in times.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    if (long.TryParse(t.Trim(), out var s))
                    {
                        int ago = (int)(today - DateTimeOffset.FromUnixTimeSeconds(s).LocalDateTime.Date).TotalDays;
                        if (ago >= 0 && ago < 21) days[20 - ago]++;
                    }
                g.Daily = days;
            }
            catch (Exception ex) { Log.Write("git " + p.Name + ": " + ex.Message); }
            g.Loaded = true;
            p.Git = g;
            if (p.GitHub == null && g.Remote != null) p.GitHub = WebUrl(g.Remote);
        });

        private static int Num(string s, string key)
        {
            int i = s.IndexOf(key, StringComparison.Ordinal);
            return i >= 0 && int.TryParse(new string(s.Substring(i + key.Length).TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0;
        }

        /// <summary>git@github.com:me/x.git / https://github.com/me/x.git -> https://github.com/me/x</summary>
        public static string WebUrl(string remote)
        {
            if (string.IsNullOrEmpty(remote)) return null;
            var r = remote.Trim();
            if (r.StartsWith("git@")) { r = r.Substring(4); int c = r.IndexOf(':'); if (c > 0) r = r.Substring(0, c) + "/" + r.Substring(c + 1); r = "https://" + r; }
            if (r.EndsWith(".git")) r = r.Substring(0, r.Length - 4);
            return r.StartsWith("http") ? r : null;
        }

        private static string Run(string dir, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "-C \"" + dir + "\" " + args)
                {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return null; }
                return p.ExitCode == 0 ? o.Result : null;
            }
            catch { return null; }
        }
    }
}
