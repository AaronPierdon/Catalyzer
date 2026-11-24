# CLAUDE.md — <App>

Follows **Catalyzer Development Principles** (`repos\Catalyzer\principles`,
github.com/AaronPierdon/Catalyzer/tree/main/principles); this repo's version is in `.catalyzer-principles`. Read them
before working (at least the CHANGELOG since that version). Rules below only add to them; exceptions are marked
**EXCEPTION** with the reason. This file is the project's instructions: the Claude project's own instructions only
point here, so they can't drift.

New chat:
1. Check the Catalyzer link.
2. Read this file, then `docs/HANDOFF.md`, `docs/ROADMAP.md` (*Now*) and `git log --oneline -20`.
3. Skim `docs/SUMMARY.md` and `docs/ARCHITECTURE.md` for the part you'll touch.
4. `docs/NOTES.md` is Aaron's. Read it only when he asks you to pull his notes in; never edit it.

## What it is
<one paragraph. The full feature list is in docs/SUMMARY.md>

## Shape
<the projects / folders and what each owns. The inventory and the reasons are in docs/ARCHITECTURE.md>

## Build / install / run
Through the Catalyzer link. Check it with `bash .catalyzer/link/run.sh --status`; if it's OFF, ask Aaron to click
LINK on the card.
- Build: `bash .catalyzer/link/run.sh -f tools/build.ps1`
- Snapshot: `bash .catalyzer/link/run.sh -t 600 -f tools/snap.ps1`
- Install: `bash .catalyzer/link/run.sh -f tools/install.ps1`. Warn Aaron first if it restarts something he uses.
- Data: `%APPDATA%\<App>\settings.json`, log `%LOCALAPPDATA%\<App>\log.txt`.

## Scratch
One-off scripts, dumps, screenshots and experiments go in `_devSpace/` (git-ignored), never in `tools/`, `src/` or
the root. List it for Aaron at milestones before cleaning.

## App-specific rules
<anything the principles don't cover>

## Exceptions
<none, or: **EXCEPTION** — the rule, why, when to revisit>
