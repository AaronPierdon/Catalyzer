# GUIDE — Catalyzer

How Catalyzer works, for someone new to the code. This isn't a full reference: it gives you enough to find your
way, then points at the files. The parts and where they live are in [ARCHITECTURE.md](ARCHITECTURE.md).

## The 2-minute tour
1. `App.Main` (`src/Catalyzer/App.cs`) makes sure only one copy runs. A second start signals the first one, which
   comes back, even from the tray. Then `Boot` (App.cs:57):
   - starts the theme service;
   - makes the `Board`;
   - loads the plugins (`PluginHost.LoadAll`, Plugins.cs:94);
   - shows `HubWindow` and rescans.
2. `Board.Rescan` (Board.cs:27) asks `Projects.Scan` (Projects.cs:24) for every repo in the scan folders. Each one
   becomes a `Project`, read with its `catalyzer.json` by `Projects.Read` (Projects.cs:62). Git is read in the
   background by `Projects.LoadGit` (Projects.cs:101), using hidden `git.exe`.
3. `HubWindow.BuildCards` (HubWindow.cs:182) makes a `ProjectCard` per project. Cards rise in staggered, and their
   text decodes in.
4. Plugins add buttons, badges (chips), themes and background work. The app draws all of them.

## Main classes
| Class | File | What it's for |
|---|---|---|
| `App` | `src/Catalyzer/App.cs` | start-up, single instance, `--snapshot`, `--autostart` |
| `Board` | `src/Catalyzer/Board.cs` | the project list, rescans, the folder watcher |
| `Projects` | `src/Catalyzer/Projects.cs` | finding projects, reading `catalyzer.json`, reading git |
| `ProjectCard` | `src/Catalyzer/ProjectCard.cs` | one card: header, git line, activity, badges, buttons |
| `HubWindow` (+ `.Settings` `.Work` `.Feed` `.Notes`) | `src/Catalyzer/HubWindow*.cs` | the window and its drawers, prompt, tray |
| `GitHub` | `src/Catalyzer/GitHub.cs` | GitHub REST (repos, create) with the key borrowed from git's sign-in; hidden git for clone / push |
| `HubWindow.Welcome` / `.GitHub` / `.NewProject` / `.DevSpace` / `.Dialog` | `src/Catalyzer/HubWindow.*.cs` | first run · the GitHub panel · NEW PROJECT + handoff · the DEV chip · the shared glass dialog |
| `Notes` | `src/Catalyzer/Notes.cs` | reading and editing a repo's `docs/NOTES.md`, and syncing it |
| `PluginHost` | `src/Catalyzer/Plugins.cs` | loads plugins and implements `ICatalyzerHost` |
| `ThemeService`, `Cz` | `src/Catalyzer/Theme.cs`, `Controls.cs` | theme roles as `Cz*` resources; the drawn controls (Glass, CzButton, Chip, CzMenu...) |
| `Prefs` | `src/Catalyzer/Prefs.cs` | settings.json (atomic, tolerant) |
| `LinkPlugin`, `Link` | `plugins/Catalyzer.Link/LinkPlugin.cs` | the link: mailbox, queue, runner, feed |
| `HelpersPlugin` | `plugins/Catalyzer.Helpers/HelpersPlugin.cs` | helpers: start, stop, restart, logs |

## How a feature works
### A card's NOTES chip and the notes drawer
1. `HubWindow.InitNotes` registers a badge called `notes`. Every card asks it for its state through
   `ProjectCard.Badges` (ProjectCard.cs:179).
2. `NotesBadge` counts the open items in `docs/NOTES.md`, cached by file time.
3. Clicking the chip calls `OpenNotes` (HubWindow.Notes.cs:181), which loads `Notes` and draws the sections and
   items.
4. Enter → `Notes.Add` (Notes.cs:80) puts the note at the top of *Inbox*. Ticking → `Notes.Toggle` (Notes.cs:90)
   moves it to *Done* with a date.
5. Sync → `Notes.Sync`. It:
   - commits only NOTES.md, with a generated message;
   - fetches;
   - brings in GitHub's commits only if they touch nothing but NOTES.md (fast-forward, or a merge; a conflict is
     aborted and reported);
   - pushes only if every unpushed commit touches nothing but NOTES.md.

   It's triggered by the SYNC chip on the card (`SyncBadge` / `SyncProject` in HubWindow.Notes.cs), by the drawer
   button, and by auto-sync, 60 s after the last panel edit (`AutoSyncSoon`, setting `NotesAutoSync`).
   `Notes.Status` reads where things stand: it runs at start, every 15 min with a fetch, and whenever the file
   changes. Opening the drawer runs `Notes.PullIfSafe`.
6. A `FileSystemWatcher` per `docs\` folder refreshes the chip and the drawer when a chat edits the file.

### LINK (a hidden terminal for a chat)
1. `LinkPlugin.Toggle` (LinkPlugin.cs:107) → `Link.Activate` (LinkPlugin.cs:221) prepares `.catalyzer\link\`:
   `.gitignore`, `README.md`, `run.sh`, `STATUS`. It sets aside any old requests, then starts a 750 ms poll and a
   15 s heartbeat.
2. A chat runs `run.sh '<PowerShell>'`, which writes `in\<id>.ps1`.
3. `Link.Tick` (LinkPlugin.cs:289) → `PickUp` (LinkPlugin.cs:308) moves the request to `out\` (adding the BOM
   Windows PowerShell needs), reads its options, and queues it.
4. `Link.Launch(Req)` (LinkPlugin.cs:377) starts the hidden runner (`run-request.ps1`) in the kill-on-close
   `Job`. The runner writes `out\<id>.log` and `out\<id>.done` itself.
5. `Tail` streams new log lines into the feed (`IFeed`). `HubWindow.OpenFeed` (HubWindow.Feed.cs:93) shows them.

### Closing with work running
`HubWindow.InitWork` → the `Closing` handler. If `PluginHost.Running > 0`, it does what `Prefs.CloseWithWork`
says: `Prompt` (HubWindow.Work.cs:112), `ToTray` (HubWindow.Work.cs:180), or `Exit`, which stops everything.

## Key methods worth knowing
- `Board.Rescan()` (Board.cs:27): reread every project (F5 does this).
- `Prefs.Save(p => ...)` (Prefs.cs:51): change a setting. Written atomically; never in snapshot mode.
- `ThemeService.Register / Apply` (Theme.cs:28, 39): add or replace a theme; make one current.
- `CzMenu.Show(anchor, header, items)` (Controls.cs:505): our glass menu (never a native one).
- `PluginHost.UpdateBadges()`: ask every card to redraw its chips.
- `Log.Write(text, show: true)`: the log file, plus the footer info bar.

## Where to change things
- **A colour or the look:** themes in `Theme.cs`; the drawn controls in `Controls.cs`. Never put a colour in UI
  code: ask for a role (`CzAccentBrush`, `Tone.Ok`...).
- **A setting:** `Prefs.cs` (the data), plus `HubWindow.Settings.cs` (its row in the drawer).
- **A card button:** a project's own buttons go in its `catalyzer.json` → `actions`; global ones go in a plugin's
  `AddAction`.
- **A new plugin:** copy `plugins/Catalyzer.Helpers` (smallest with API 2), and see `docs/PLUGINS.md`.
- **What a chat reads about the link:** `plugins/Catalyzer.Link/Assets/README.md`. Catalyzer writes it into every
  linked repo.
