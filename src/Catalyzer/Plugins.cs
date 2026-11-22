using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>A plugin Catalyzer found (on or off, loaded or not).</summary>
    public sealed class PluginInfo
    {
        public string Id, Name, Version, Author, Description, Folder, Dll;
        public bool On, Bundled, Running;
        public string Error;
        public ICatalyzerPlugin Instance;
    }

    /// <summary>
    /// Plugins: one folder per plugin, with its .dll and that .dll's own dependencies. Looked for in
    ///   &lt;install&gt;\plugins\*   (bundled, e.g. Nexus) and %APPDATA%\Catalyzer\plugins\*   (yours: Add / Import).
    /// They load into the app itself (not an isolated context): WPF resources such as fonts only resolve from there.
    /// Their dependencies resolve from their own folder; Catalyzer.Sdk always comes from Catalyzer. Switching one off or
    /// on takes effect at the next start (an assembly can't be unloaded from the app).
    /// </summary>
    public sealed class PluginHost : ICatalyzerHost
    {
        public static PluginHost I { get; private set; }
        public readonly List<PluginInfo> Plugins = new List<PluginInfo>();
        private readonly List<ProjectAction> _actions = new List<ProjectAction>();
        private readonly List<IProjectSource> _sources = new List<IProjectSource>();
        private readonly List<string> _probe = new List<string>();
        private readonly List<ProjectBadge> _badges = new List<ProjectBadge>();
        private readonly List<IBackgroundWork> _work = new List<IBackgroundWork>();
        public static readonly string UserFolder = Path.Combine(Prefs.Folder, "plugins");
        public static string BundledFolder => Path.Combine(AppContext.BaseDirectory, "plugins");

        public int ApiVersion => Api.Version;
        public string AppVersion => typeof(PluginHost).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        public Dispatcher Ui => Application.Current.Dispatcher;
        public IThemeService Themes => ThemeService.I;
        public IReadOnlyList<Project> Projects => Board.Items;
        public event Action ProjectsChanged;
        public IReadOnlyList<ProjectAction> Actions => _actions;
        public IReadOnlyList<IProjectSource> Sources => _sources;
        public Board Board { get; }
        public IReadOnlyList<ProjectBadge> Badges => _badges;
        public IReadOnlyList<IBackgroundWork> Work => _work;
        /// <summary>The badges should be read again (UI thread).</summary>
        public event Action BadgesChanged;
        /// <summary>Some background work started or stopped (UI thread).</summary>
        public event Action WorkChanged;
        public bool IsSnapshot => Prefs.Offline;
        /// <summary>Everything plugins keep running, added up.</summary>
        public int Running => _work.Sum(w => { try { return w.Running; } catch { return 0; } });

        public PluginHost(Board board)
        {
            I = this; Board = board;
            board.Changed += () => ProjectsChanged?.Invoke();
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        public void AddAction(ProjectAction a) { if (a != null && a.Run != null) { _actions.RemoveAll(x => x.Id == a.Id && a.Id != ""); _actions.Add(a); Board.Poke(); } }
        public void AddSource(IProjectSource s) { if (s != null) { _sources.Add(s); } }
        public void Refresh() => Board.Rescan();
        public string DataFolder(ICatalyzerPlugin p) { var d = Path.Combine(Prefs.Folder, "plugin-data", p?.Id ?? "unknown"); Directory.CreateDirectory(d); return d; }
        public void Log(ICatalyzerPlugin p, string m, bool show = false) => Catalyzer.Log.Write("[" + (p?.Name ?? "plugin") + "] " + m, show);

        public void AddBadge(ProjectBadge b) { if (b?.State != null) { _badges.RemoveAll(x => x.Id == b.Id && b.Id != ""); _badges.Add(b); UpdateBadges(); } }
        public void UpdateBadges() => Application.Current?.Dispatcher.BeginInvoke(new Action(() => BadgesChanged?.Invoke()));
        public void AddBackgroundWork(IBackgroundWork w)
        {
            if (w == null || _work.Contains(w)) return;
            _work.Add(w);
            w.Changed += () => Application.Current?.Dispatcher.BeginInvoke(new Action(() => WorkChanged?.Invoke()));
        }

        /// <summary>Stop every plugin's background work (closing, or "stop all").</summary>
        public void StopAllWork()
        {
            foreach (var w in _work.ToList())
                try { w.StopAll(); } catch (Exception ex) { Catalyzer.Log.Write("stop " + w.Name + ": " + ex.Message); }
        }

        /// <summary>Find every plugin; start the ones that are on.</summary>
        public void LoadAll()
        {
            Plugins.Clear();
            foreach (var (root, bundled) in new[] { (BundledFolder, true), (UserFolder, false) })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.GetDirectories(root))
                    foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                    {
                        var name = Path.GetFileNameWithoutExtension(dll);
                        if (!name.EndsWith("Plugin", StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.ChangeExtension(dll, ".catalyzer"))) continue;   // a plugin's main dll: *Plugin.dll (or marked with a .catalyzer file)
                        Inspect(dll, dir, bundled);
                    }
            }
            foreach (var p in Plugins.Where(p => p.On && p.Error == null)) StartOne(p);
        }

        private void Inspect(string dll, string dir, bool bundled)
        {
            var info = new PluginInfo { Dll = dll, Folder = dir, Bundled = bundled, Name = Path.GetFileNameWithoutExtension(dll) };
            Plugins.Add(info);
            try
            {
                if (!_probe.Contains(dir)) _probe.Add(dir);
                var asm = Assembly.LoadFrom(dll);
                var type = asm.GetTypes().FirstOrDefault(t => typeof(ICatalyzerPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);
                if (type == null) { info.Error = "no ICatalyzerPlugin in it"; return; }
                var inst = (ICatalyzerPlugin)Activator.CreateInstance(type);
                info.Instance = inst; info.Id = inst.Id; info.Name = inst.Name; info.Version = inst.Version; info.Author = inst.Author; info.Description = inst.Description;
                if (Plugins.Count(p => p.Id == info.Id) > 1) { info.Error = "another plugin has the id '" + info.Id + "'"; return; }
                if (inst.MinApi > Api.Version) { info.Error = "needs Catalyzer API " + inst.MinApi + " (this is " + Api.Version + ")"; return; }
                info.On = !Prefs.Value.PluginsOff.Contains(info.Id);
            }
            catch (ReflectionTypeLoadException ex) { info.Error = "couldn't load: " + (ex.LoaderExceptions.FirstOrDefault()?.Message ?? ex.Message); }
            catch (Exception ex) { info.Error = "couldn't load: " + ex.Message; }
            if (info.Error != null) Catalyzer.Log.Write("plugin " + info.Name + ": " + info.Error, show: true);
        }

        private void StartOne(PluginInfo p)
        {
            try { p.Instance.Start(this); p.Running = true; Catalyzer.Log.Write("plugin " + p.Name + " " + p.Version + " started"); }
            catch (Exception ex) { p.Error = "failed to start: " + ex.Message; Catalyzer.Log.Write("plugin " + p.Name + ": " + ex); }
        }

        public void StopAll() { foreach (var p in Plugins.Where(p => p.Running)) { try { p.Instance.Stop(); } catch { } } }

        /// <summary>On / off (takes effect at the next start).</summary>
        public void Switch(PluginInfo p, bool on)
        {
            p.On = on;
            Prefs.Save(x => { x.PluginsOff.Remove(p.Id); if (!on) x.PluginsOff.Add(p.Id); });
        }

        /// <summary>
        /// Add a plugin: a .zip (unpacked) or a .dll (its whole folder comes along - that's where its dependencies are)
        /// into %APPDATA%\Catalyzer\plugins\&lt;name&gt;. Returns the folder, or null.
        /// </summary>
        public string Import(string path)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(path);
                string to = Path.Combine(UserFolder, name);
                if (Directory.Exists(to)) to = to + "-" + DateTime.Now.ToString("HHmmss");
                Directory.CreateDirectory(to);
                if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ZipFile.ExtractToDirectory(path, to, true);
                else
                {
                    var src = Path.GetDirectoryName(path);
                    foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
                    if (!Path.GetFileNameWithoutExtension(path).EndsWith("Plugin", StringComparison.OrdinalIgnoreCase)) File.WriteAllText(Path.Combine(to, Path.GetFileNameWithoutExtension(path) + ".catalyzer"), "plugin");
                }
                Catalyzer.Log.Write("plugin imported into " + to, show: true);
                return to;
            }
            catch (Exception ex) { Catalyzer.Log.Write("import " + path + ": " + ex.Message, show: true); return null; }
        }

        /// <summary>A plugin's dependencies come from its own folder; Catalyzer.Sdk always from the app.</summary>
        private Assembly Resolve(object sender, ResolveEventArgs e)
        {
            var an = new AssemblyName(e.Name);
            if (an.Name == "Catalyzer.Sdk") return typeof(ICatalyzerPlugin).Assembly;
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == an.Name);
            if (loaded != null) return loaded;
            foreach (var dir in _probe)
            {
                var f = Path.Combine(dir, an.Name + ".dll");
                if (File.Exists(f)) { try { return Assembly.LoadFrom(f); } catch { } }
            }
            return null;
        }
    }
}
