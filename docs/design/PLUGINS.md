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
    public int MinApi => 1;               // the Api.Version you need (2 for badges / background work)

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
| `IsSnapshot` *(API 2)* | a render-only run (`--snapshot`): draw, but don't start or write anything |
| `AddBadge(ProjectBadge)` *(API 2)* | a status chip on cards: `State(p)` → text, `Tone`, pulse, tip; `Menu(p)` → what clicking offers |
| `UpdateBadges()` *(API 2)* | your state changed: cards read their chips again (any thread) |
| `ShowFeed(IFeed)` *(API 2)* | open the live log drawer: `Title`, `State`, `Lines()`, `Actions()` (its buttons), `Changed` |
| `AddBackgroundWork(IBackgroundWork)` *(API 2)* | something you keep running. Catalyzer shows it in the header, asks before closing while any of it runs, and offers stop in the tray |

## Plugin settings (API 3)
`host.AddSetting(this, new PluginSetting { Id, Label, Note, Get, Set })` adds an on / off toggle to Settings →
General, under the plugin's name. The plugin keeps the value itself, in its `DataFolder`. Example: Link's *Relink
linked projects when Catalyzer starts*.

## Badges and background work (API 2)
- **Badges.** A `ProjectBadge` has an `Id`, an `Order`, an `AppliesTo`, a `State` and a `Menu`. `State` returns a
  `BadgeState`: `Text` (short, capitals), `Tone` (`Faint`, `Dim`, `Accent`, `Select`, `Ok`, `Warn` or `Danger`; a
  theme role, never a colour), `Pulse` (busy) and `Tip`. `Click` (optional) makes the chip a one-click toggle, with `Menu` on right-click. `Menu` returns `MenuEntry` lines (`Text`, `Glyph`, `Hint`,
  `Run`, `Separator`, `Danger`); Catalyzer draws them as its own glass menu.
- **Card mood (API 4).** `BadgeState.Card` (`CardMood`: `None`, `Linked`, `Working`, `Warn`, `Danger`) is what the
  state asks of the whole card; the strongest across a card's badges wins. `Linked`: the card takes the select
  colour. `Working`: full energy, the card's two colours shift through each other. `Warn` / `Danger`: a serious
  problem only (a conflict, a failed install): the whole card goes amber / red and breathes. Ordinary states (changes,
  pushes, notes) leave it `None` and stay on the chip.
- **Background work.** `IBackgroundWork` has:
  - `Name`: plural, capitals, e.g. `"HELPERS"`;
  - `Running`: a count;
  - `Changed`: an event;
  - `Items()`: one stop line per running thing;
  - `StopAll()`.

  Catalyzer calls `StopAll()` when the user exits with work running. Your `Stop()` is still called at exit: stop
  everything there too.

Examples:
- `plugins/Catalyzer.Helpers` uses badges and background work;
- `plugins/Catalyzer.Link` uses `Click`, feeds and background work.

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

Examples: `plugins/Catalyzer.Nexus` — a live theme from Nexus's public `NexusPublicApi`, plus a card button.
