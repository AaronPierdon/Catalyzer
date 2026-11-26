# HANDOFF — Catalyzer

The baton between chats, and Claude's working list. Short and current: each chat rewrites *Last session* and keeps
the lists true. History is in git, not here.

## Last session — 2026-11-26 (2)
- **Did (Catalyzer 1.5.0, API 4, Link 1.1.0): Aaron's notes of 08:24 and 08:26.**
  - **Edit a note** in place (double-click, or right-click → Edit): keeps its stamp and check box, finds it by text if
    the file moved, backs up first; Esc cancels. Synced notes too (auto-sync pushes the edit).
  - **Card mood** (`BadgeState.Card`, API 4): LINKED → select tint; link RUNNING / git WORKING / INSTALLING / SYNCING
    → full energy (MAWP's busy damp: rise 2.2, settle 1.2; the two colours shift as a travelling wave, the brackets
    reach, a sheen sweeps); CONFLICT / INSTALL FAILED → red, DIVERGED → amber, breathing. Paced as status (`Pace.Pulse`:
    slow in ECO, still colour in STATIC).
  - **Card colours:** right-click → Card colours: primary + secondary from every theme's colours or a #hex, live
    preview (rest / working). `Prefs.CardColors`, not the repo.
  - Why MAWP TV is purple and MAWP Android blue: each repo's `catalyzer.json` sets `accent` (`#B26BFF`, `#3AA8FF`).
- **Verified:** builds clean; snapshots looked at (linked cards tint). Not tried by hand: editing a note, the colours
  dialog, a working card's motion.
- **Ready to test:** link a repo and run something (card should come alive); double-click a note; right-click a card →
  Card colours.

## Earlier — 2026-11-26
- **Did (Catalyzer 1.4.0): visuals levels, for the RDP file server (6600H ran hot).**
  - `Visuals.cs`: FULL / BALANCED / ECO / STATIC, one level **on this PC** (default FULL) and one **over Remote
    Desktop** (default ECO; RDP renders WPF in software, so every frame and DropShadow is CPU). Settings → *Visuals*,
    plus *Hex floor* and *Reduce motion* (moved there). Prefs: `visualsLocal`, `visualsRemote`, `hexFloor`.
  - Every frame clock (`Cz.Tick`) is paced by the level (`Pace.Ambient / Pulse / Feedback`), stops when its owner is
    hidden or done, and wakes on demand: cards no longer tick at 60 fps at rest, LEDs only tick while pulsing.
  - Ambience (backdrop, reactor, LED pulses) stops while minimized, in the tray, locked or the RDP session is
    disconnected (`SystemEvents.SessionSwitch`).
  - Below FULL the theme's own backdrop (the Nexus plugin's, unthrottled) is swapped for Catalyzer's; ECO and STATIC
    drop glow (live: existing glow layers are cleared).
- **Principles 6.0.0:** new hard rule 05 #11, commit and push every piece of work straight away, in every project
  (04, 06, PROMPTS, template CLAUDE.md, this CLAUDE.md follow). Every other app needs the Update prompt.
- **Principles 6.1.0:** the wrap-up list (04 *Working with Aaron*, 06): 4+ features or fixes → one line each, `check:`
  on the risky ones.
- **Principles 7.0.0:** hard rule 05 #12, nothing personal or machine-specific in any repo (so repos can go
  public); temp data in `_devSpace/`. Catalyzer enforces it: `Repo` adds `_devSpace/` to every repo's
  `.git/info/exclude` and never lists or commits a `_devSpace` path (it was only flagged before).
- **Verified:** builds clean (Linux, `EnableWindowsTargeting`). **Not yet run on Windows or snapshotted.**
- **Ready to test:** on the server, Settings → Visuals shows *OVER REMOTE DESKTOP ◂ NOW*; compare CPU in Task
  Manager at FULL vs ECO.

