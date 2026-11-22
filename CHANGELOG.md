# Changelog

Newest first. Each entry says what changed and **what an app must do**.

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
