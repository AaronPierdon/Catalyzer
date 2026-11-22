using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
