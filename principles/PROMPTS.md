# Prompts

Copy and paste these into a chat that's linked to the app's repo.

## Adopt (an app that doesn't follow the principles yet)
> Read the Catalyzer Development Principles (repos\Catalyzer\principles, or
> github.com/AaronPierdon/Catalyzer/tree/main/principles), all of it. Then audit this app against 06-CHECKLIST: list
> what passes, what fails and what's an intentional exception, and estimate the effort for each fix. Don't change
> code yet. After I pick, add the repo files from 07-APP-REPO, starting from `templates/`: CLAUDE.md,
> `.catalyzer-principles` with the current VERSION, catalyzer.json, and the docs. Fill VISION, ARCHITECTURE and GUIDE
> from the actual code. Create the GitHub repo if it's missing. Then fix things in the order I chose.

## Update (the principles moved on)
> Update this app to the latest Catalyzer Development Principles. Read `.catalyzer-principles` for our version, then
> read `principles/CHANGELOG.md` in the Catalyzer repo for every entry newer than it. For each entry:
> - say whether it applies here;
> - apply it, or record an EXCEPTION in CLAUDE.md with the reason.
>
> Bump `.catalyzer-principles` to the new version. Then commit with the list of changes and update HANDOFF.

## Audit (spot check)
> Check this app against Catalyzer Development Principles 06-CHECKLIST and 05-RULES. Report failures only, with
> file:line and a one-line fix each.

## Change the principles
> In the Catalyzer repo, in `principles/`: <the change>.
> - Pick MAJOR / MINOR / PATCH per the README.
> - Bump VERSION.
> - Add a CHANGELOG entry that says what changed and **what an app must do**.
> - Commit and push.
>
> Then list which apps need the Update prompt.

## Note this (from anywhere, even the phone)
> Note this for <App>: <the idea>.

The chat adds it to `docs/NOTES.md` → *Inbox* in that app's repo, commits and pushes. Notes that apply to every
app go into these principles instead.

## New app
> Start a new app called <App>: <what it's for>. Follow the Catalyzer Development Principles from the start:
> - create the repo from `templates/` and `AaronPierdon/<App>` on GitHub;
> - write VISION from what I said;
> - add catalyzer.json;
> - set up `tools\` build and install scripts.
>
> Then tell me to click LINK on its card in Catalyzer.

## Project instructions (paste into each Claude project's settings)
Keep this short. The detail lives in the repo (CLAUDE.md, the docs) and in these principles, so it can't drift.
> Project repo: github.com/AaronPierdon/<App> (PC: `C:\Users\Aaron\source\repos\<App>`)
> Rules: Catalyzer Development Principles, github.com/AaronPierdon/Catalyzer/tree/main/principles
> (PC: `C:\Users\Aaron\source\repos\Catalyzer\principles`)
>
> Every chat:
> 1. Read CLAUDE.md and follow its new-chat steps (handover, notes, state, git log).
> 2. Check the Catalyzer link: `bash .catalyzer/link/run.sh --status`. If it's OFF, ask me to click LINK on the card.
>    Builds, git and installs go through the link.
> 3. Follow the principles for the look, the workflow and the repo docs. Never bring back anything in STATE →
>    *Removed / decided against*.
> 4. Tell me in a few lines what you understood and what you'll do, then start.
> 5. End every piece of work by updating HANDOFF (and NOTES / STATE if they changed), committing and pushing.

## Kickoff (Catalyzer hands this out for a new app)
Catalyzer fills in `<App>`, `<what it's for>` and the paths, and shows the setup steps from 04 *Setting up a project
in Claude* next to it. Paste it into the first chat of the new project.
> This is the first chat for <App>: <what it's for>.
> - App repo: github.com/AaronPierdon/<App> (create it from the principles' `templates/` if it's empty). On the PC:
>   `C:\Users\Aaron\source\repos\<App>`.
> - Principles: github.com/AaronPierdon/Catalyzer/tree/main/principles (`repos\Catalyzer\principles`). Read
>   README, 04-WORKFLOW, 05-RULES and 07-APP-REPO first.
> - Hub: github.com/AaronPierdon/Catalyzer (`repos\Catalyzer`). Add `catalyzer.json` so the app gets a card, and set
>   up `tools\` build and install scripts so the Catalyzer link can run them.
> - Write VISION from what I said, fill CLAUDE.md, HANDOFF, NOTES and STATE, and set `.catalyzer-principles` to the
>   current VERSION. Push.
> - Then give me the project instructions to paste and the Morning brief task to schedule.

## Morning brief (the project's scheduled task)
> Morning brief for <App>. Read only CLAUDE.md, docs/HANDOFF.md, docs/NOTES.md (Inbox, Next up), docs/STATE.md and
> `git log --oneline -20`. Don't read source code, build or test. Check the Catalyzer link (`--status`) and whether
> the app is behind the principles VERSION. Then tell me in under 10 lines:
> - where things stand and what's waiting to test;
> - the first move from HANDOFF → Next, and the next two after it;
> - anything blocked on me.
>
> Then stop and wait.

## Retire a thread
> We're retiring this thread. Update the docs and handover first:
> - update HANDOFF / STATE / NOTES (and ARCHITECTURE / GUIDE if the shape changed);
> - write anything unfinished into HANDOFF → *Next*;
> - push.
>
> Then give me a prompt for the next thread that says:
> - which repo and branch;
> - what's done and what's next;
> - what to read first.
