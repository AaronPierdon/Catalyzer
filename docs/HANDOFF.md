# HANDOFF — the baton between chats

Each chat rewrites its own section at the end (git keeps history).

## From the "how I work with Claude" chat — 2026-11-23 (phone, via GitHub; no PC access)

### Decided
Catalyzer is **where every new project starts**. Aaron's pipeline, now written into the principles (2.1.0, 04 *Setting
up a project in Claude*, PROMPTS → *Kickoff* and *Morning brief*):
1. Open Catalyzer → new project. It shows the setup steps and one complete kickoff prompt: the GitHub link, how to
   structure the repo, how to work with Catalyzer, and the paths of the Catalyzer and principles repos.
2. Aaron makes the GitHub repo and a Claude project **in the desktop app on that computer, from the repo folder**, so
   every chat in the project starts linked. A folder link belongs to one computer and needs that computer's desktop
   app open.
3. A scheduled **Morning brief** inside the project, with the folder chosen ("Require this computer"), opens a fresh,
   briefed chat each morning, or on demand from the phone with Run now. It reads only the handover files and git, to
   save usage.

Checked in the docs: `claude://` links only **prefill** prompts and always ask before using a folder. So a launcher that
automates the desktop app is out. Scheduled tasks do the job.

### Sync check
This repo is on principles **1.0.0** and current is **2.1.0**. 2.0.0's work (BACKLOG → NOTES, VISION / ARCHITECTURE /
GUIDE) isn't done. Run PROMPTS → *Update* in the next Catalyzer chat.

### Next (first move first)
1. **Update to principles 2.1.0** (PROMPTS → *Update*).
2. **NEW PROJECT** (BACKLOG, now fully specified): Catalyzer creates the GitHub repo, folder, templates and first push
   by itself, then hands off to Claude with copy buttons. Every project starts here.
3. **GitHub dashboard** (BACKLOG): list all repos, public and private, with URLs; rename and delete optional.
4. Everything under the first chat's *Next* below still stands.
## From the helpers / link chat — 2026-11-22 (cloud chat linked to the PC)

### What this chat did
- **GitHub:** pushed `AaronPierdon/Catalyzer` (`origin` had been missing) and `AaronPierdon/Nexus-Development-Principles`.
  The principles path has spaces, so the push went through the Nexus helper with the short name `..\NEXUSD~1`.
- **Branch `pc/2026-11-22-helpers`** (pushed; merge after Aaron's live look):
  - **Link** (`plugins/Catalyzer.Link`, `docs/LINK.md`), Aaron's new way of working:
    - LINK on a card gives that repo a hidden Windows terminal for chats, through a mailbox at `.catalyzer\link\`
      that Catalyzer git-ignores itself;
    - Catalyzer writes `README.md` and `run.sh` into the mailbox, so a new chat discovers how to use it;
    - nothing listens on the network; requests run hidden in the repo root, one at a time, inside a kill-on-close job;
    - survive mode (`-s`) is for installing Catalyzer itself;
    - every command shows in the card's feed drawer;
    - links are remembered and relink at start;
    - requests found waiting at link time are set aside, never run.
  - **Helpers** (`plugins/Catalyzer.Helpers`): a project's long-running scripts, from `helpers` in its
    `catalyzer.json`, run hidden with a card chip.
  - **SDK API 2:**
    - badges (`ProjectBadge`, now with `Click`);
    - `IBackgroundWork`;
    - `IFeed` / `ShowFeed`;
    - `IsSnapshot`.
  - **App:**
    - the header chip for running work;
    - the close prompt (STOP ALL & EXIT / KEEP IN TRAY, remembered; Settings → General);
    - a tray icon with a glass menu;
    - a second start restores the window from the tray;
    - a folder watcher, so new repos appear by themselves;
    - the feed drawer.
  - `tools/snap.ps1`: Catalyzer builds and snapshots itself, so it no longer needs Nexus's script once linked.
- **Principles 1.1.0, then 2.0.0** (their own repo, pushed). 1.1.0: chats work through the Catalyzer link. 2.0.0: the
  principles cover how apps are developed:
  - every app keeps `docs/NOTES.md` (BACKLOG is merged in), `VISION.md`, `ARCHITECTURE.md` and `GUIDE.md`;
  - starter files are in `templates/`;
  - every chat reads two repos and runs a sync check;
  - 08-ASSETS sets the direction for reusable assets.
- **Notes panel:**
  - a NOTES chip on every card (open items; "n NEW" when the Inbox has some);
  - the notes drawer: add with Enter (goes to the Inbox), tick (moves to Done with a date), right-click to move,
    RAW to edit the whole file, SYNC (commit NOTES.md only, then pull --rebase --autostash, then push);
  - repos still on BACKLOG.md get a warning chip.
- **Catalyzer conforms to 2.0.0:** NOTES (from BACKLOG), VISION, ARCHITECTURE and GUIDE are written from the code,
  and CLAUDE.md has the new reading order.

### Verified
- On the PC: the build, an install of the helpers stage, and a Heartbeat test helper running hidden (its log ticks
  every 5 s).
- Snapshots of the helpers stage looked at. The link stage is covered under *Ready to test* below.

### Ready to test
- **Uncommitted test rig:** `catalyzer.json` has two test helpers (`.scratch\test-*.ps1`). The original is in
  `.scratch\catalyzer.json.bak`. **Restore it before merging.**
- **Link:** after the install, Aaron clicks LINK on the Catalyzer card. Then from a chat:
  1. `bash .catalyzer/link/run.sh --status`
  2. `bash .catalyzer/link/run.sh 'git status'`
  3. `bash .catalyzer/link/run.sh -t 600 -f tools/snap.ps1`
  4. check that the feed shows all three.
- **Notes:** click NOTES on the Catalyzer card. Add a note, tick one, right-click and move one, try RAW, then SYNC
  (it commits `docs/NOTES.md` on the current branch and pushes).

### Next
1. Aaron's live look, then: restore `catalyzer.json`, merge to `main`, install through the link (`-s`), push.
2. Aaron links his other projects: the phone app and the two desktop apps. Each repo's CLAUDE.md gets the
   "running things on the PC" lines (Principles 1.1.0 → *update to the latest principles* in each app's chat).
3. Principles badge on cards (NOTES → Next up).

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
