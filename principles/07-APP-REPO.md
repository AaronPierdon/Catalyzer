# 07 — What every app repo contains

Every app is built **with Catalyzer in mind**:
- it's registered on GitHub;
- it describes itself to Catalyzer (`catalyzer.json`);
- it keeps its notes and docs in the repo, where Aaron, Catalyzer and every chat can read them.

The repo is the single source of truth. Chat memory isn't.

## Spartan repos
A repo holds what's needed to build, run, understand and ship the app: nothing more. Each thing in it is code, a
doc someone reads, or a tool that builds or installs it. Here's the whole picture:

```
<App>/
  README.md                what it is, how to install and run, where its data lives (GitHub's front page)
  CLAUDE.md                how to work here: rules, build / run, what to read (template)
  catalyzer.json           its card in Catalyzer (name, description, accent, glyph, buttons, helpers)
  .catalyzer-principles    the principles version it follows (one line)
  <App>.slnx / .sln        ONE solution that builds everything (or the platform's equivalent, e.g. Gradle)
  .gitignore .gitattributes Directory.Build.props (optional)
  src/                     the code: one folder per project (.csproj), with its own Assets/ (images, icons, fonts)
  plugins/                 only for apps with a plugin SDK: one folder per bundled plugin
  tests/                   test projects (inside the same solution)
  tools/                   build / install / snap scripts + a double-click .cmd beside each
  docs/                    NOTES, SUMMARY, ARCHITECTURE, HANDOFF, ROADMAP (+ design/ for contracts, see below)
  _devSpace/               scratch: git-ignored, never committed, cleaned at milestones
  .catalyzer/              Catalyzer's link mailbox (it git-ignores itself; never commit it)
```

**The rules:**
- **Nothing else at the root.** No `.cmd` files, scripts, plans, runbooks or handovers up there. They go in `tools/` or
  `docs/`.
- **One solution.** Test projects live inside it. A second solution only for a real need (say a fast subset), and
  CLAUDE.md says why.
- **A project (`.csproj`) is one output** (an exe or a dll). Split code into projects for reuse or clear boundaries
  (a `Core` used by the app, its agents and its tests), not for tidiness.
- **Extra docs only for live contracts.** Beyond the five standard docs, a doc earns a file in `docs/design/` only
  when other code, repos or people depend on it staying exact: a protocol (`CONTRACT.md`), a runbook for live data,
  a file format or plugin API. Plans never get their own file: they go in ROADMAP, and once built they shrink into
  ARCHITECTURE. A finished plan is deleted, not archived: git remembers it.
- **Retired code and old prototypes are deleted, not kept in `docs/history/`.** Git keeps them, and ROADMAP →
  *Decided against* records the decision.
- **Prototypes you care about** live on a git branch (`proto/<name>`), so they're backed up. A prototype that wins
  moves into `src/`.
- **Big files** (media, test images, datasets, builds) stay out of git. They go on the file server or in
  `_devSpace/`, and the README says where.
- **`tools/` holds the scripts people run**, and a script earns its place by being run again. One-off diagnostic
  scripts (a screenshot of this, a dump of that) are written in `_devSpace/` and stay there.

## `_devSpace` — the scratch pad
- **What goes there:** experiments, throwaway scripts, test data, screenshots, logs, a spike before it's real code.
  A chat writes its one-offs here, not in `tools/` or `src/`.
- **Git-ignored:** `.gitignore` has `_devSpace/`. That means it is **not backed up**. Anything worth keeping
  graduates into `src/`, `tools/`, `docs/` or a `proto/` branch.
- **Cleaned at milestones.** When a feature ships (HANDOFF updated), the chat lists what's in `_devSpace` and
  what it plans to delete. Aaron says yes before anything goes. Catalyzer can show its size on the card and suggest
  a cleanup of files untouched since well before the last commit: it always shows the list first and never deletes
  silently.

## App families (one product across several repos)
Some apps are a family: one product across several repos that talk to each other. MAWP is one: the PC app, the
Android app and the TV app, sharing a backend and a device-link protocol.
- **One contract doc** describes what the members share: the protocol, messages, ports, discovery, the shared data
  shapes. It lives in the lead repo as `docs/design/CONTRACT.md` (for MAWP: the PC repo), and the others link to it rather
  than keeping copies.
- **A chat works in one member and reads the rest only when it needs to.** When the work touches the contract, it:
  - reads CONTRACT and the other members' HANDOFF;
  - updates CONTRACT;
  - adds the follow-up to each affected member's HANDOFF → *Next*.

  It never loads the other codebases unless asked. That keeps family-aware work cheap.
