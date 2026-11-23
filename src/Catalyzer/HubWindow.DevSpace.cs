using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// _devSpace (principles 07): each repo's git-ignored scratch pad. The card shows its size; CLEAN UP offers the files
    /// nobody has touched for a week before the repo's last commit (the work moved on without them), lists every one,
    /// and only on your yes moves them to the Recycle Bin. Never silent, never permanent.
    /// </summary>
    public sealed partial class HubWindow
    {
        private readonly Dictionary<string, long> _devSize = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private const int StaleDays = 7;

        private void InitDevSpace()
        {
            PluginHost.I?.AddBadge(new ProjectBadge
            {
                Id = "devspace", Order = 50,
                AppliesTo = p => Directory.Exists(Path.Combine(p.Folder, "_devSpace")),
                State = p => _devSize.TryGetValue(p.Folder, out var b) ? new BadgeState { Text = "DEV " + Size(b), Tone = Tone.Faint, Tip = "_DEVSPACE: THE SCRATCH PAD (NOT IN GIT)\nCLICK: OPEN  //  RIGHT-CLICK: CLEAN UP" } : null,
                Click = p => Launch.Shell("explorer.exe", "\"" + Path.Combine(p.Folder, "_devSpace") + "\""),
                Menu = p => new[]
                {
                    new MenuEntry { Text = "Open _devSpace", Glyph = "", Run = () => Launch.Shell("explorer.exe", "\"" + Path.Combine(p.Folder, "_devSpace") + "\"") },
                    new MenuEntry { Text = "Clean up...", Glyph = "", Hint = "lists old files first", Run = () => CleanDevSpace(p) },
                },
            });
            _board.Changed += () => { foreach (var p in _board.Items) MeasureDevSpace(p.Folder); };
        }

        private static string Size(long b) => b < 1 << 20 ? Math.Max(1, b >> 10) + " KB" : b < 1L << 30 ? (b >> 20) + " MB" : (b / (double)(1L << 30)).ToString("0.0") + " GB";

        private async void MeasureDevSpace(string folder)
        {
            if (Prefs.Offline) return;
            var dir = Path.Combine(folder, "_devSpace");
            if (!Directory.Exists(dir)) { _devSize.Remove(folder); return; }
            try
            {
                var bytes = await Task.Run(() => new DirectoryInfo(dir).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Sum(f => f.Length));
                _devSize[folder] = bytes;
                PluginHost.I?.UpdateBadges();
            }
            catch (Exception ex) { Log.Write("devspace " + folder + ": " + ex.Message); }
        }

        private async void CleanDevSpace(Project p)
        {
            var dir = Path.Combine(p.Folder, "_devSpace");
            var last = p.Git?.LastWhen?.ToLocalTime();
            if (last == null) { Log.Write(p.Name + ": no commits yet, so nothing counts as old", show: true); return; }
            var cut = last.Value.AddDays(-StaleDays);
            List<FileInfo> old;
            try { old = await Task.Run(() => new DirectoryInfo(dir).EnumerateFiles("*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Where(f => f.LastWriteTime < cut).OrderByDescending(f => f.Length).ToList()); }
            catch (Exception ex) { Log.Write("devspace: " + ex.Message, show: true); return; }
            if (old.Count == 0) { Say(p.Name.ToUpperInvariant() + " _DEVSPACE  //  NOTHING UNTOUCHED SINCE A WEEK BEFORE THE LAST COMMIT"); return; }

            var d = Dialog("Clean up " + p.Name + " _devSpace", 640, onEsc: null);
            long total = old.Sum(f => f.Length);
            d.Body.Children.Add(Para(old.Count + " file(s), " + Size(total) + ", untouched since " + cut.ToString("yyyy-MM-dd") + " (a week before the last commit). They go to the Recycle Bin, so you can still get them back.", 13.5, "CzTextBrush"));
            foreach (var f in old.Take(16))
                d.Body.Children.Add(Cz.Mono(Path.GetRelativePath(dir, f.FullName) + "   ·   " + Size(f.Length) + "   ·   " + f.LastWriteTime.ToString("yyyy-MM-dd"), 10.5, "CzTextDimBrush").Also(t => { t.Margin = new Thickness(4, 1, 0, 1); t.TextTrimming = TextTrimming.CharacterEllipsis; }));
            if (old.Count > 16) d.Body.Children.Add(Cz.Mono("+" + (old.Count - 16) + " MORE", 10, "CzTextFaintBrush").Also(t => t.Margin = new Thickness(4, 4, 0, 0)));
            d.Buttons.Children.Add(Btn("KEEP THEM", d.Close));
            var go = Btn("MOVE TO RECYCLE BIN", null, "", primary: true);
            go.Click = async () =>
            {
                go.IsEnabled = false; go.SetLabel("MOVING...");
                int n = 0;
                await Task.Run(() =>
                {
                    foreach (var f in old)
                        try { Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(f.FullName, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin); n++; }
                        catch (Exception ex) { Log.Write("devspace " + f.FullName + ": " + ex.Message); }
                });
                Log.Write(p.Name + " _devSpace: " + n + " file(s) to the Recycle Bin", show: true);
                d.Close();
                MeasureDevSpace(p.Folder);
            };
            d.Buttons.Children.Add(go);
        }
    }
}
