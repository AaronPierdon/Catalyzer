# HANDOFF — the baton between chats

Each chat rewrites its own section at the end (git keeps history).

## From the helpers chat — 2026-11-22 (cloud chat linked to the PC)

### What this chat did
- **GitHub:** pushed `AaronPierdon/Catalyzer` (`script catalyzer repo`; `origin` had been missing) and
  `AaronPierdon/Nexus-Development-Principles`. The principles path has spaces, which the helper's argument split
  can't take, so the push used the short name: `script catalyzer git -C ..\NEXUSD~1 push -u origin main`.
- **Branch `pc/2026-11-22-helpers`** (pushed, not merged):
  - **SDK API 2** (add-only): `ProjectBadge` / `BadgeState` / `MenuEntry` / `Tone`, `IBackgroundWork`,
    `ICatalyzerHost.IsSnapshot`, `AddBadge`, `UpdateBadges`, `AddBackgroundWork`. Documented in `docs/PLUGINS.md`.
  - **HelpersPlugin** (`plugins/Catalyzer.Helpers` → `HelpersPlugin.dll`):
    - reads `helpers` from each `catalyzer.json` (`docs/MANIFEST.md`);
    - runs every helper hidden (CreateNoWindow) inside a kill-on-close job object;
    - logs to `%APPDATA%\Catalyzer\plugin-data\helpers\logs`;
    - shows a card chip (HELPERS n/m) with start / stop / open log;
    - supports `auto` (once a session) and `restart` (3 tries a minute).

    Its Debug build copies itself into the app's Debug `plugins\Helpers`, so `script catalyzer snap` loads it
    without touching Nexus's script.
  - **App:**
    - the header chip "n HELPERS" (stop each or all);
    - closing with work running follows `closeWithWork` in settings (`ask` default / `stop` / `tray`): a glass
      prompt with STOP ALL & EXIT / KEEP IN TRAY and a Remember toggle;
    - Settings → General lets you change the close choice later;
    - a tray icon with a Catalyzer glass menu (open, stop each, stop all, exit);
    - a second start now signals the first copy (`Local\Catalyzer.Show`), which comes back from the tray.
  - **Folder watcher:** each scan root is watched one level deep, and each project for its own `catalyzer.json`.
    Nothing is recursive, so builds never wake it. Changes trigger a debounced rescan after 1.5 s.
  - `--snapshot` also renders `catalyzer-close.png` (the close prompt).

### Verified
- It builds on the PC.
- Snapshots in all five themes, plus settings and the close prompt, were looked at:
  - the Catalyzer card shows HELPERS 0/2 (snapshot runs never start helpers);
  - fixed after the first look: the prompt is wider so Remember isn't clipped, and the tray glyph changed.
- Not yet seen live:
  - helpers actually running (the LED, logs, restart);
  - the tray;
  - the prompt from a real ✕;
  - the watcher picking up a fresh clone.

### Ready to test (on Aaron's PC)
- **Uncommitted test rig:** `catalyzer.json` has two test helpers, pointing at `.scratch\test-heartbeat.ps1`
  (auto, writes every 5 s) and `.scratch\test-fail.ps1` (exits 3). The original is in `.scratch\catalyzer.json.bak`.
  **Restore it before merging.**
- Install (`script catalyzer install`) restarts Catalyzer only (not Nexus). Then:
  1. HEARTBEAT should be on and the chip should read HELPERS 1/2, and no window should appear.
  2. Start the fail test: the chip should turn red and the log should show `! on purpose`.
  3. Press ✕: the prompt should appear. Try KEEP IN TRAY, then right-click the tray icon and try Exit. Check in
     Task Manager that no powershell is left.
  4. Kill Catalyzer from Task Manager while the heartbeat runs: its powershell should die too.

### Next
1. Aaron's live look at the above, then: restore `catalyzer.json`, merge to `main`, install, push.
2. Give real projects their `helpers`. Ask Aaron which scripts. Not the Nexus helper (see MANIFEST).
3. Principles badge (BACKLOG).

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
