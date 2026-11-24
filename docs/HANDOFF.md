# HANDOFF — Catalyzer

The baton between chats, and Claude's working list. Short and current: each chat rewrites *Last session* and keeps
the lists true. History is in git, not here.

## Last session — 2026-11-24
- **Did:**
  - **Principles 5.0.0:** five docs with one owner each: NOTES (Aaron's, timestamped), SUMMARY, ARCHITECTURE,
    HANDOFF, ROADMAP; `docs/design/` for live contracts only. VISION, GUIDE, STATE, TODO and plan docs are gone.
    New templates; 03–07, PROMPTS and the CHANGELOG are updated.
  - **Catalyzer's docs converted:** SUMMARY, ARCHITECTURE (with GUIDE merged in), HANDOFF, ROADMAP;
    LINK / MANIFEST / PLUGINS moved to `docs/design/`. Aaron's three test notes kept in the new format.
  - **Catalyzer 1.1.0, notes:** stamped notes (`yyyy-MM-dd HH:mm`) in one `## Notes` section; WIPE + SYNC (confirm →
    stops if newer notes just arrived from GitHub → backup → fresh slate → sync); Remove in the right-click menu;
    BACKUPS button; `NotesCache` (every new version, deduped, per-project cap from Settings: OFF / 25 / 100 / 500 MB,
    default 100; purged when a project leaves the board, kept for hidden projects and offline drives). Older
    NOTES files still read and edit. NEW PROJECT scaffolds the 5.0.0 docs and the shorter project instructions.
  - **Cleaned:** the root `.catalyzer-src*.tgz` snapshots, `.scratch/`, `publish/` and `_devSpace/` contents (28 MB).
- **Verified:** the app and the Link / Helpers plugins build clean (Linux build with Windows targeting; NexusPlugin
  not built, it needs the Nexus repo). The notes logic passed a throwaway test: stamps, newest-first, remove, legacy
  files, wipe, cache dedupe / cap / newest kept / OFF / purge. **Not yet run on Windows or looked at.**
- **Ready to test:** after the fresh install, open NOTES on the Catalyzer card: add a note (stamp shows), right-click
  → Remove, BACKUPS, then WIPE + SYNC on the three test notes. Settings → General → Notes backups.

## Next
1. **Fresh install:** Apps & Features (or `tools\uninstall.ps1`), then `tools\install.ps1` from the repo (the card's
   Install button goes away with the uninstall). Then the welcome, and LINK on the cards that need it.
2. Snapshot the notes drawer and Settings (`tools/snap.ps1`) and look at them: the wrapped button row is new.
3. ROADMAP → *Now* (chunk A): the principles badge, then MAWP's docs to 5.0.0 (the agreed plan: 19 docs → the five
   + `design/CONTRACT.md` + `design/RUNBOOK.md`).
4. Helpers: add real `helpers` to the projects that have them (ask Aaron which).

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
