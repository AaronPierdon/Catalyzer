# Writing a Catalyzer plugin

A plugin is a .NET 10 (`net10.0-windows`) class library that references **Catalyzer.Sdk**. It contains one public class with a parameterless constructor that implements `ICatalyzerPlugin`.

```xml
<ProjectReference Include="..\Catalyzer\src\Catalyzer.Sdk\Catalyzer.Sdk.csproj">
  <Private>false</Private>              <!-- the host supplies the Sdk: never ship your own copy -->
  <ExcludeAssets>runtime</ExcludeAssets>
</ProjectReference>
```

```csharp
public sealed class HelloPlugin : ICatalyzerPlugin
{
    public string Id => "hello";          // unique, permanent
    public string Name => "Hello";
    public string Version => "1.0.0";
    public string Author => "you";
    public string Description => "Says hi on every card.";
    public int MinApi => 1;               // the Api.Version you need

    public void Start(ICatalyzerHost host)
    {
        host.AddAction(new ProjectAction {
            Id = "hello.wave", Label = "WAVE", Glyph = "", Order = 50,
            AppliesTo = p => p.Git.IsRepo,
            Run = p => host.Log(this, "hi from " + p.Name, show: true) });
    }
    public void Stop() { }
}
```

## Packaging
- Name the main dll `*Plugin.dll` (for example `HelloPlugin.dll`), or put a `<Name>.catalyzer` file next to it.
- Put it in its own folder with its dependencies.
- In Catalyzer, use **Settings → Plugins → Add / Import**. You can import the dll (its whole folder is copied) or a .zip of the folder.
- Plugins land in `%APPDATA%\Catalyzer\plugins\<name>\`. Bundled plugins ship in `<install>\plugins\<name>\`.
- Turning a plugin on or off, or importing one, takes effect after a restart.

## What the host gives you (`ICatalyzerHost`)
| | |
|---|---|
| `ApiVersion`, `AppVersion` | what you're running in |
| `Ui` | the UI dispatcher (Start runs on it; marshal back to it from your own threads) |
| `Projects`, `ProjectsChanged` | the board's projects (name, folder, manifest keys in `Extra`, live `Git` state) |
| `AddAction(ProjectAction)` | a button on cards (`AppliesTo` picks which ones; `Order` places it) |
| `AddSource(IProjectSource)` | more project folders (from a server list, another tool, anything) |
| `Themes` | the theme layer (see below) |
| `Refresh()` | rescan |
| `DataFolder(this)` | your own folder for files and settings |
| `Log(this, text, show)` | the log; `show: true` also puts it on the info bar |

## Themes
`host.Themes.Register(new CatalyzerTheme { Id = "mine", Name = "Mine", Accent = ..., Select = ..., ... })`

- Anything you leave out falls back to Catalyzer's defaults: fonts, panel, edge, line, ok / warn / danger.
- The fields are:
  - colours: `Bg0`, `Bg1`, `Panel`, `PanelEdge`, `Line`, `Text`, `TextDim`, `TextFaint`, `Accent`, `Select`, `Ok`, `Warn`, `Danger`;
  - fonts: `FontUi`, `FontHead`, `FontMono`, plus `HeadCaps`;
  - effects: `Glow` (0..2), `Cut` (corner chamfer), `Scan` (scan lines), `Light` (ink instead of glow);
  - `Backdrop`: a factory for the window's background element.
- **Live themes:** to repaint the app, register again with the same `Id`. The Nexus plugin does this every time Nexus's theme changes.
- `Themes.Apply(id)` switches theme. `Themes.Changed` tells you when it changes.

## Compatibility rules
- `Api.Version` goes up only when the API gains something. Existing members are never removed or changed.
- Catalyzer won't start a plugin whose `MinApi` is higher than the host's API version, and shows why in Settings.
- Errors in your plugin are caught and logged. A failing plugin is marked in Settings and never takes the app down.

Example: `plugins/Catalyzer.Nexus` — a live theme from Nexus's public `NexusPublicApi`, plus a card button.
