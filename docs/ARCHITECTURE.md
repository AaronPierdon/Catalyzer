# ARCHITECTURE — Catalyzer

The inventory and the reasons: what Catalyzer is built from and why each piece was chosen. What it does for the
user is in [SUMMARY.md](SUMMARY.md). Contracts other code depends on are in `design/`: the link
([LINK](design/LINK.md)), `catalyzer.json` ([MANIFEST](design/MANIFEST.md)) and the plugin API
([PLUGINS](design/PLUGINS.md)).

## Stack at a glance
| Layer | Technology | Why this |
|---|---|---|
| App | .NET 10, WPF (`net10.0-windows`) | Windows-only desktop hub; same stack as Nexus, so the look and Nexus.Kit carry over |
| UI | code-built WPF: `OnRender` + `FrameworkElement`s, no XAML | chamfers, glow, decode-in text and live theme roles are easier to own in code than to fight in templates |
| Tray | Windows Forms `NotifyIcon` only | WPF has no tray icon; the menu itself is our glass `CzMenu` |
| Git | hidden `git.exe` processes | uses Aaron's real git config and Git Credential Manager, behaves exactly like his terminal, no native library to ship |
| GitHub | REST through `HttpClient`, the token borrowed from Git Credential Manager per request | no `gh` dependency and no stored secret: the key is never written or logged |
| Running things | Windows PowerShell 5.1, hidden, in a Job object (kill-on-close) | ships with Windows; the job guarantees nothing outlives Catalyzer |
| Settings / JSON | `System.Text.Json` | built in; settings are written atomically (temp file + move) and read tolerantly |
| Extensibility | plugins: .dlls loaded into the default context against `Catalyzer.Sdk` | simple and fast; the cost (no unloading, so plugin on/off needs a restart) is accepted |

## Parts
| Part | Folder / project | Owns |
|---|---|---|
| **App** | `src/Catalyzer` | window, board, cards, drawers (settings, notes, feed, GitHub), dialogs (welcome, new project, cleanup), theme layer, tray, plugin host |
| **SDK** | `src/Catalyzer.Sdk` | the plugin API (API 3): themes, card actions and badges, project sources, background work, feeds, plugin settings. Add-only |
| **NexusPlugin** | `plugins/Catalyzer.Nexus` | the live Nexus theme (through `NexusPublicApi`) and the NEXUS button |
| **LinkPlugin** | `plugins/Catalyzer.Link` | LINK per card: a hidden Windows terminal through a mailbox in the repo |
| **HelpersPlugin** | `plugins/Catalyzer.Helpers` | a project's long-running scripts (`helpers` in `catalyzer.json`) |
| **Principles** | `principles/` | the Catalyzer Development Principles and the templates NEW PROJECT copies. Docs only, versioned in `principles/VERSION` |
| **tools** | `tools/` | `install.ps1` (Release → `%LOCALAPPDATA%\Programs\Catalyzer`, principles bundled), `uninstall.ps1`, `snap.ps1` |

The app never references a plugin or Nexus. Plugins only see the SDK.

## Libraries
| Library | Used for | Why this one |
|---|---|---|
| .NET 10 BCL (`System.Text.Json`, `HttpClient`, `FileSystemWatcher`, `Process`) | everything not UI | no third-party packages to update or audit |
| `Microsoft.VisualBasic.FileIO` | `_devSpace` cleanup to the Recycle Bin | the only built-in way to recycle rather than delete |
| Nexus.Kit (NexusPlugin only) | the live Nexus look | Nexus publishes its theme there; the app itself stays independent |

## Main classes
| Class | File | What it's for |
|---|---|---|
| `App` | `src/Catalyzer/App.cs` | start-up, single instance, `--snapshot`, `--autostart` |
| `Board` | `Board.cs` | the project list, rescans, the folder watcher |
| `Projects` | `Projects.cs` | finding projects, reading `catalyzer.json`, reading git |
| `ProjectCard` | `ProjectCard.cs` | one card: header, git line, activity, badges, buttons |
| `HubWindow` (partial: `.Settings` `.Work` `.Feed` `.Notes` `.GitHub` `.NewProject` `.Welcome` `.DevSpace` `.Dialog`) | `HubWindow*.cs` | the window and everything on it |
| `Notes` | `Notes.cs` | reading and editing `docs/NOTES.md`, and syncing it (NOTES.md only) |
| `NotesCache` | `NotesCache.cs` | the notes backups: snapshot each new version, cap the size, purge projects that left |
| `GitHub` | `GitHub.cs` | GitHub REST (repos, create) and hidden git for clone / push |
| `PluginHost` | `Plugins.cs` | loads plugins, implements `ICatalyzerHost` |
| `ThemeService`, `Cz` | `Theme.cs`, `Controls.cs` | theme roles as `Cz*` resources; the drawn controls (Glass, CzButton, Chip, CzMenu…) |
| `Prefs`, `Log` | `Prefs.cs` | settings.json; the log (rolls at 1 MB) |
| `LinkPlugin`, `Link` | `plugins/Catalyzer.Link/LinkPlugin.cs` | the link: mailbox, queue, runner, feed |
| `HelpersPlugin` | `plugins/Catalyzer.Helpers/HelpersPlugin.cs` | helpers: start, stop, restart, logs, trust |

