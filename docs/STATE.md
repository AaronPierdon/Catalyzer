# STATE

## What's live (0.1, 2026-11-22)
Board of project cards · settings drawer (General / Project folders / Theme / Plugins / About) · plugin SDK v1 ·
NexusPlugin (live Nexus theme + Open in Nexus Explorer) · 4 built-in themes · run at Windows start · single instance ·
`--snapshot` · `tools/install.ps1`.

## Removed / decided against — don't bring back
- A shared helper queue with MAWP's helper (`_Helpers` attempt, 2026-11-22): scrapped by Aaron.

## Known gaps
- No GitHub repo yet (see HANDOFF → Next).
- Helpers (start project tools hidden) not built yet.
- Plugin on/off and import need a restart (by design for now: assemblies can't unload from the default context).
