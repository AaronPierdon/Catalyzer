# Changelog

Newest first. Each entry says what changed and **what an app must do**.

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
