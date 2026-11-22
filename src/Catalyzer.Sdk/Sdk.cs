using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Catalyzer.Sdk
{
    /// <summary>The plugin API version. Grows only by adding things; a plugin built against an older version keeps working.</summary>
    public static class Api
    {
        /// <summary>
        /// 1 = themes, project actions, project sources, log, data folder.
        /// 2 = card badges (<see cref="ProjectBadge"/>), background work (<see cref="IBackgroundWork"/>: the header chip,
        ///     the close prompt and the tray), <see cref="ICatalyzerHost.IsSnapshot"/>.
        /// </summary>
        public const int Version = 2;
    }

    /// <summary>
    /// A Catalyzer plugin. Put a public class implementing this in a .dll; Catalyzer finds it in a plugins folder
    /// (one folder per plugin, with the plugin's own dependencies beside it), creates it with its parameterless
    /// constructor and calls <see cref="Start"/> on the UI thread when it's switched on.
    /// </summary>
    public interface ICatalyzerPlugin
    {
        /// <summary>Permanent, lowercase-kebab (e.g. "nexus"). Used for its on / off switch and its data folder.</summary>
        string Id { get; }
        string Name { get; }
        string Version { get; }
        string Author { get; }
        string Description { get; }
        /// <summary>The lowest <see cref="Api.Version"/> it needs.</summary>
        int MinApi { get; }
        /// <summary>Register what it brings (themes, actions, sources). Called once, on the UI thread.</summary>
        void Start(ICatalyzerHost host);
        /// <summary>Called when Catalyzer closes (switching a plugin off takes effect at the next start).</summary>
        void Stop();
    }

    /// <summary>What Catalyzer offers a plugin.</summary>
    public interface ICatalyzerHost
    {
        int ApiVersion { get; }
        string AppVersion { get; }
        /// <summary>The UI thread (everything that touches WPF goes through it).</summary>
        Dispatcher Ui { get; }
        IThemeService Themes { get; }
        /// <summary>Every project on the board right now.</summary>
        IReadOnlyList<Project> Projects { get; }
        /// <summary>Raised (UI thread) after the board was rescanned.</summary>
        event Action ProjectsChanged;
        /// <summary>A button on project cards (shown where <see cref="ProjectAction.AppliesTo"/> says).</summary>
        void AddAction(ProjectAction action);
        /// <summary>More projects (folders) for the board, beside the scanned ones.</summary>
        void AddSource(IProjectSource source);
        /// <summary>Rescan everything (sources included).</summary>
        void Refresh();
        /// <summary>A folder of its own for the plugin's settings / cache (created on demand).</summary>
        string DataFolder(ICatalyzerPlugin plugin);
        /// <summary>A line in Catalyzer's log; also shown on the footer when <paramref name="show"/>.</summary>
        void Log(ICatalyzerPlugin plugin, string message, bool show = false);

        // ---------------------------------------------------------------- API 2

        /// <summary>API 2. A render-only run (--snapshot): draw, but don't start anything or write anything.</summary>
        bool IsSnapshot { get; }
        /// <summary>API 2. A status chip on project cards (shown where <see cref="ProjectBadge.AppliesTo"/> says).</summary>
        void AddBadge(ProjectBadge badge);
        /// <summary>API 2. Ask the cards to read their badges again (call it when your state changed; any thread).</summary>
        void UpdateBadges();
        /// <summary>
        /// API 2. Work that keeps running while Catalyzer is open (helpers, watchers...). Catalyzer shows it in the header,
        /// asks before closing while any is running, and offers "stop all" in the tray.
        /// </summary>
        void AddBackgroundWork(IBackgroundWork work);
    }

    /// <summary>API 2. How a badge or line is coloured: a theme role, never a literal.</summary>
    public enum Tone { Faint, Dim, Accent, Select, Ok, Warn, Danger }

    /// <summary>API 2. A small status chip on a card: a light, a short text, and a menu when clicked.</summary>
    public sealed class ProjectBadge
    {
        public string Id { get; set; } = "";
        /// <summary>Lower = further left.</summary>
        public int Order { get; set; } = 100;
        public Func<Project, bool> AppliesTo { get; set; } = _ => true;
        /// <summary>What it shows for a project right now (null = nothing for this one). Called on the UI thread.</summary>
        public Func<Project, BadgeState> State { get; set; }
        /// <summary>What clicking it offers (null or empty = not clickable). Called on the UI thread.</summary>
        public Func<Project, IEnumerable<MenuEntry>> Menu { get; set; }
    }

    /// <summary>API 2. A badge's look at one moment.</summary>
    public sealed class BadgeState
    {
        /// <summary>Short, capitals (e.g. "HELPERS 1/2").</summary>
        public string Text { get; set; } = "";
        public Tone Tone { get; set; } = Tone.Dim;
        /// <summary>The light breathes (something is busy).</summary>
        public bool Pulse { get; set; }
        public string Tip { get; set; }
    }

    /// <summary>API 2. A line in a Catalyzer menu.</summary>
    public sealed class MenuEntry
    {
        public string Text { get; set; } = "";
        /// <summary>A Segoe Fluent Icons / MDL2 glyph.</summary>
        public string Glyph { get; set; }
        /// <summary>Faint text after the label.</summary>
        public string Hint { get; set; }
        public Action Run { get; set; }
        public bool Separator { get; set; }
        public bool Danger { get; set; }
    }

    /// <summary>API 2. Something a plugin keeps running (see <see cref="ICatalyzerHost.AddBackgroundWork"/>).</summary>
    public interface IBackgroundWork
    {
        /// <summary>What one of them is called, plural, capitals: "HELPERS".</summary>
        string Name { get; }
        /// <summary>How many are running now.</summary>
        int Running { get; }
        /// <summary>Raised (any thread) when <see cref="Running"/> may have changed.</summary>
        event Action Changed;
        /// <summary>One line per running item, for the header chip's menu (each with its own stop).</summary>
        IEnumerable<MenuEntry> Items();
        /// <summary>Stop everything, now (Catalyzer is closing or the user asked).</summary>
        void StopAll();
    }

    /// <summary>The theme layer: every Catalyzer control draws from the current theme, live.</summary>
    public interface IThemeService
    {
        CatalyzerTheme Current { get; }
        IReadOnlyList<CatalyzerTheme> All { get; }
        /// <summary>Raised (UI thread) when the current theme changed or was updated.</summary>
        event Action Changed;
        /// <summary>Add a theme, or replace one with the same Id (if it's the current one, the change shows at once - live themes).</summary>
        void Register(CatalyzerTheme theme);
        /// <summary>Make a theme current (also remembered as your choice).</summary>
        void Apply(string id);
    }

    /// <summary>
    /// A theme: colour roles, fonts, a few look dials and (optionally) its own backdrop. Roles, never literals: controls
    /// ask for "accent", "select", "text"... so any theme reskins everything.
    /// </summary>
    public sealed class CatalyzerTheme
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>Who brought it ("Catalyzer", or a plugin's name).</summary>
        public string Source { get; set; } = "Catalyzer";

        public Color Bg0 { get; set; }        // window floor
        public Color Bg1 { get; set; }        // raised floor / gradients
        public Color Panel { get; set; }      // card glass (alpha = see-through)
        public Color PanelEdge { get; set; }  // card rims
        public Color Line { get; set; }       // faint rules, tracks
        public Color Text { get; set; }
        public Color TextDim { get; set; }
        public Color TextFaint { get; set; }
        /// <summary>The dominant colour: frames, icons, hover, graphs.</summary>
        public Color Accent { get; set; }
        /// <summary>The chosen / important thing: selection, primary buttons, focus.</summary>
        public Color Select { get; set; }
        public Color Ok { get; set; }
        public Color Warn { get; set; }
        public Color Danger { get; set; }

        public FontFamily FontUi { get; set; }
        public FontFamily FontHead { get; set; }
        public FontFamily FontMono { get; set; }
        /// <summary>Headings in capitals with extra tracking.</summary>
        public bool HeadCaps { get; set; } = true;

        /// <summary>0 = flat, 1 = normal glow, 2 = a lot.</summary>
        public double Glow { get; set; } = 1;
        /// <summary>Corner cut of panels in px (0 = square).</summary>
        public double Cut { get; set; } = 12;
        /// <summary>Scan lines on glass (0..1).</summary>
        public double Scan { get; set; } = 0.35;
        /// <summary>Light theme: ink instead of glow.</summary>
        public bool Light { get; set; }

        /// <summary>Optional: the theme's own window backdrop (null = Catalyzer's).</summary>
        public Func<FrameworkElement> Backdrop { get; set; }

        public CatalyzerTheme Clone() => (CatalyzerTheme)MemberwiseClone();
    }

    /// <summary>A project on the board: a folder (usually a git repo) + what its catalyzer.json says.</summary>
    public sealed class Project
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Folder { get; set; } = "";
        public string Description { get; set; } = "";
        public string GitHub { get; set; }
        /// <summary>The solution / project file to open in Visual Studio (null = none found).</summary>
        public string Solution { get; set; }
        /// <summary>Its own colour (from catalyzer.json "accent"), or null for the theme's.</summary>
        public Color? Accent { get; set; }
        public string Glyph { get; set; }
        public bool HasManifest { get; set; }
        /// <summary>Who added it ("scan", "manual", or a plugin's id).</summary>
        public string Origin { get; set; } = "scan";
        /// <summary>Everything in catalyzer.json, for plugins that read their own keys.</summary>
        public IReadOnlyDictionary<string, string> Extra { get; set; } = new Dictionary<string, string>();
        public GitState Git { get; set; } = new GitState();
    }

    public sealed class GitState
    {
        public bool IsRepo { get; set; }
        public bool Loaded { get; set; }
        public string Branch { get; set; }
        public int Ahead { get; set; }
        public int Behind { get; set; }
        public int Changed { get; set; }
        public string LastHash { get; set; }
        public string LastSubject { get; set; }
        public DateTime? LastWhen { get; set; }
        public string Remote { get; set; }
        /// <summary>Commits per day, oldest first (the last 21 days).</summary>
        public int[] Daily { get; set; } = Array.Empty<int>();
    }

    /// <summary>A button on project cards.</summary>
    public sealed class ProjectAction
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        /// <summary>A Segoe Fluent Icons / MDL2 glyph.</summary>
        public string Glyph { get; set; }
        public string Tip { get; set; }
        /// <summary>Lower = further left. Built-ins use 0..50.</summary>
        public int Order { get; set; } = 100;
        public Func<Project, bool> AppliesTo { get; set; } = _ => true;
        public Action<Project> Run { get; set; }
    }

    /// <summary>Projects from somewhere else (another folder tree, a server, a list...).</summary>
    public interface IProjectSource
    {
        string Name { get; }
        /// <summary>Project folders. Called off the UI thread on every rescan.</summary>
        IEnumerable<string> Folders();
    }
}
