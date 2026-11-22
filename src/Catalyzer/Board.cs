using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>The projects on the board, rescanned on demand; git state arrives per project as it's read.</summary>
    public sealed class Board
    {
        public List<Project> Items { get; private set; } = new List<Project>();
        /// <summary>The list changed (cards rebuild). UI thread.</summary>
        public event Action Changed;
        /// <summary>One project's git state arrived. UI thread.</summary>
        public event Action<Project> GitLoaded;
        /// <summary>Plugins added card buttons. UI thread.</summary>
        public event Action ActionsChanged;
        public DateTime LastScan { get; private set; }
        public bool Scanning { get; private set; }

        public void Poke() => Application.Current?.Dispatcher.BeginInvoke(new Action(() => ActionsChanged?.Invoke()));

        public async void Rescan()
        {
            if (Scanning) return;
            Scanning = true;
            try
            {
                var sources = PluginHost.I?.Sources.ToList() ?? new List<IProjectSource>();
                var list = await Task.Run(() => Projects.Scan(sources));
                var order = Prefs.Value.Order;
                // your order first, then the most recently touched (the repo's index / HEAD log), then by name
                Items = list.OrderBy(p => { int i = order.FindIndex(o => string.Equals(o, p.Folder, StringComparison.OrdinalIgnoreCase)); return i < 0 ? int.MaxValue : i; })
                            .ThenByDescending(p => Touched(p.Folder)).ThenBy(p => p.Name).ToList();
                LastScan = DateTime.Now;
                Changed?.Invoke();
                Watch();
                Log.Write("scan: " + Items.Count + " projects");
                foreach (var p in Items.ToList())
                {
                    await Projects.LoadGit(p);
                    GitLoaded?.Invoke(p);
                }
            }
            catch (Exception ex) { Log.Write(ex); }
            finally { Scanning = false; }
        }

        /// <summary>Git again for every card (no rescan of folders).</summary>
        public async void RefreshGit()
        {
            foreach (var p in Items.ToList()) { await Projects.LoadGit(p); GitLoaded?.Invoke(p); }
        }

        // ------------------------------------------------------------------ watching for new projects
        // Cheap on purpose: each scan root is watched one level deep (a folder appears, goes or is renamed), and each
        // project only for its own catalyzer.json. Nothing recursive, so builds churning bin\obj never wake us.

        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private DispatcherTimer _settle;
        private static readonly HashSet<string> Ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "archives", "archive", "node_modules", "temp", "tmp" };

        private void Watch()
        {
            if (Prefs.Offline) return;
            foreach (var w in _watchers) { try { w.EnableRaisingEvents = false; w.Dispose(); } catch { } }
            _watchers.Clear();
            foreach (var root in Prefs.Value.Roots.Where(Directory.Exists))
                Add(root, null, NotifyFilters.DirectoryName, (e, old) =>
                {
                    bool Real(string path) { var n = Path.GetFileName(path ?? ""); return n.Length > 0 && !n.StartsWith(".") && !Ignore.Contains(n); }
                    return Real(e) || Real(old);
                });
            foreach (var p in Items.Where(p => Directory.Exists(p.Folder)))
                Add(p.Folder, Projects.ManifestName, NotifyFilters.FileName | NotifyFilters.LastWrite, (e, old) => true);
        }

        private void Add(string dir, string filter, NotifyFilters what, Func<string, string, bool> matters)
        {
            try
            {
                var w = new FileSystemWatcher(dir) { IncludeSubdirectories = false, NotifyFilter = what };
                if (filter != null) w.Filter = filter;
                FileSystemEventHandler on = (s, e) => { if (matters(e.FullPath, null)) Soon(); };
                w.Created += on; w.Deleted += on; w.Changed += on;
                w.Renamed += (s, e) => { if (matters(e.FullPath, e.OldFullPath)) Soon(); };
                w.Error += (s, e) => Log.Write("watch " + dir + ": " + e.GetException()?.Message);
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch (Exception ex) { Log.Write("watch " + dir + ": " + ex.Message); }
        }

        /// <summary>Something changed on disk: rescan once it has settled (a clone or a save fires a burst).</summary>
        private void Soon() => Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_settle == null)
            {
                _settle = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                _settle.Tick += (s, e) => { if (Scanning) return; _settle.Stop(); Log.Write("folders changed: rescan"); Rescan(); };
            }
            _settle.Stop(); _settle.Start();
        }));

        private static DateTime Touched(string dir)
        {
            try
            {
                var c = new[] { Path.Combine(dir, ".git", "index"), Path.Combine(dir, ".git", "logs", "HEAD"), Path.Combine(dir, Projects.ManifestName) };
                return c.Where(File.Exists).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(Directory.GetLastWriteTimeUtc(dir)).Max();
            }
            catch { return DateTime.MinValue; }
        }
    }
}
