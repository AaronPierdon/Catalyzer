# Catalyzer

**The hub for making and maintaining Aaron's software.** Two things live in this repo:
- **the app**, a project command center for Windows;
- **the Catalyzer Development Principles** in [`principles/`](principles/README.md), the rulebook every app
  follows: the look, how apps are built, the workflow with Claude, and what every repo contains.

The app is a project command center for Windows. Every repo you own floats on the board as a glass card showing:
- what it is;
- where its git stands (branch, changes, commits to push or pull, the last commit);
- three weeks of activity;
- one-click buttons: folder, GitHub, VS Code, Visual Studio, terminal, plus anything the project or a plugin adds.

Git is read with a hidden `git.exe`, so you never see a console window.

![catalyzer](docs/catalyzer.png)

## Run
- Visual Studio or the .NET 10 SDK: open `Catalyzer.slnx` and run `Catalyzer`. The Nexus plugin needs the Nexus repo next to this one (`..\Nexus`).
- Install: `tools\install.ps1`. It builds a Release copy to `%LOCALAPPDATA%\Programs\Catalyzer` with the plugins in `plugins\` and adds a Start menu shortcut.
- To start Catalyzer with Windows (minimized), turn on Settings → General → *Run at Windows start*.

Keys: `F5` rescans, `Ctrl+F` searches, `Esc` closes settings or clears the search. Right-click a card for more options. Double-click a card to open its folder.

## Getting started
- **First start:** a welcome asks where your projects live (default `source\repos`) and signs in to GitHub. It uses
  the sign-in git already has on the PC; Catalyzer never stores the key.
- **`+`** in the header: **New project** (folder, docs, first commit, GitHub repo, push, then what to paste into
  Claude), **Clone from GitHub**, or **Add an existing folder**.
- **The GitHub button** lists every repo on your account against this PC: CLONE what's missing, PUSH what's ahead,
  PUBLISH what's only local.
- **Each card** has chips:
  - NOTES (and SYNC when notes and GitHub differ);
  - LINK (a hidden terminal for Claude);
  - HELPERS, if the project declares any;
  - DEV (its `_devSpace` scratch folder: open it, or clean it up into the Recycle Bin after a list).
- **Uninstall:** Settings → Apps → Catalyzer → Uninstall. Your projects are never touched.

## Projects
Catalyzer shows every git repo, or folder with a `catalyzer.json`, directly inside the scan folders. The default scan folder is `%USERPROFILE%\source\repos`. It skips `archives`, `node_modules`, `temp` and dot-folders.

You can add more folders in Settings, or with **+**:
- pick a project folder and Catalyzer adds that project;
- pick a folder of projects and Catalyzer scans it.

A project describes itself with an optional `catalyzer.json` (name, description, colour, glyph, its own buttons, its helper scripts). See [docs/design/MANIFEST.md](docs/design/MANIFEST.md). New projects need no code changes, and a new repo shows up on the board by itself.

## Notes
Every card has a **NOTES** chip for its repo's `docs/NOTES.md`: your own notepad for that project. Click the chip,
type, press Enter: the note lands at the top, stamped with the date and time. Chats read your notes when you ask
("pull my notes in") and never edit them; once they're pulled in, **WIPE + SYNC** clears the file for a fresh slate.
- **Sync:** a minute after your last edit, Catalyzer commits only `docs/NOTES.md` and pushes it; opening the notes
  brings in notes written elsewhere; a **SYNC ↑ / ↓** chip shows when notes and GitHub differ. It never pushes anyone
  else's unpushed work, and never pulls in anything but notes. Auto-sync can be turned off in Settings → General.
- **Backups:** every version Catalyzer sees is kept in `%LOCALAPPDATA%\Catalyzer\notes-cache\`, up to a cap per
  project (Settings → General, 100 MB by default; OFF takes none). **BACKUPS** in the drawer opens a project's.
  A project's backups are deleted when it leaves the board.

## Link
Click **LINK** on a project's card and that repo gets a hidden Windows terminal that a chat (Claude) can use for
builds, git and installs. There are no windows, and every command shows in the card's feed. See
[docs/design/LINK.md](docs/design/LINK.md).

## Helpers
List a project's long-running scripts under `helpers` in its `catalyzer.json`. Catalyzer then:
- starts them hidden from the card (no console windows) and logs their output;
- shows HELPERS n/m on the card;
- keeps them in a job that stops them if Catalyzer stops.

Closing with helpers running asks whether to stop them all or keep them running in the tray. See
[docs/design/MANIFEST.md](docs/design/MANIFEST.md).

## Extending it
- **Plugins** are .NET dlls built on `Catalyzer.Sdk`. They can:
  - add card buttons;
  - add project sources;
  - add themes, including live ones that repaint as they change;
  - log to the info bar.

  Manage them under Settings → Plugins: switch each on or off, or use Add / Import (a .dll with its folder, or a .zip). See [docs/design/PLUGINS.md](docs/design/PLUGINS.md).
- **Themes** live in Catalyzer's own theme layer (`CatalyzerTheme`: colours, fonts, glow, cut, scan lines, backdrop). Four themes are built in. Plugins register more.
- **NexusPlugin** (`plugins/Catalyzer.Nexus`) shows the pattern:
  - it reads Nexus through Nexus's public `NexusPublicApi`;
  - it maps the live Nexus look onto a Catalyzer theme;
  - it adds "Open in Nexus Explorer".

  The base app knows nothing about Nexus.

## Data
- Settings: `%APPDATA%\Catalyzer\settings.json`
- User plugins: `%APPDATA%\Catalyzer\plugins\<name>\`
- Plugin data: `%APPDATA%\Catalyzer\plugin-data\<id>\`
- Log: `%LOCALAPPDATA%\Catalyzer\log.txt`

`Catalyzer.exe --snapshot <dir>` renders the board in every theme to PNGs.