## Previous session — 2026-11-25
- **Did (Catalyzer 1.3.1, principles 5.1.0):**
  - **1.3.1:** the git chip works with a remote that isn't called `origin` (Nexus's is `Nexus`): it uses the
    branch's tracked remote, else `origin`, else the only one; fetch is `--all`. It said NOT ON GITHUB before.
  - **Git on the card:** a GIT chip per card (PULL ↓n / PUSH ↑n / PUBLISH BRANCH / COMMIT n / DIVERGED / CONFLICT /
    ✓ IN STEP), click does the next step; the git drawer (incoming / outgoing commits, changed files with
    checkboxes and risk flags, message, COMMIT, COMMIT + PUSH); FETCH ALL in the header plus a quiet 15-minute
    fetch; a TO PULL stat. Odd states block the buttons and open Visual Studio on Git Changes (`View.GitWindow`),
    or bring an already-open Visual Studio to the front.
  - **One-click update:** the UPDATE chip when the repo's `<Version>` is newer than the running copy;
    `install.ps1` now builds first, then closes, installs on top and restarts, and restarts the old copy if a
    later step fails (`-Log`, `install.cmd`).
  - Notes sync now says "NOTES WAIT FOR n UNPUSHED COMMIT(S)" instead of the vague message.
  - **1.3.0 / principles 5.1.0: UPDATE on every app's card.** The install contract (build first, close, install on
    top, restart, `-Log`, exit 0 / 1, old copy back on failure) in 07 and the template `install.ps1`; `<Version>`
    bumped by every shipped change; 04: with the link off a chat ends with "Build and install: click UPDATE on the
    <App> card". `HubWindow.Apps`: the chip (UPDATE a → b / INSTALL / INSTALLING / ✓ INSTALLED / INSTALL FAILED),
    paths from `catalyzer.json` "app" or the template layout; old scripts without `-Log` still run, output captured.
  - **1.2.1: close to the tray** by default (X hides; tray right-click → Exit; one-time balloon hint; Run at
    Windows start goes to the tray; a Windows shutdown really quits). Setting: General → *Close to the tray*.
  - 1.1.0 (yesterday): principles 5.0.0, the notes rework, the cleanup.
- **Verified:** the app and Link / Helpers plugins build clean. `Repo` passed 31 scenarios against real repos with a
  bare remote: in step, behind clean / dirty / untracked-only, ahead, push while GitHub moved, diverged,
  mid-merge conflict, detached, index.lock, link busy, new branch publish, same-name branch on GitHub (refused),
  deleted-on-GitHub with and without unique commits, no remote, commit of exactly the ticked files with something
  else staged, `.env` and line-ending flags. **Not yet run on Windows or looked at.**
- **Ready to test:** double-click `tools\install.cmd` once (1.1.0 doesn't have the UPDATE chip yet). Then the GIT
  chip on Catalyzer's own card should say PUSH ↑3: click it. Open the git panel on a repo with changes; try
  Visual Studio from a card's right-click menu.

## Next
0. Before Catalyzer goes public: scan its whole git history for rule 12 (IPs, emails, user paths, keys).
1. Aaron installs 1.2.0 (`tools\install.cmd`), pushes Catalyzer from its GIT chip, then the NOTES SYNC chip
   clears.
2. Snapshot and look at: the git drawer, the GIT chips, the header (FETCH ALL, UPDATE chip), the notes drawer.
3. Check that `/Command View.GitWindow` opens Git Changes in his Visual Studio (if it complains, the command name
   changes in `OpenVsGit`).
4. ROADMAP → *Now* (chunk A): the principles badge, then MAWP's docs to 5.0.0.
5. Helpers: add real `helpers` to the projects that have them (ask Aaron which).

## Bugs

## Known gaps
- A helper's (or link request's) grandchildren started in its first milliseconds could escape the job (it's
  assigned right after start); in practice PowerShell takes far longer to start.
- The GitHub panel lists the repos the PC's git sign-in can see. A second account (StackView is under `Aetrocles`)
  shows only if that sign-in has access.
- The Nexus helper must not be a Catalyzer helper: installing Catalyzer would kill it mid-install. Through the link,
  install Catalyzer with `run.sh -s` (survive).
- The link runs whatever it's sent, as Aaron: the chat's rules are the guard, and the feed is the record.
- Plugin on/off and import need a restart (assemblies can't unload from the default context).
- Notes backups are read by a chat through the link only (`%LOCALAPPDATA%` isn't in the repo folder).
