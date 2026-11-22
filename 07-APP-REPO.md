# 07 — What every app repo contains

```
<App>/
  .nexus-principles        the principles version this app conforms to, e.g. 1.0.0 (one line)
  CLAUDE.md                rules for working here (template below)
  README.md                what it is, how to run / install, where data lives
  catalyzer.json           how the app shows in Catalyzer (name, description, accent, glyph, its buttons)
  <App>.sln / .slnx        builds everything with one command
  docs/
    HANDOFF.md             the baton between chats
    STATE.md               what exists · Removed / decided against · known gaps
    BACKLOG.md             Aaron's list + bugs
  tools/                   build / install / snap scripts a linked chat runs (bash .catalyzer/link/run.sh -f tools/x.ps1)
  .catalyzer/              Catalyzer's link mailbox (it git-ignores itself; never commit it)
  src/ ...                 the code
```

## CLAUDE.md template
```markdown
# CLAUDE.md — <App>

Follows **Nexus Development Principles** (github.com/AaronPierdon/Nexus-Development-Principles); this repo's
version is in `.nexus-principles`. Read them before working (at least the CHANGELOG since that version).
Rules below only add to them; exceptions are marked **EXCEPTION** with the reason.

## What it is
<one paragraph>

## Shape
<projects / folders and what each owns; the public API / SDK if any>

## Build / install / run
Through the Catalyzer link (`bash .catalyzer/link/run.sh --status`; OFF → ask Aaron to click LINK on the card):
<the tools\ scripts to run, e.g. `bash .catalyzer/link/run.sh -f tools/build.ps1`; data locations; snapshot>

## App-specific rules
<anything the principles don't cover>
```

## catalyzer.json
See `Catalyzer/docs/MANIFEST.md`. At minimum: `name`, `description`, `accent`. Add buttons for build, install and
run.
