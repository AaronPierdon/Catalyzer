# Changelog

Newest first. Each entry says what changed and **what an app must do**.

## 4.0.0 — 2026-11-23
**Spartan repos** (07). The repo layout is now a rule, not a suggestion:
- the root holds only README, CLAUDE, `catalyzer.json`, the marker, the solution and the git files;
- everything else goes in `src/`, `tests/`, `tools/`, `docs/` or `_devSpace/`;
- one solution;
- topic docs only for things that exist;
- retired code and plans are deleted (git remembers), not kept in `docs/history/`;
- prototypes live on `proto/` branches;
- big files stay out of git.

**New: `_devSpace/`**, the git-ignored scratch pad for experiments, one-off scripts and test data. It's cleaned at
milestones, only with Aaron's yes.

**New: app families** (07): one product across several repos (MAWP PC / Android / TV). One `docs/CONTRACT.md` in the
lead repo; a chat reads the other members only when it touches the contract; one Claude project and one Morning
brief for the family.

**What an app must do** (in its next chat, as its own commit, with Aaron's go-ahead before deleting anything):
1. Add `_devSpace/` to `.gitignore`.
2. Move root clutter into `tools/` or `docs/`, keeping the `.cmd` files Aaron double-clicks working. The code goes
   into `src/` if it sits at the root.
3. Sort `docs/`: plans → VISION / NOTES; finished plans and `docs/history/` → delete (list them first).
4. Sort scripts: the ones people run again → `tools/`; one-offs → `_devSpace/`.
5. Big files out of git. Untracked code that matters → commit it, or put it on a `proto/` branch.
6. For a family member: create or point to the family's `CONTRACT.md`.
7. Bump `.catalyzer-principles` to `4.0.0`.

## 3.2.0 — 2026-11-23
**Wiring an app to Catalyzer** (07): the once-per-app recipe:
- a GitHub repo and folder;
- a `catalyzer.json`;
- `tools\` scripts, each with a double-click `.cmd`;
- the docs;
- LINK, then the first build through it, then a push.

Also in 07: how to retire an old `_tools` queue helper. **New templates:** `templates/catalyzer.json`,
`templates/tools/` (`build` and `install`, as `.ps1` + `.cmd`). Link requests now run at below-normal priority (add
`-n` for normal).

**What an app should do** (when next touched):
- add `tools\build.ps1` and `install.ps1` if it doesn't have them;
- run them through the link;
- retire its queue helper once the link has worked;
- bump `.catalyzer-principles` to `3.2.0`.

## 3.1.0 — 2026-11-23
**A standard *Project instructions* block** (PROMPTS) for each app's Claude project. It's a few lines that point
to the repo and these principles, and it says: read CLAUDE.md, check the link, follow the principles, push at the end.

**What an app should do:** replace its Claude project's instructions with the block, filled in. Bump
`.catalyzer-principles` to `3.1.0`.

## 3.0.0 — 2026-11-23
**Catalyzer Development Principles.** The rulebook moved into the hub it serves. It was *Nexus Development
Principles* in its own repo; it's now the `principles/` folder of the Catalyzer repo, with the full history kept.
- **New home:** `repos\Catalyzer\principles` on the PC, github.com/AaronPierdon/Catalyzer/tree/main/principles on GitHub. The old repo
  only points here. Don't change it.
- **New marker:** apps record their version in `.catalyzer-principles`; `.nexus-principles` is the old name.
- **The look is the Catalyzer look** (02 → *Where the look comes from*). It was born in Nexus, and the palette is
  still called Nexus. This page is the source of truth; Nexus.Kit is today's WPF implementation, and Nexus follows
  these principles like any other app.
- **Every chat's second repo is now Catalyzer**, which holds both the hub and these principles.

**What an app must do** (in its next chat):
1. Rename `.nexus-principles` to `.catalyzer-principles`, containing `3.0.0`.
2. CLAUDE.md header → the template's: *Follows Catalyzer Development Principles*, with the new path and link.
3. Point any other links to the old repo (README, project instructions in Claude, Kickoff text) at the new home.
4. If it's behind 2.0.0 or 2.1.0, do those entries first.

## 2.1.0 — 2026-11-23
**Projects start in Catalyzer and wake up briefed.** Each app has a Claude project per computer, made in that
computer's desktop app from the repo folder, so every chat starts linked. A scheduled **Morning brief** inside the
project opens a fresh chat each morning (or on demand from the phone with Run now) that reads only the handover
files and git, never the whole codebase, then waits. New apps start from Catalyzer's **Kickoff** prompt. (04 *Setting
up a project in Claude*, PROMPTS → *Kickoff*, *Morning brief*.)

**What an app should do** (when next touched):
- Make sure its Claude project was created from the repo folder on the computer where it's built.
- Add the Morning brief as a scheduled task in that project, with a lighter model.
- Start HANDOFF → *Next* with one first move.
- Bump `.nexus-principles` to `2.1.0`.

## 2.0.0 — 2026-11-22
The principles now cover **how apps are developed**, not only how they look. Every app is built with Catalyzer in
mind and explains itself in its repo.
- **New required docs** (07):
  - `docs/NOTES.md`: the shared notepad for Aaron, Claude and Catalyzer's notes panel, with sections *Inbox*,
    *Next up*, *Ideas*, *Bugs* and *Done*;
  - `docs/VISION.md`: purpose, scope now and later, what it won't do;
  - `docs/ARCHITECTURE.md`: a one-screen map of the parts, libraries, feature sets and data flow;
  - `docs/GUIDE.md`: the "for dummies" tour, with main classes, key methods and `file:line` pointers.
- **BACKLOG.md is merged into NOTES.md** and retired.
- **Starter files** are in `templates/`.
- **Every chat works with two repos**, the app and these principles. It runs a *sync check* at start (04).
- A change updates the doc that describes it in the same commit (04, 06).
- **New:** 08-ASSETS, a direction for reusable assets (nothing required yet).
- **New prompts:** *Note this* and *New app*.

**What an app must do:**
1. Move `docs/BACKLOG.md` into `docs/NOTES.md`, using the sections in 07, and delete BACKLOG.md. Keep every item.
2. Add VISION, ARCHITECTURE and GUIDE from `templates/`, filled in from the actual code. Leave no placeholders.
3. Update CLAUDE.md to the template: the new-chat reading order, and *Build / install / run* through the link.
4. Make sure it has a GitHub repo and a `catalyzer.json`.
5. Bump `.nexus-principles` to `2.0.0`.

## 1.1.0 — 2026-11-22
**The Catalyzer link** replaces per-repo `_tools` queue helpers. Aaron clicks LINK on a project's card, and that
repo gets a hidden Windows terminal that its chat drives with `bash .catalyzer/link/run.sh '<PowerShell>'`. There's
one terminal per project, no console window, and every command shows in Catalyzer's feed. A linked chat edits the
PC copy directly and runs git, builds and installs through the link. (04 *Running things on the PC*, 04 *Starting a
chat* step 0, 05 rule 10, 07.)

**What an app must do** (in its next chat):
- CLAUDE.md → *Build / install / run*: start from the link (template in 07).
- Put build / install / snap in the repo's own `tools\` scripts, so a linked chat runs them with
  `run.sh -f tools/<x>.ps1`.
- Keep an old `_tools` helper only as a fallback, and say so in CLAUDE.md.
- Bump `.nexus-principles` to `1.1.0`.

## 1.0.0 — 2026-11-22
First version. It distils Nexus's VISION, DESIGN_GUIDELINES, WORKFLOW and CLAUDE.md, plus what we learned building
Catalyzer, into rules for every app.

**What an app must do:** adopt the principles (PROMPTS → *Adopt*):
- add `.nexus-principles` containing `1.0.0`;
- add the CLAUDE.md header;
- add the docs set;
- add `catalyzer.json`.

**Already conforming:**
- Catalyzer 0.1;
- Nexus (docs live in its own repo; it is the source of these principles).
