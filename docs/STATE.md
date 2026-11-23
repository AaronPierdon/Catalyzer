# STATE

## What's live (0.1, 2026-11-22)
Board of project cards · settings drawer (General / Project folders / Theme / Plugins / About) · plugin SDK v1 ·
NexusPlugin (live Nexus theme + Open in Nexus Explorer) · 4 built-in themes · run at Windows start · single instance ·
`--snapshot` · `tools/install.ps1`.

## On branch `pc/2026-11-22-helpers` (not merged yet)
Notes (NOTES chip + drawer over docs/NOTES.md, git sync) · Link (hidden Windows terminal per repo for chats) · Helpers (hidden long-running scripts per card) · SDK API 2
(badges, background work, feeds) · close prompt + tray · folder watcher · feed drawer · `tools/snap.ps1`.

## Removed / decided against — don't bring back
- A shared helper queue with MAWP's helper (`_Helpers` attempt, 2026-11-22): scrapped by Aaron.

## Known gaps
- Helpers: built, waiting for Aaron's live look (HANDOFF). A helper's grandchildren started in its first
  milliseconds could escape the job (assigned right after start); in practice powershell takes far longer to start.
- The Nexus helper must not be a Catalyzer helper: installing Catalyzer would kill it mid-install. Through the link,
  install Catalyzer with `run.sh -s` (survive).
- The link runs whatever it's sent, as Aaron: the chat's rules are the guard, and the feed is the record.
- Plugin on/off and import need a restart (by design for now: assemblies can't unload from the default context).
