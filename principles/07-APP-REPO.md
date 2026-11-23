# 07 — What every app repo contains

Every app is built **with Catalyzer in mind**:
- it's registered on GitHub;
- it describes itself to Catalyzer (`catalyzer.json`);
- it keeps its notes and docs in the repo, where Aaron, Catalyzer and every chat can read them.

The repo is the single source of truth. Chat memory isn't.

```
<App>/
  .nexus-principles        the principles version it follows, e.g. 2.0.0 (one line)
  CLAUDE.md                how to work here: rules, build / run, what to read (template)
  README.md                what it is, how to install and run, where its data lives
  catalyzer.json           its card in Catalyzer (name, description, accent, glyph, buttons, helpers)
  <App>.sln / .slnx        builds everything with one command (or the platform's equivalent)
  docs/
    NOTES.md               the shared notepad: Aaron's list, ideas, bugs, done (Aaron + Claude + Catalyzer write)
    VISION.md              where it's going: purpose, scope now, scope later, out of scope
    ARCHITECTURE.md        the top-level map: components, libraries, feature sets, how data flows
    GUIDE.md               the guide for humans: how it works, the main classes and key methods, with file pointers
    HANDOFF.md             the baton between chats
    STATE.md               what exists · Removed / decided against · known gaps
  tools/                   build / install / snap scripts that a linked chat runs through the Catalyzer link
  .catalyzer/              Catalyzer's link mailbox (it git-ignores itself; never commit it)
  src/ ...                 the code
```

Starter files for all of these are in [`templates/`](templates/). Copy them, then fill them in. Don't leave
placeholders in a pushed repo.

## The docs, and who keeps them true
| File | Holds | Updated when |
|---|---|---|
| **NOTES.md** | everything to do or remember: *Inbox*, *Next up*, *Ideas*, *Bugs*, *Done* | any time. "Add to the list / note this" → *Inbox* |
| **VISION.md** | why it exists, what it's for now, where it could go, what it won't do | the scope changes, or Aaron dreams out loud |
| **ARCHITECTURE.md** | one screen: the parts, the libraries, the feature sets, the data flow, external links (APIs, other apps) | a part, library or integration is added or removed |
| **GUIDE.md** | the "for dummies" tour: how a feature works end to end, main classes and what they do, key methods, `file:line` pointers; not a full reference | a main class or flow changes |
| **HANDOFF.md** | what the last chat did, what's ready to test, what's next | every chat, before it ends |
| **STATE.md** | what's live, *Removed / decided against*, known gaps | a feature ships or is dropped |

**Short and current beats long and stale.** Each doc stays readable in a few minutes, and a chat that changes the
code updates the doc that describes it in the same commit.

## NOTES.md rules
- **One file for the whole list.** It replaces BACKLOG.md (principles 2.0.0).
- **Sections:**
  - *Inbox*: raw, undated notes, newest first;
  - *Next up*;
  - *Ideas*;
  - *Bugs*;
  - *Done* (the last ~20, with dates).
- **Items are `- [ ]` lines.** Bugs use the format in [04](04-WORKFLOW.md). Who added an item can be marked
  `(Aaron)` / `(Claude)`.
- **Chats triage the Inbox:** move items into the right section. Never delete Aaron's words. Rewording is fine;
  dropping is not.
- **Catalyzer reads and writes the file** from the card (its notes panel). Keep the headings exactly as above, so
  every tool finds them.

## CLAUDE.md template
See [`templates/CLAUDE.md`](templates/CLAUDE.md). It always starts with the principles header, then:
- *What it is* (one paragraph, pointing to VISION);
- *Shape* (pointing to ARCHITECTURE);
- *Build / install / run* (through the Catalyzer link);
- *App-specific rules*;
- *Exceptions* (**EXCEPTION** + reason).

## GitHub and Catalyzer
- **GitHub:** each app has its own repo, `AaronPierdon/<App>`, created before the first handover, with `main` pushed.
  Large binaries (media, builds) stay off GitHub: they live on the file server, and the repo says where.
- **catalyzer.json:** see `Catalyzer/docs/MANIFEST.md`.
  - At minimum: `name`, `description`, `accent`.
  - Add buttons for build, install and run.
  - Add `helpers` for anything that should run in the background.