## How the main flows work
### Start-up
1. `App.Main` keeps one copy running (a second start brings the first back, even from the tray), then `Boot`:
   theme service → `Board` → `PluginHost.LoadAll` → `HubWindow` → rescan.
2. `Board.Rescan` → `Projects.Scan` finds the repos; `Projects.Read` reads each `catalyzer.json`; `Projects.LoadGit`
   reads git in the background.
3. `HubWindow.BuildCards` makes a `ProjectCard` per project; plugins add buttons, badges, themes and work.

### Notes
1. `HubWindow.InitNotes` registers the `notes` and `notes-sync` badges; `NotesBadge` counts notes, cached by file
   time.
2. `OpenNotes` loads `Notes` and draws the list. Enter → `Notes.Add` (top of `## Notes`, stamped
   `yyyy-MM-dd HH:mm`).
3. `Notes.Sync` commits NOTES.md alone, fetches, brings in GitHub's commits only if they touch nothing but NOTES.md
   (fast-forward or merge; a conflict is aborted and reported), and pushes only if every unpushed commit is
   notes-only. Triggered by the SYNC chip, the drawer, and auto-sync 60 s after the last edit.
4. **WIPE + SYNC:** confirm → `PullIfSafe` → backup → `Notes.Wipe` (keeps the intro, empties the notes) → `Sync`.
5. A `FileSystemWatcher` per `docs\` folder refreshes the chip and drawer when anything changes the file, and
   `NotesCache.Snapshot` saves the new version.

### LINK
1. `LinkPlugin.Toggle` → `Link.Activate` prepares `.catalyzer\link\` (`.gitignore`, `README.md`, `run.sh`, `STATUS`),
   sets aside old requests, starts a 750 ms poll and a 15 s heartbeat.
2. A chat runs `run.sh '<PowerShell>'`, which writes `in\<id>.ps1`.
3. `Link.Tick` → `PickUp` moves it to `out\` (adding the BOM PowerShell 5.1 needs), reads its options, queues it.
   Requests tracked by git are refused.
4. `Link.Launch` starts the hidden runner in the kill-on-close job; it writes `out\<id>.log` and `.done`.
5. `Tail` streams the log into the card's feed.

### Closing with work running
`HubWindow.InitWork` → `Closing`: if `PluginHost.Running > 0`, `Prefs.CloseWithWork` decides: `Prompt`, `ToTray` or
`Exit` (stops everything).

## Data
```
source\repos\*  --scan-->  Project (+ catalyzer.json)  --git.exe-->  GitState  -->  ProjectCard
      | docs\NOTES.md  <--Notes-->  notes drawer --SYNC--> git commit/pull/push      | badges (notes, link, helpers)
      |        \--watcher--> NotesCache --> %LOCALAPPDATA%\Catalyzer\notes-cache\<project>\
      | .catalyzer\link\in\*.ps1  --LinkPlugin-->  powershell (hidden, job)  --> out\*.log/.done --> feed
plugins\*\*.dll  --PluginHost-->  themes · actions · badges · sources · background work · feeds
```
Where each store lives is in [SUMMARY → Where its data lives](SUMMARY.md#where-its-data-lives).

## Decisions and patterns
- **Repos are the source of truth; Catalyzer keeps no database.** Everything it shows is read from the folder,
  git and `catalyzer.json`, so a chat and Catalyzer always agree.
- **The link is a file mailbox, not a server.** Nothing listens on a port; a chat already sees the repo folder, so
  that's the channel. Requests that arrived with a pull or clone (tracked by git) are refused.
- **Notes sync touches NOTES.md only.** It never pushes a chat's unpushed work and never pulls code; anything else
  is left for the project's chat.
- **Nothing runs from a cloned repo until Aaron says so.** A helper's `auto` works only after a first manual start
  (`trusted.json`).
- **Never delete for the user.** GitHub actions don't delete; `_devSpace` cleanup lists first and recycles.
- **The SDK is add-only.** `Api.Version` goes up when something arrives; nothing is renamed or removed, and plugins
  declare a `MinApi`.
- **No colour literals.** UI asks for roles (`CzAccentBrush`, `Tone.Ok`); themes fill them.

## Where to change things
- **The look:** themes in `Theme.cs`, drawn controls in `Controls.cs`.
- **A setting:** `Prefs.cs` (data) + `HubWindow.Settings.cs` (its row).
- **A card button:** a project's own go in its `catalyzer.json`; global ones in a plugin's `AddAction`.
- **A new plugin:** copy `plugins/Catalyzer.Helpers`; see [design/PLUGINS.md](design/PLUGINS.md).
- **What a chat reads about the link:** `plugins/Catalyzer.Link/Assets/README.md` (written into every linked repo).

## Outside connections
- **GitHub:** git remotes and REST, with Aaron's credentials from Git Credential Manager.
- **Nexus:** the settings bus and theme, through NexusPlugin only.
- **Claude chats:** through the repo folder: `.catalyzer\link`, `docs\NOTES.md`, `principles/`.
