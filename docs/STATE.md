# STATE

## What's live (1.0, 2026-11-23)
- **The board:** a card per repo under the projects folder, showing git state, 21 days of activity and buttons.
  New repos appear by themselves (folder watcher). Search, rescan, hide, reorder.
- **First-run welcome:** the projects folder, and GitHub through the PC's git sign-in (nothing stored).
- **GitHub panel:** every repo on the account against this PC. CLONE / PUSH / PUBLISH; nothing is ever deleted.
- **NEW PROJECT:** folder from the principles' templates, first commit, GitHub repo, push, then the Claude handoff
  (instructions + kickoff with COPY).
- **Notes:**
  - a NOTES chip and drawer over `docs/NOTES.md`;
  - auto-sync of NOTES.md only (a minute after the last edit, generated messages);
  - a SYNC ↑/↓ chip;
  - pull on open when safe.
- **Link:** a hidden Windows terminal per repo for chats (mailbox `.catalyzer/link`, `run.sh`, feed, cancel,
  survive). Below-normal priority. Relinks at start (Settings can turn that off). Refuses requests git tracks.
- **Helpers:** a project's long-running scripts, hidden, in a kill-on-close job. Auto-start only after a first
  start by hand.
- **`_devSpace` chip:** size; a cleanup that lists files first and moves them to the Recycle Bin.
- **Window behaviour:** a close prompt with work running (ask / stop / tray), a tray menu, single instance, run at
  Windows start.
- **Themes:** 4 built in, plus live Nexus (NexusPlugin); `--snapshot`.
- **Plugin SDK API 3:** badges, background work, feeds, plugin settings.
- **Install:** `tools/install.ps1` (Start menu, Apps & Features entry, principles bundled); `uninstall.ps1`.

## Removed / decided against — don't bring back
- A shared helper queue with MAWP's helper (`_Helpers` attempt, 2026-11-22): scrapped by Aaron.

## Known gaps
- A helper's (or link request's) grandchildren started in its first milliseconds could escape the job (it's
  assigned right after start); in practice powershell takes far longer to start.
- The GitHub panel lists repos the PC's git sign-in can see. A second account (StackView is under `Aetrocles`)
  shows up only if that sign-in has access.
- The Nexus helper must not be a Catalyzer helper: installing Catalyzer would kill it mid-install. Through the link,
  install Catalyzer with `run.sh -s` (survive).
- The link runs whatever it's sent, as Aaron: the chat's rules are the guard, and the feed is the record.
- Plugin on/off and import need a restart (by design for now: assemblies can't unload from the default context).
