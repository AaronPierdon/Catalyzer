# HANDOFF — the baton between chats

Each chat rewrites its own section at the end (git keeps history).

## From the first chat — 2026-11-22 (built in the Nexus PC chat, then retired)

### What exists
- **Catalyzer 0.1:** WPF / .NET 10 at `C:\Users\Aaron\source\repos\Catalyzer`.
  - Installed to `%LOCALAPPDATA%\Programs\Catalyzer`, with a Start menu shortcut. It was started once after install.
- **Projects** (`Catalyzer.slnx`):
  - `src/Catalyzer.Sdk` — the plugin API (`Api.Version = 1`);
  - `src/Catalyzer` — the app;
  - `plugins/Catalyzer.Nexus` — `NexusPlugin.dll`.
- **Board:**
  - a card per git repo, or folder with `catalyzer.json`, in `%USERPROFILE%\source\repos` (12 found);
  - each card shows status (CLEAN / n CHANGED / n TO PUSH / TO PULL), branch, last commit, and 21 days of activity;
  - buttons: FOLDER, GITHUB (once the remote is read), CODE, VS, TERMINAL, NEXUS, plus the manifest's own. Extras fold
    into a ⋯ menu;
  - right-click menu: copy path, create / edit catalyzer.json, refresh git, move to front, hide;
  - header: spinning reactor logo, decoding title, live stats, search (Ctrl+F), rescan (F5), add folder, settings;
  - footer info bar.
- **Settings drawer:**
  - General: run at Windows start (HKCU Run, `--autostart` = minimized); reduce motion;
  - project folders: scan roots, single projects, hidden projects;
  - Theme: Catalyst, Plasma, Ember, Ion, plus **Nexus (live)** from the plugin;
  - Plugins: on/off toggles, ADD / IMPORT (.dll with its folder, or .zip), plugins folder, restart button;
  - About.
- **NexusPlugin:**
  - joins the Nexus settings bus as app `catalyzer`;
  - maps the live Nexus look (colours, Rajdhani / Orbitron / Share Tech fonts, glow, FilesBackdrop) onto a
    `CatalyzerTheme` and re-registers it whenever Nexus changes;
  - adds the NEXUS button (opens the folder in Nexus Explorer).

  It talks to Nexus only through **`Nexus.UI.Wpf.NexusPublicApi`**, which was added to Nexus.Kit on the Nexus branch
  `pc/2026-11-22-explorer`, not merged yet.

### Verified
- Snapshots in all five themes plus settings were looked at (`script catalyzer snap` → `.scratch\snap\*.png`). Fixed
  after the first looks:
  - stroked button paths grew 1 px per layout pass, so only one button showed;
  - cards now fill the row;
  - the drawer and menus are now solid;
  - faint text is brighter;
  - an unborn branch showed as "NO".
- Not yet checked on the live screen by Aaron. The GITHUB-button-after-git-load fix went in after the last snapshot.

### How code gets to the PC (cloud chat)
1. Edit in the container. Pack it: `tar czf /mnt/user-data/outputs/catalyzer-src-N.tgz --exclude=bin --exclude=obj .`
   Use a **new name each time**: re-committing the same name delivered a stale copy once.
2. `device_commit_files` it to `~/mnt/Catalyzer/.catalyzer-src-N.tgz`.
3. Extract it on the device in two steps, because git objects are read-only:
   - `tar xzf F --no-same-owner --overwrite --exclude=./.git/objects`
   - `tar xzf F --no-same-owner --skip-old-files ./.git/objects`
4. Build and install through the Nexus helper: `bash ra.sh "script catalyzer snap|install|build|repo" script`. The log
   name is **`script`**.

### Next
1. **GitHub:** Aaron creates an empty private repo `AaronPierdon/Catalyzer` (no README). Then run
   `script catalyzer repo` (remote `origin` is already set; there's no `gh` on the PC; the cloud can't create repos).
2. Aaron's first live look: hover lift, decode, theme switching, the Nexus theme following Nexus Control, the drawer,
   imports.
3. **Helpers** (the original idea): start each project's helper scripts hidden (no console windows), with a status
   LED per card. Likely a `HelpersPlugin`, plus a `helpers` list in `catalyzer.json`. Don't conflict with MAWP's
   helper queue (the shared-queue attempt was scrapped).
4. **Ideas:**
   - a principles badge per card (reads `.nexus-principles`, shows "behind" when older than the principles VERSION);
   - repo plugin actions (pull / push / fetch-all);
   - a tray icon;
   - drag-to-reorder cards.
