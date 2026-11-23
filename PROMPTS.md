# Prompts

Copy and paste these into a chat that's linked to the app's repo.

## Adopt (an app that doesn't follow the principles yet)
> Read the Nexus Development Principles (repos\Nexus Development Principles, or
> github.com/AaronPierdon/Nexus-Development-Principles), all of it. Then audit this app against 06-CHECKLIST: list
> what passes, what fails and what's an intentional exception, and estimate the effort for each fix. Don't change
> code yet. After I pick, add the repo files from 07-APP-REPO, starting from `templates/`: CLAUDE.md,
> `.nexus-principles` with the current VERSION, catalyzer.json, and the docs. Fill VISION, ARCHITECTURE and GUIDE
> from the actual code. Create the GitHub repo if it's missing. Then fix things in the order I chose.

## Update (the principles moved on)
> Update this app to the latest Nexus Development Principles. Read `.nexus-principles` for our version, then
> read CHANGELOG.md in the principles repo for every entry newer than it. For each entry:
> - say whether it applies here;
> - apply it, or record an EXCEPTION in CLAUDE.md with the reason.
>
> Bump `.nexus-principles` to the new version. Then commit with the list of changes and update HANDOFF.

## Audit (spot check)
> Check this app against Nexus Development Principles 06-CHECKLIST and 05-RULES. Report failures only, with
> file:line and a one-line fix each.

## Change the principles
> In the Nexus Development Principles repo: <the change>.
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
> Start a new app called <App>: <what it's for>. Follow the Nexus Development Principles from the start:
> - create the repo from `templates/` and `AaronPierdon/<App>` on GitHub;
> - write VISION from what I said;
> - add catalyzer.json;
> - set up `tools\` build and install scripts.
>
> Then tell me to click LINK on its card in Catalyzer.

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
