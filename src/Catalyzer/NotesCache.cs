using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Catalyzer
{
    /// <summary>
    /// Backups of every project's docs/NOTES.md, so a lost note can be found again ("any notes between X and Y?").
    /// <c>%LOCALAPPDATA%\Catalyzer\notes-cache\&lt;name&gt;-&lt;hash&gt;\</c> holds one file per version Catalyzer
    /// saw, named by when (<c>2026-11-24_14-57-03_changed.md</c>), plus <c>project.txt</c> (the repo's folder). A version
    /// identical to the last one isn't saved again. Each project is capped (Settings, <see cref="PrefsData.NotesCacheMB"/>;
    /// the oldest go first, the newest always stays), and a project's backups go when it leaves the board.
    /// </summary>
    public static class NotesCache
    {
        public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Catalyzer", "notes-cache");
        private static readonly object Gate = new object();

        /// <summary>The backup folder for a project (not created).</summary>
        public static string DirFor(string folder)
        {
            var full = Path.GetFullPath(folder).TrimEnd('\\', '/');
            var name = new string(Path.GetFileName(full).Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_').ToArray());
            var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(full.ToLowerInvariant()))).Substring(0, 8).ToLowerInvariant();
            return Path.Combine(Root, (name == "" ? "project" : name) + "-" + hash);
        }

        /// <summary>Save the current NOTES.md if it differs from the last saved version. Never throws.</summary>
        public static void Snapshot(string folder, string reason)
        {
            int capMb = Prefs.Value.NotesCacheMB;
            if (capMb <= 0 || Prefs.Offline) return;
            try
            {
                var notes = Path.Combine(folder, Notes.RelPath);
                if (!File.Exists(notes)) return;
                var bytes = File.ReadAllBytes(notes);
                var sha = Convert.ToHexString(SHA256.HashData(bytes));
                lock (Gate)
                {
                    var dir = DirFor(folder);
                    Directory.CreateDirectory(dir);
                    var last = Path.Combine(dir, "last.sha");
                    if (File.Exists(last) && File.ReadAllText(last).Trim() == sha) return;
                    var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    var file = Path.Combine(dir, stamp + "_" + Safe(reason) + ".md");
                    for (int i = 2; File.Exists(file); i++) file = Path.Combine(dir, stamp + "_" + Safe(reason) + "-" + i + ".md");
                    File.WriteAllBytes(file, bytes);
                    File.WriteAllText(last, sha);
                    File.WriteAllText(Path.Combine(dir, "project.txt"), Path.GetFullPath(folder));
                    Prune(dir, capMb * 1024L * 1024L);
                }
            }
            catch (Exception ex) { Log.Write("notes cache " + folder + ": " + ex.Message); }
        }

        /// <summary>Oldest versions go until the folder fits the cap; the newest one always stays.</summary>
        private static void Prune(string dir, long cap)
        {
            var files = new DirectoryInfo(dir).GetFiles("*.md").OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
            long total = files.Sum(f => f.Length);
            for (int i = 0; i < files.Count - 1 && total > cap; i++) { total -= files[i].Length; files[i].Delete(); }
        }

        /// <summary>Delete the backups of every project <paramref name="keep"/> says no to (it left the board). Never throws.</summary>
        public static int Purge(Func<string, bool> keep)
        {
            int n = 0;
            try
            {
                if (!Directory.Exists(Root)) return 0;
                lock (Gate)
                    foreach (var dir in Directory.GetDirectories(Root))
                    {
                        var p = Path.Combine(dir, "project.txt");
                        if (!File.Exists(p)) continue;
                        var folder = File.ReadAllText(p).Trim();
                        if (folder == "" || keep(folder)) continue;
                        try { Directory.Delete(dir, true); n++; Log.Write("notes cache: removed the backups of " + folder + " (no longer on the board)"); }
                        catch (Exception ex) { Log.Write("notes cache purge " + dir + ": " + ex.Message); }
                    }
            }
            catch (Exception ex) { Log.Write("notes cache purge: " + ex.Message); }
            return n;
        }

        /// <summary>How much the cache holds in all.</summary>
        public static long Size()
        {
            try { return Directory.Exists(Root) ? new DirectoryInfo(Root).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0; }
            catch { return 0; }
        }

        private static string Safe(string s) => new string((s ?? "changed").Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '-').ToArray());
    }
}
