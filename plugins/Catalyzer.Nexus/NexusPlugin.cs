using System;
using System.IO;
using System.Windows;
using Catalyzer.Sdk;
using Nexus.UI.Wpf;

namespace Catalyzer.Nexus
{
    /// <summary>
    /// Catalyzer wearing Nexus. Joins the Nexus settings bus (NexusPublicApi.Attach), turns the live Nexus look into a
    /// Catalyzer theme ("Nexus  //  live") and re-registers it whenever Nexus changes, so switching theme in Nexus
    /// Control repaints Catalyzer too. Adds an "Open in Nexus Explorer" button to every card.
    /// Everything goes through two public surfaces: Catalyzer.Sdk and Nexus's NexusPublicApi. Nothing in Catalyzer knows Nexus.
    /// </summary>
    public sealed class NexusPlugin : ICatalyzerPlugin
    {
        public string Id => "nexus";
        public string Name => "Nexus";
        public string Version => "1.0.0";
        public string Author => "Aaron Pierdon";
        public string Description => "Follows your live Nexus theme (colours, fonts, glow, backdrop) and opens projects in Nexus Explorer.";
        public int MinApi => 1;

        private ICatalyzerHost _host;

        public void Start(ICatalyzerHost host)
        {
            _host = host;
            NexusPublicApi.Attach("catalyzer", "Catalyzer");
            NexusPublicApi.LookChanged += Push;
            Push();
            host.AddAction(new ProjectAction
            {
                Id = "nexus.explorer", Label = "NEXUS", Glyph = "", Order = 15,
                Tip = "OPEN IN NEXUS EXPLORER",
                AppliesTo = p => File.Exists(Path.Combine(NexusPublicApi.InstallFolder, "Nexus.Files.exe")) && Directory.Exists(p.Folder),
                Run = p => { if (!NexusPublicApi.OpenFolder(p.Folder)) host.Log(this, "Nexus Explorer isn't installed", true); },
            });
            host.Log(this, "following Nexus theme '" + NexusPublicApi.Look.ThemeName + "' (Nexus API " + NexusPublicApi.Version + ")");
        }

        public void Stop() { NexusPublicApi.LookChanged -= Push; }

        private void Push()
        {
            var L = NexusPublicApi.Look;
            _host.Themes.Register(new CatalyzerTheme
            {
                Id = "nexus", Name = "Nexus  //  " + (L.ThemeName ?? "live"), Source = "Nexus plugin",
                Bg0 = L.Bg0, Bg1 = L.Bg1, Panel = L.Glass, PanelEdge = L.GlassEdge, Line = L.Line.A(0.6),
                Text = L.Text, TextDim = L.TextDim, TextFaint = L.TextFaint,
                Accent = L.Accent, Select = L.Select, Ok = L.Ok, Warn = L.Warn, Danger = L.Danger,
                FontUi = L.FontUi, FontHead = L.FontHead, FontMono = L.FontMono, HeadCaps = true,
                Glow = L.Glow, Light = L.Light, Cut = 12, Scan = 0.45,
                Backdrop = NexusPublicApi.Backdrop,
            });
        }
    }
}
