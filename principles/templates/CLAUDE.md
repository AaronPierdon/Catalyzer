# CLAUDE.md — <App>

Follows **Catalyzer Development Principles** (`repos\Catalyzer\principles`,
github.com/AaronPierdon/Catalyzer/tree/main/principles); this repo's version is in `.catalyzer-principles`. Read them
before working (at least the CHANGELOG since that version). Rules below only add to them; exceptions are marked
**EXCEPTION** with the reason.

New chat:
1. Check the Catalyzer link.
2. Read this file, the principles, then `docs/HANDOFF.md`, `docs/NOTES.md`, `docs/STATE.md` and
   `git log --oneline -20`.
3. Skim `docs/VISION.md` and `docs/ARCHITECTURE.md`.

## What it is
<one paragraph. The why and the where-next are in docs/VISION.md>

## Shape
<the projects / folders and what each owns; any public API or SDK. The full map is in docs/ARCHITECTURE.md>

## Build / install / run
Through the Catalyzer link. Check it with `bash .catalyzer/link/run.sh --status`; if it's OFF, ask Aaron to click
LINK on the card.
- Build: `bash .catalyzer/link/run.sh -f tools/build.ps1`
- Snapshot: `bash .catalyzer/link/run.sh -t 600 -f tools/snap.ps1`
- Install: `bash .catalyzer/link/run.sh -f tools/install.ps1`. Warn Aaron first if it restarts something he uses.
- Data: `%APPDATA%\<App>\settings.json`, log `%LOCALAPPDATA%\<App>\log.txt`.

## App-specific rules
<anything the principles don't cover>

## Exceptions
<none, or: **EXCEPTION** — the rule, why, when to revisit>
