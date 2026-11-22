# 04 — Workflow

## Chats and machines
- **Every chat starts fresh.** Everything it needs lives in the repo: CLAUDE.md, docs, git history. Nothing lives in
  chat memory.
- **One project per app** (in Claude). The project instructions point at the app's CLAUDE.md and at these principles.
- **Lab and truth:**

  | Chat | Linked to | Job |
  |---|---|---|
  | **VM / lab chat** | the NexusDev VM | builds and **tries to break things** (stress battery). Never judges smoothness, GPU or font sharpness |
  | **PC / truth chat** | Aaron's PC (REACTOR, 2560×1440 at 100 %) | deploys, judges look and performance on the real screen, fixes, merges to `main` |

  Small apps may use a single PC chat.
- When a thread gets long, write the handover and start a new one. Don't let context run out mid-change.

## Starting a chat
1. Read the app's `CLAUDE.md` → these principles (at least the CHANGELOG since the app's `.nexus-principles`) →
   `docs/HANDOFF.md` / `STATE.md` / `BACKLOG.md` → `git log --oneline -30`.
2. Study how the part you'll touch already works, and reuse its mechanisms. Don't build a parallel one.
3. Tell Aaron in a few lines what you understood and what you'll do, then start.

## Branches and commits
- `main` is what's installed and trusted.
- Work branches:
  - `vm/<YYYY-MM-DD>-<topic>` for the lab;
  - `pc/<YYYY-MM-DD>-<topic>` for the PC.
- Merged or abandoned branches are renamed `retire--<name>`.
- Commit messages say *what changed and why*, in plain words. They end with the attribution lines the session asks for.
- **Push before a chat ends.** Each app has its own GitHub repo under `AaronPierdon`.

## Handover files (every app repo)
- `docs/HANDOFF.md` — the baton. Covers:
  - what this chat did;
  - what's ready to test and how;
  - what failed;
  - what's next.

  Each chat rewrites its own section; git keeps history.
- `docs/STATE.md` — what exists, *Removed / decided against*, known gaps.
- `docs/BACKLOG.md` — Aaron's list. **"Add to the list: …"** means append here.
- A thread being retired writes a **handover doc** plus a **prompt for the next thread** (see PROMPTS.md).

## Working with Aaron
- He's often away or on his phone. Work unattended:
  - keep the task list current;
  - send progress and screenshots;
  - ask only when a wrong guess is expensive or irreversible.
- **Look matters as much as function.** Render and look before calling UI done.
- **Warn before every install** that restarts something he's using.
- **Computer control** only with permission, in short bursts, then release it.

## Testing: triple-test everything
1. **The normal path.**
2. **Edge cases:**
   - empty, huge, tiny;
   - long names, offline, slow disk;
   - DPI, second monitor.
3. **Conflicts:**
   - other popups open;
   - fast repeated clicks;
   - input during animations;
   - restarts, sleep / resume.

The lab's stress battery covers:
- every control, with other windows open;
- overlapping popups;
- spam clicks;
- resize extremes;
- minimize-all;
- system events;
- an hour-long run for leaks;
- recovery (kill it: does the user get their stuff back?).

Bug format in BACKLOG: `- [ ] <app>: <what happens> — steps: … — expected: … — VM/PC — added <date>`.

## Shipping
Build → snapshot and look → install (warned) → check on the real screen (sharpness, motion, CPU/GPU) → update
HANDOFF / STATE / BACKLOG → merge → push.
