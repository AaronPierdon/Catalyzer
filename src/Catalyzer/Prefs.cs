using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Catalyzer
{
    /// <summary>Catalyzer's settings (%APPDATA%\Catalyzer\settings.json).</summary>
    public sealed class PrefsData
    {
        public int Ver { get; set; } = 1;
        public string Theme { get; set; } = "nexus";   // the Nexus plugin's live theme when it's there, else Catalyst
        /// <summary>Folders whose sub-folders are projects (default: %USERPROFILE%\source\repos).</summary>
        public List<string> Roots { get; set; } = new List<string>();
        /// <summary>Single project folders added by hand.</summary>
        public List<string> Extra { get; set; } = new List<string>();
        /// <summary>Project folders you hid.</summary>
        public List<string> Hidden { get; set; } = new List<string>();
        /// <summary>Your card order (folders); the rest follow by last commit.</summary>
        public List<string> Order { get; set; } = new List<string>();
        /// <summary>Plugins switched off (ids).</summary>
        public List<string> PluginsOff { get; set; } = new List<string>();
        public double Width { get; set; } = 1500;
        public double Height { get; set; } = 920;
        public bool Maximized { get; set; }
        public bool ReduceMotion { get; set; }
        /// <summary>Closing while helpers (plugins' background work) run: "ask", "stop" (stop all and exit) or "tray".</summary>
        public string CloseWithWork { get; set; } = "ask";
    }

    public static class Prefs
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Catalyzer");
        private static readonly string FilePath = Path.Combine(Folder, "settings.json");
        private static readonly JsonSerializerOptions Opt = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        private static PrefsData _v;
        public static bool Offline;   // --snapshot: never write

        public static PrefsData Value
        {
            get
            {
                if (_v != null) return _v;
                try { if (File.Exists(FilePath)) _v = JsonSerializer.Deserialize<PrefsData>(File.ReadAllText(FilePath), Opt); } catch (Exception ex) { Log.Write("prefs: " + ex.Message); }
                _v ??= new PrefsData();
                if (_v.Roots.Count == 0) _v.Roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos"));
                return _v;
            }
        }

        public static void Save(Action<PrefsData> edit)
        {
            edit?.Invoke(Value);
            if (Offline) return;
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_v, Opt));
                File.Move(tmp, FilePath, true);
            }
            catch (Exception ex) { Log.Write("prefs save: " + ex.Message); }
        }
    }

    /// <summary>%LOCALAPPDATA%\Catalyzer\log.txt (rolls at 1 MB).</summary>
    public static class Log
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Catalyzer", "log.txt");
        private static readonly object Gate = new object();
        public static event Action<string> Shown;
        public static void Write(string line, bool show = false)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 1_000_000) File.Move(FilePath, FilePath + ".old", true);
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + line + Environment.NewLine);
                }
            }
            catch { }
            if (show) Shown?.Invoke(line);
        }
        public static void Write(Exception ex) => Write(ex.GetType().Name + ": " + ex.Message + " " + ex.StackTrace?.Split('\n')[0]?.Trim());
    }
}
