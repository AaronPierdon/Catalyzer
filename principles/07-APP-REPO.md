# 07 — What every app repo contains

Every app is built **with Catalyzer in mind**:
- it's registered on GitHub;
- it describes itself to Catalyzer (`catalyzer.json`);
- it keeps its notes and docs in the repo, where Aaron, Catalyzer and every chat can read them.

The repo is the single source of truth. Chat memory isn't.

```
<App>/
  .catalyzer-principles        the principles version it follows, e.g. 2.0.0 (one line)
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

## Wiring an app to Catalyzer (do this once per app)
A chat that's been handed the Catalyzer repo and told "let's build something" follows these steps:
1. **GitHub repo** `AaronPierdon/<App>`, and the folder `C:\Users\Aaron\source\repos\<App>`. The folder is
   enough for Catalyzer to show a card (it scans `repos`).
2. **`catalyzer.json`** at the root, from [`templates/catalyzer.json`](templates/catalyzer.json): name,
   description, accent, and buttons for build and install.
3. **`tools\` scripts** from [`templates/tools/`](templates/tools/):
   - `build.ps1` and `install.ps1` (plus `snap.ps1` for UI apps);
   - a `.cmd` beside each, so Aaron can double-click it;
   - every script prints a short result and exits non-zero on failure.
4. **The docs** from `templates/`. CLAUDE.md's *Build / install / run* names the tools scripts and the link.
5. **Ask Aaron to click LINK** on the new card. Check it with `bash .catalyzer/link/run.sh --status`, then run
   `bash .catalyzer/link/run.sh -f tools/build.ps1`. That's the proof the wiring works.
6. **Push.** The card shows the repo, its git state, its NOTES and its link.

**Using the link:** `.catalyzer/link/README.md` (Catalyzer writes it into the repo the first time LINK is on) and
04 → *Running things on the PC*. In short:
- `run.sh '<PowerShell>'` or `run.sh -f <script>`;
- it runs hidden, in the repo root, at below-normal priority, inside Catalyzer's kill-on-close job;
- every command shows in the card's feed.

**Retiring a `_tools` queue helper** (MAWP, Nexus, apps from before the link):
- The scripts stay. `bash .catalyzer/link/run.sh -f _tools/scripts/<x>.ps1` runs any of them directly, with no
  queue, no list of allowed actions, and no window.
- Move the everyday ones (build, install, snap, test) into `tools\` as the app is next touched. Old double-click
  `.cmd` files can stay.
- Stop starting the helper window once the link has worked for that app. Delete `queue\`, `out\` and
  `run-action.sh` when nothing uses them. Then say so in HANDOFF and STATE.

## GitHub and Catalyzer
- **GitHub:** each app has its own repo, `AaronPierdon/<App>`, created before the first handover, with `main` pushed.
  Large binaries (media, builds) stay off GitHub: they live on the file server, and the repo says where.
- **catalyzer.json:** see `Catalyzer/docs/MANIFEST.md`.
  - At minimum: `name`, `description`, `accent`.
  - Add buttons for build, install and run.
  - Add `helpers` for anything that should run in the background.
