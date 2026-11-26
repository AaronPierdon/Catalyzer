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

## Setting up a project in Claude (once per app, per computer)
The goal: Aaron opens his phone and a chat is already waiting, linked to the right computer, briefed and ready. No
remote desktop, no clicking around in the desktop app. **Every new app starts in Catalyzer**: it gives Aaron the setup
steps and the whole kickoff prompt at once (PROMPTS → *Kickoff*).
1. **GitHub repo** `AaronPierdon/<App>`, made from `templates/`.
2. **A Claude project per app, per computer:** `<App>` for the PC, plus `<App> – VM` if the app uses the lab.
   - Create it **in the Claude desktop app on that computer**, from the repo folder, so the folder link belongs to
     the project and every new chat in it starts linked.
   - A folder belongs to one computer, and folder links only work while that computer's desktop app is open. A
     chat started from the phone in a project with no folder has no file access.
   - Project instructions: PROMPTS → *Project instructions* (the repo, these principles, read CLAUDE.md first,
     check the link, push at the end).
3. **Morning brief:** a scheduled task inside that project (PROMPTS → *Morning brief*), with the repo folder chosen,
   so *Require this computer* is on.
   - Runs every morning, or on demand with **Run now** from the phone. Each run is a fresh, full chat in the project.
   - Use a lighter model for the brief. Switch up when the real work starts.
4. Keep that computer awake with the Claude desktop app open. That's all the brief needs.

**Saving usage:** the brief reads only CLAUDE.md, HANDOFF, ROADMAP → *Now* and the git log, never the
whole codebase. It stops after the summary and waits for Aaron. HANDOFF → *Next* starts with **one first move**, so
the brief has nothing to dig for. Code is read when the day's work touches it, and only the part it touches.

**Watching over remote desktop** (optional): set the client to scale, not resize (Windows App: *Fit session to
window*; mstsc: *Smart sizing*), so Claude's screen stays a fixed resolution at any window size. While Claude drives
the mouse, Aaron only watches.

## Running things on the PC: the Catalyzer link
Each app chat has its own repo folder linked. Catalyzer (always running, started with Windows) gives that repo a
hidden Windows terminal when Aaron clicks **LINK** on its card. There's no console window, nothing to start by
hand, and one terminal per project, so parallel chats never share one.
- **Check:** `bash .catalyzer/link/run.sh --status`. If it's OFF, ask Aaron to click LINK on the project's card.
- **Use:** `bash .catalyzer/link/run.sh '<PowerShell>'` runs in the repo root, prints the output and returns the
  exit code. The full how-to is in `.catalyzer/link/README.md`, which Catalyzer writes into the repo.
- **With the link on:**
  - edit the PC working copy directly;
  - run git, builds, snapshots and installs through the link (git there has Aaron's credentials);
  - no tarball sync.
- Every command shows in Catalyzer's feed. The link makes commands possible, not approved: these rules still
  decide what's allowed.
- Builds and installs live in the repo's own `tools\` scripts, so any chat can run them through the link. An app's
  old `_tools` queue helper stays only until the app adopts the link.

## Starting a chat
Every chat works with **two repos**: its app, and Catalyzer (the hub, with these principles in `principles/`).
0. Check the Catalyzer link (`bash .catalyzer/link/run.sh --status`). Say in your first lines whether it's on.
1. Read the app's `CLAUDE.md`, then these principles: the README and 05-RULES always, and the CHANGELOG since the
   app's `.catalyzer-principles`.
2. Read `docs/HANDOFF.md` and `docs/ROADMAP.md` (*Now*), then run `git log --oneline -30`. Skim `SUMMARY.md`, and
   read `ARCHITECTURE.md` for the part you'll touch. `NOTES.md` is Aaron's: read it when he asks.
3. **Scratch goes in `_devSpace/`.** One-off scripts, dumps, screenshots and experiments never land in `tools/`,
   `src/` or the root.
4. **Sync check:** is the app behind the principles VERSION? Are any of the 07 files missing? Does
   NOTES have notes Aaron hasn't asked about yet? Say so in one line, and offer the Update prompt rather than silently fixing it.
5. Study how the part you'll touch already works, and reuse its mechanisms. Don't build a parallel one.
6. Tell Aaron in a few lines what you understood and what you'll do, then start.

## Branches and commits
- `main` is what's installed and trusted.
- Work branches:
  - `vm/<YYYY-MM-DD>-<topic>` for the lab;
  - `pc/<YYYY-MM-DD>-<topic>` for the PC.
- Merged or abandoned branches are renamed `retire--<name>`.
- Commit messages say *what changed and why*, in plain words. They end with the attribution lines the session asks for.
- **Commit and push after every piece of work** (05 rule 11): locally and to GitHub, straight away, without
  asking. Each app has its own GitHub repo under `AaronPierdon`.

## Handover files (every app repo)
- `docs/HANDOFF.md` — the baton and Claude's working list: *Last session* (did, verified, ready to test),
  *Next*, *Bugs*, *Known gaps*. Each chat rewrites *Last session*; git keeps history.
- `docs/ROADMAP.md` — agreed ideas in chunks (*Now*, *Next*, *Later*) and *Decided against* (07).
- `docs/NOTES.md` — Aaron's own notes (07). A chat never edits it. **"Add to the list / do this: …"** goes to
  HANDOFF → *Next*; **"note this for <App>"** from a phone chat adds a timestamped line to NOTES on his behalf.
- `docs/SUMMARY.md`, `ARCHITECTURE.md` — the app explained (07). **A change that alters what they describe updates
  them in the same commit.**
- **Ideas that apply to every app** (a new rule, a preference, a shared asset) go into these principles, not one
  app's notes. Aaron's latest word becomes a rule here.
- A thread being retired writes a **handover doc** plus a **prompt for the next thread** (see PROMPTS.md).

## Working with Aaron
- He's often away or on his phone. Work unattended:
  - keep the task list current;
  - send progress and screenshots;
  - ask only when a wrong guess is expensive or irreversible.
- **Look matters as much as function.** Render and look before calling UI done.
- **Warn before every install** that restarts something he's using.
- **Computer control** only with permission, in short bursts, then release it.
- **The wrap-up: 4 or more features or fixes → a short list.** One line per item: what changed. Items worth
  checking get a `check:` (what to try, or what could break). Only those items; no paragraphs.
  ```
  - Visuals levels in Settings (ECO over RDP by default). check: Task Manager CPU, FULL vs ECO
  - Cards don't tick at rest
  - Ambience pauses when minimized / locked. check: disconnect RDP, come back: it moves again
  - Glow off below BALANCED
  ```

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

Bug format in HANDOFF → *Bugs*: `- [ ] <app>: <what happens> — steps: … — expected: … — VM/PC — added <date>`.

## Shipping
Build → snapshot and look → bump `<Version>` (07) → install (warned) → check on the real screen (sharpness, motion,
CPU/GPU) → update HANDOFF, SUMMARY, ARCHITECTURE and ROADMAP as needed → merge → push.

**Who installs:** with the link on, the chat installs itself (warning Aaron first if the app is running). Otherwise,
or when Aaron would rather save the tokens, the chat ends with one line: **"Build and install: click UPDATE on the
<App> card"**, plus what to look at once it's in. Aaron clicks; Catalyzer builds, installs and restarts the app.
