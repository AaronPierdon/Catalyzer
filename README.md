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

## Projects
Catalyzer shows every git repo, or folder with a `catalyzer.json`, directly inside the scan folders. The default scan folder is `%USERPROFILE%\source\repos`. It skips `archives`, `node_modules`, `temp` and dot-folders.

You can add more folders in Settings, or with **+**:
- pick a project folder and Catalyzer adds that project;
- pick a folder of projects and Catalyzer scans it.

A project describes itself with an optional `catalyzer.json` (name, description, colour, glyph, its own buttons, its helper scripts). See [docs/MANIFEST.md](docs/MANIFEST.md). New projects need no code changes, and a new repo shows up on the board by itself.

## Notes
Every card has a **NOTES** chip for its repo's `docs/NOTES.md`, the notepad you and every chat share. Click the chip to
add a note (it lands in the Inbox), tick things done, or move them between sections. Notes **sync themselves**:
- a minute after your last edit, Catalyzer commits only `docs/NOTES.md` (with a message like
  `Notes: +1 inbox, 2 done (Catalyzer, 2026-11-23 15:31)`) and pushes it;
- opening the notes brings in notes written elsewhere;
- a **SYNC ↑ / ↓** chip on the card shows whenever notes and GitHub differ, and clicking it syncs now.

It never pushes anyone else's unpushed work, and never pulls in anything but notes. You can turn auto-sync off in
Settings → General.

## Link
Click **LINK** on a project's card and that repo gets a hidden Windows terminal that a chat (Claude) can use for
builds, git and installs. There are no windows, and every command shows in the card's feed. See
[docs/LINK.md](docs/LINK.md).

## Helpers
List a project's long-running scripts under `helpers` in its `catalyzer.json`. Catalyzer then:
- starts them hidden from the card (no console windows) and logs their output;
- shows HELPERS n/m on the card;
- keeps them in a job that stops them if Catalyzer stops.

Closing with helpers running asks whether to stop them all or keep them running in the tray. See
[docs/MANIFEST.md](docs/MANIFEST.md).

## Extending it
- **Plugins** are .NET dlls built on `Catalyzer.Sdk`. They can:
  - add card buttons;
  - add project sources;
  - add themes, including live ones that repaint as they change;
  - log to the info bar.

  Manage them under Settings → Plugins: switch each on or off, or use Add / Import (a .dll with its folder, or a .zip). See [docs/PLUGINS.md](docs/PLUGINS.md).
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
