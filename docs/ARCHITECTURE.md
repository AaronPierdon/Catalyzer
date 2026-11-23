# ARCHITECTURE — Catalyzer

One screen. How each part works is in [GUIDE.md](GUIDE.md).

## Parts
| Part | Folder | Does |
|---|---|---|
| **App** | `src/Catalyzer` | the window, board, cards, drawers (settings, notes, feed, GitHub), dialogs (welcome, new project, publish, cleanup), theme layer, tray, plugin host |
| **SDK** | `src/Catalyzer.Sdk` | the plugin API (API 3): themes, card actions and badges, project sources, background work, feeds, plugin settings. Add-only |
| **NexusPlugin** | `plugins/Catalyzer.Nexus` | the live Nexus theme (through `NexusPublicApi`), and the NEXUS button |
| **LinkPlugin** | `plugins/Catalyzer.Link` | LINK per card: a hidden Windows terminal through a mailbox in the repo |
| **HelpersPlugin** | `plugins/Catalyzer.Helpers` | a project's long-running scripts (`helpers` in `catalyzer.json`), hidden |
| **Principles** | `principles/` | the Catalyzer Development Principles (look, engineering, workflow, rules, repo layout, templates). Docs only, versioned in `principles/VERSION` |
| **tools** | `tools/` | `install.ps1` (Release to `%LOCALAPPDATA%\Programs\Catalyzer`), `snap.ps1` (build + snapshots) |

The app never references a plugin or Nexus. Plugins only see the SDK.

## Libraries
| Library | For |
|---|---|
| .NET 10, WPF | everything; the UI is code-built (`OnRender` and `FrameworkElement`s), with no XAML |
| Windows Forms (`NotifyIcon` only) | the tray icon |
| `System.Text.Json` | settings, `catalyzer.json`, linked.json |
| `git.exe` (hidden), Windows PowerShell 5.1 | git state and notes sync; link requests and helpers |
| Nexus.Kit (NexusPlugin only) | the live Nexus look |

## Feature sets
- **Board:** scan, cards, git state, activity, buttons, search, a folder watcher. Lives in `Projects`, `Board`,
  `ProjectCard` and `HubWindow`.
- **GitHub:** `GitHub` (REST through `HttpClient`, the token borrowed from Git Credential Manager per request,
  never stored or logged), `HubWindow.GitHub` (panel: clone / push / publish), `HubWindow.NewProject` (scaffold from
  `principles/templates`, git init, create, push, handoff), `HubWindow.Welcome` (first run). GitHub is core, not a
  plugin: managing repos is what Catalyzer is for.
- **`_devSpace`:** `HubWindow.DevSpace` (size chip; cleanup to the Recycle Bin after a listed confirm).
- **Notes:** the NOTES chip and the notes drawer over `docs/NOTES.md`, with git sync. Lives in `Notes` and
  `HubWindow.Notes`.
- **Link:** `.catalyzer\link` mailbox → queue → hidden PowerShell runner → log, exit code and feed. Lives in
  LinkPlugin and `HubWindow.Feed`.
- **Helpers:** start, stop, restart, logs, and a kill-on-close job. Lives in HelpersPlugin.
- **Background work:** the header chip, the close prompt (ask / stop / tray), the tray menu. Lives in
  `HubWindow.Work`.
- **Themes:** the theme service writes `Cz*` resources; 4 built in, plus Nexus (live). Lives in `Theme`.
- **Settings:** the drawer with General, Project folders, Theme, Plugins and About. Lives in
  `HubWindow.Settings` and `Prefs`.

## Data and flow
```
source\repos\*  --scan-->  Project (+ catalyzer.json)  --git.exe-->  GitState  -->  ProjectCard
      |                              |                                               ^
      | docs\NOTES.md  <--Notes-->  notes drawer --SYNC--> git commit/pull/push       | badges (notes, link, helpers)
      | .catalyzer\link\in\*.ps1  --LinkPlugin-->  powershell (hidden, job)  --> out\*.log/.done --> feed
plugins\*\*.dll  --PluginHost-->  themes · actions · badges · sources · background work · feeds
```

| What | Where |
|---|---|
| Settings | `%APPDATA%\Catalyzer\settings.json` |
| Plugin data | `%APPDATA%\Catalyzer\plugin-data\<id>\` (Link: `linked.json`, `run-request.ps1`; Helpers: `logs\`) |
| Log | `%LOCALAPPDATA%\Catalyzer\log.txt` |
| Install | `%LOCALAPPDATA%\Programs\Catalyzer` (bundled plugins in `plugins\`) |

## Outside connections
- **GitHub:** read through `git remote`. NOTES sync pushes; the link's git uses Aaron's credentials.
- **Nexus:** the settings bus and the theme, through NexusPlugin only.
- **Chats (Claude):** through the repo folder: `.catalyzer\link` and `docs\NOTES.md`.
