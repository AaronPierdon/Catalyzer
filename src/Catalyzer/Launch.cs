using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Catalyzer.Sdk;
using Microsoft.Win32;

namespace Catalyzer
{
    /// <summary>The built-in card buttons (plugins add more) and the project's own actions from its catalyzer.json.</summary>
    public static class Launch
    {
        public static List<ProjectAction> BuiltIn() => new List<ProjectAction>
        {
            new ProjectAction { Id = "folder", Label = "FOLDER", Glyph = "", Tip = "OPEN THE PROJECT FOLDER", Order = 0, Run = p => Shell("explorer.exe", "\"" + p.Folder + "\"") },
            new ProjectAction { Id = "github", Label = "GITHUB", Glyph = "", Tip = "THE REPO ON GITHUB", Order = 10, AppliesTo = p => p.GitHub != null, Run = p => Url(p.GitHub) },
            new ProjectAction { Id = "code", Label = "CODE", Glyph = "", Tip = "OPEN IN VS CODE", Order = 20, AppliesTo = _ => VsCode() != null, Run = p => Start(VsCode(), "\"" + p.Folder + "\"", p.Folder) },
            new ProjectAction { Id = "vs", Label = "VS", Glyph = "", Tip = "OPEN THE SOLUTION IN VISUAL STUDIO", Order = 30, AppliesTo = p => p.Solution != null && File.Exists(p.Solution), Run = p => Shell(p.Solution, null) },
            new ProjectAction { Id = "term", Label = "TERMINAL", Glyph = "", Tip = "A TERMINAL IN THE PROJECT FOLDER", Order = 40, Run = p => Terminal(p.Folder) },
        };

        /// <summary>"actions": [ { "label", "glyph", "run", "args", "tip" } ] in catalyzer.json: the project's own buttons.</summary>
        public static List<ProjectAction> FromManifest(Project p)
        {
            var list = new List<ProjectAction>();
            if (!p.Extra.TryGetValue("actions", out var raw) || string.IsNullOrWhiteSpace(raw)) return list;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                int i = 0;
                foreach (var a in doc.RootElement.EnumerateArray())
                {
                    string S(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
                    var run = S("run"); if (string.IsNullOrWhiteSpace(run)) continue;
                    var args = S("args"); var label = (S("label") ?? Path.GetFileName(run)).ToUpperInvariant();
                    string exe = Path.IsPathRooted(run) || !File.Exists(Path.Combine(p.Folder, run)) ? run : Path.Combine(p.Folder, run);
                    list.Add(new ProjectAction { Id = "m" + i++, Label = label, Glyph = S("glyph") ?? "", Tip = S("tip") ?? (run + " " + args).ToUpperInvariant(), Order = 60 + i, Run = _ => Shell(exe, args, p.Folder) });
                }
            }
            catch (Exception ex) { Log.Write(p.Name + " actions: " + ex.Message, show: true); }
            return list;
        }

        public static void Shell(string file, string args, string cwd = null)
        {
            try { Process.Start(new ProcessStartInfo(file, args ?? "") { UseShellExecute = true, WorkingDirectory = cwd ?? "" }); }
            catch (Exception ex) { Log.Write("couldn't open " + file + ": " + ex.Message, show: true); }
        }

        private static void Start(string exe, string args, string cwd)
        {
            try { Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, WorkingDirectory = cwd, CreateNoWindow = true }); }
            catch (Exception ex) { Log.Write("couldn't start " + exe + ": " + ex.Message, show: true); }
        }

        public static void Url(string url) => Shell(url, null);

        private static string _code;
        private static string VsCode()
        {
            if (_code != null) return _code == "" ? null : _code;
            var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            _code = new[] { Path.Combine(la, "Programs", "Microsoft VS Code", "Code.exe"), Path.Combine(pf, "Microsoft VS Code", "Code.exe") }.FirstOrDefault(File.Exists) ?? "";
            return _code == "" ? null : _code;
        }

        private static void Terminal(string dir)
        {
            var wt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
            if (File.Exists(wt)) Shell(wt, "-d \"" + dir + "\"");
            else Shell("powershell.exe", "-NoExit -NoLogo", dir);
        }

        // ------------------------------------------------------------------ run at Windows start

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool AtStartup
        {
            get { try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("Catalyzer") is string; } catch { return false; } }
            set
            {
                try
                {
                    using var k = Registry.CurrentUser.CreateSubKey(RunKey);
                    if (value) k.SetValue("Catalyzer", "\"" + Environment.ProcessPath + "\" --autostart");
                    else k.DeleteValue("Catalyzer", false);
                    Log.Write("run at Windows start: " + (value ? "on" : "off"));
                }
                catch (Exception ex) { Log.Write("startup: " + ex.Message, show: true); }
            }
        }
    }
}