- **One Claude project and one Morning brief for the family**, with every member's folder attached. The brief reads
  every member's HANDOFF and flags contract drift.
- **Merging the repos into one** (`pc/`, `android/`, `tv/`) is an option when most changes hit every member at once.
  It costs combining the histories and mixing toolchains in one tree, so decide it at a big refactor, not mid-feature.

Starter files for all of these (`.gitignore` included) are in [`templates/`](templates/). Copy them, then fill them in. Don't leave
placeholders in a pushed repo.

## The docs, and who keeps them true
Five standard docs, the same in every repo. Catalyzer's NEW PROJECT makes them from [`templates/`](templates/).

| File | Holds | Owner | Updated when |
|---|---|---|---|
| **NOTES.md** | Aaron's raw ideas and notes, each timestamped | **Aaron only** | any time; wiped by Aaron for a fresh slate |
| **SUMMARY.md** | what the app does for a user, every feature it offers: an informal functional spec | Claude | a feature ships, changes or is removed |
| **ARCHITECTURE.md** | the inventory and the reasons: stack, libraries, parts, main classes, flows, data, decisions and why each was chosen | Claude | a part, library, class, flow or decision changes |
| **HANDOFF.md** | *Last session*, *Next* (Claude's working list), *Bugs*, *Known gaps* | Claude | every chat, before it ends |
| **ROADMAP.md** | agreed ideas with their decisions, in chunks: *Now*, *Next*, *Later*, *Decided against* | both | an idea is agreed, a chunk starts or ships, an idea is dropped |

Plus `README.md` (GitHub's front page) and `CLAUDE.md` (how to work here) at the root, and `docs/design/` for live
contracts only.

**Short and current beats long and stale.** Each doc stays readable in a few minutes, and a chat that changes the
code updates the doc that describes it in the same commit. Nothing is kept twice: what's built is in SUMMARY and
ARCHITECTURE, what's planned is in ROADMAP, what's in hand is in HANDOFF.

## NOTES.md rules
- **Aaron's alone.** He writes it by hand or from Catalyzer's notes panel. A chat never edits, sorts, ticks or
  deletes anything in it.
- **Every note is timestamped:** `- 2026-11-24 14:57 — the note`. Catalyzer's panel does this itself; anything else
  that adds a note for Aaron (a phone chat told "note this for <App>") does too.
- **Pulling notes in:** when Aaron asks, a chat reads NOTES, talks through what's unclear, and carries each note
  into ROADMAP (an agreed idea), HANDOFF → *Next* or *Bugs* (work in hand), or ROADMAP → *Decided against*. It
  tells Aaron where each one went. Then Aaron wipes the file (Catalyzer: **WIPE + SYNC**).
- **Backups:** Catalyzer keeps every version it sees in `%LOCALAPPDATA%\Catalyzer\notes-cache\<project>\`
  (one timestamped file per version, up to a size cap set in Settings, 100 MB by default), and deletes a project's
  backups when the project leaves the board. "Find my notes between X and Y" means a chat reads that folder through
  the link. Git history has every pushed version too.
- **Layout:** the intro, then `## Notes`, newest first. Catalyzer still reads older files with other `##`
  sections.

## ROADMAP.md rules
- **Chunks:** objectives that ship together sit together under one `###` heading. Work goes one chunk at a time:
  *Now* holds one chunk, *Next* the ones that are ready, *Later* the ones still being decided.
- **Objectives** have an ID (`R1`, `R2`…, never reused), a one-line goal, *Decided*, *Open* and a size.
- **Decided against is kept forever.** Every dropped idea stays there with its reason and date, so it isn't
  proposed again. Nothing in it comes back unless Aaron asks (05).
- When a chunk ships, its objectives are deleted from ROADMAP and described in SUMMARY and ARCHITECTURE.

## CLAUDE.md
See [`templates/CLAUDE.md`](templates/CLAUDE.md). It's the project's instructions, and the Claude project's own
instructions only point to it (PROMPTS → *Project instructions*). It always starts with the principles header, then:
- *New chat* steps;
- *What it is* (one paragraph, pointing to SUMMARY);
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
  `run-action.sh` when nothing uses them. Then say so in HANDOFF.

## GitHub and Catalyzer
- **GitHub:** each app has its own repo, `AaronPierdon/<App>`, created before the first handover, with `main` pushed.
  Large binaries (media, builds) stay off GitHub: they live on the file server, and the repo says where.
- **catalyzer.json:** see `Catalyzer/docs/design/MANIFEST.md`.
  - At minimum: `name`, `description`, `accent`.
  - Add buttons for build, install and run.
  - Add `helpers` for anything that should run in the background.
