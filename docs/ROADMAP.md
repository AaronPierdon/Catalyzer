# ROADMAP — Catalyzer

Where Catalyzer is going: ideas Aaron and Claude agreed are good, with the decisions made so far. Raw ideas start in
Aaron's NOTES and land here once agreed. Objectives are grouped into chunks; each chunk ships on its own. When a
chunk ships, its objectives leave this file (SUMMARY and ARCHITECTURE describe what's built). IDs are never reused.

## Now
### A — Every app on principles 5.0.0
- **R1 Principles badge**: each card shows its repo's `.catalyzer-principles` (or old `.nexus-principles`) against
  the current principles VERSION; behind = warn, with a tip that names the Update prompt.
  - Decided: a badge through the existing badge API; reads one file, no git.
  - Open: whether clicking it copies the Update prompt.
  - Size: S
- **R2 Convert the apps**: MAWP first (its doc plan is agreed: 19 docs → the five + `design/CONTRACT.md` and
  `design/RUNBOOK.md`), then Nexus and the rest, each in its own chat with "update to the latest principles".
  - Decided: the 5.0.0 and 5.1.0 changelogs are the recipe (5.1: the install contract and `<Version>`, which light
    the UPDATE chip); Aaron OKs deletions per repo. No app has `tools\install.ps1` yet, so no card shows UPDATE
    until its chat does this.
  - Size: M

## Next
### B — Notes from anywhere
- **R3 Notes from the phone**: "note this for <App>" in any chat lands a timestamped line in that repo's NOTES;
  Catalyzer shows it after the next fetch or SYNC.
  - Decided: the chat writes NOTES on Aaron's word only (PROMPTS → *Note this*); pull-on-open already brings it in.
  - Open: an "ideas" repo for notes with no app yet.
  - Size: S

### C — Card tools
- **R4 Claude Code button**: a card button that opens Claude Code in that repo (and its recent sessions), as an
  alternative to the chat + link.
  - Size: S
- **R6 Drag to reorder cards** (today: move to front, and `Order` in settings).
  - Size: S

## Later
### D — The dev kit identity
- **R7 Devkit**: a home for the principles plus reusable assets (themes, controls, scripts, templates), with
  Catalyzer as its companion tool. Possibly a rename (e.g. "Nexus Catalyzer", or a kit name) so the repo matches how
  Aaron thinks of it.
  - Decided: naming later is cheap; GitHub redirects old links.
  - Open: one repo (Catalyzer + principles + assets, as today) or two (a devkit repo, Catalyzer as an app in it);
    the name; whether 08-ASSETS's shared assets move in.
  - Size: M

### E — More than one machine, more than one repo
- **R8 Cross-device Catalyzer**: the same board on the laptop (maybe the phone later), GitHub as the backend, each
  PC running its own link.
  - Open: per-machine settings vs shared; how a card shows "not cloned here".
  - Size: L
- **R9 Project groups**: work on several repos as one (MAWP PC + Android + TV): a group card, linked together, the
  family's CONTRACT one click away.
  - Size: M
- **R10 Nexus integration**: Catalyzer as a Nexus developer utility, always standalone so Nexus reinstalls never
  touch it.
  - Size: M

## Decided against — don't bring back
- A shared helper queue with MAWP's helper (`_Helpers`) — scrapped by Aaron; the link replaced it — 2026-11-22
- Automating the Claude desktop app through `claude://` links — they only prefill prompts and always ask before
  using a folder; scheduled tasks (Morning brief) do the job — 2026-11-23
- Being a code editor or a git client — Catalyzer launches the real ones and shows state — 2026-11-22
- A custom backend database or file sync — GitHub and the file server are the backends — 2026-11-22
- A hard dependency on Nexus — Nexus only arrives through a plugin — 2026-11-22
- Notes as a shared to-do list (Inbox / Next up / Ideas / Bugs / Done, written by chats too) — NOTES is Aaron's
  alone; Claude's list is HANDOFF, agreed ideas are ROADMAP (principles 5.0.0) — 2026-11-24
- A separate TODO.md — it duplicates HANDOFF + ROADMAP + NOTES — 2026-11-24
- Conflict resolution, rebase, stash, branch management, force-push or history editing inside Catalyzer — those
  need judgment; Visual Studio's Git window does them — 2026-11-25
- VS Code as the hand-off for git — Aaron works in Visual Studio Community — 2026-11-25
