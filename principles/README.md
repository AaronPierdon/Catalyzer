# Catalyzer Development Principles

**Where they live:** in the Catalyzer repo, folder `principles/`. That's `C:\Users\Aaron\source\repos\Catalyzer\principles`
on the PC, and github.com/AaronPierdon/Catalyzer/tree/main/principles on GitHub. Until 3.0.0 they were a separate repo, *Nexus Development Principles*; that
repo now only points here.

**Why here:** Catalyzer is the hub for making and maintaining Aaron's software: every project's card, notes, link
and kickoff. These principles are the rulebook it enforces, so the app and its rules move together.

The rulebook for every app Aaron builds: Nexus, Catalyzer, MAWP and whatever comes next. It covers:
- **one look:** the sci-fi console;
- **one way to build:** how code, settings, plugins and performance work;
- **one way to work:** chats, branches, handovers, safety, and the docs every app keeps;
- **one hub:** every app is built with Catalyzer in mind (GitHub repo, `catalyzer.json`, notes in the repo, the
  link for running things).

Every app repo points here and records which version of these principles it follows. When the principles change,
each app can be told "update to the latest principles", and it knows what changed since its version.

**Current version: see [`VERSION`](VERSION).** What changed: [`CHANGELOG.md`](CHANGELOG.md).

## The documents
| # | File | What it governs |
|---|---|---|
| 01 | [VISION](01-VISION.md) | what we're building and why: one world across every screen |
| 02 | [DESIGN](02-DESIGN.md) | the look: colour roles, glass, glow, type, chamfers, motion, energy |
| 03 | [ENGINEERING](03-ENGINEERING.md) | how apps are built: stack, theme layer, settings, extensibility, performance, reliability |
| 04 | [WORKFLOW](04-WORKFLOW.md) | how work happens: chats, branches, handovers, testing, shipping |
| 05 | [RULES](05-RULES.md) | the hard rules: safety, privacy, never-dos |
| 06 | [CHECKLIST](06-CHECKLIST.md) | definition of done: what every app and every change must pass |
| 07 | [APP REPO](07-APP-REPO.md) | what every app repo contains: the five docs (notes, summary, architecture, handoff, roadmap), GitHub, Catalyzer |
| 08 | [ASSETS](08-ASSETS.md) | reusable assets: what's shared, and where it lives (growing) |
| — | [templates/](templates/) | starter files for a new app's CLAUDE.md and docs |
| — | [PROMPTS](PROMPTS.md) | copy-paste prompts: kickoff a new app, morning brief, adopt, update, audit |

## How apps follow it
1. Each app repo has a `.catalyzer-principles` file at its root containing the version it conforms to, e.g. `5.0.0`.
   Apps from before 3.0.0 have `.nexus-principles` instead; they rename it on their next update.
2. Its `CLAUDE.md` starts with: *"Follows Catalyzer Development Principles (github.com/AaronPierdon/Catalyzer/tree/main/principles),
   version in `.catalyzer-principles`. Read them before working; app-specific rules below only add to them."*
3. **Update flow:** the principles change → bump `VERSION` and write `CHANGELOG.md`. Then, in each app's chat:
   *"update to the latest Nexus principles"* ([PROMPTS.md](PROMPTS.md)). The chat reads the changelog entries newer
   than the app's `.catalyzer-principles`, applies them, and bumps the file.
4. An app may be **behind** but never **against**: if an app must break a rule, its CLAUDE.md says which and why
   (an *exception*), and the next update revisits it.

## Versioning
- `MAJOR` — a rule changed or was removed; apps must act (e.g. a new required setting, a renamed role).
- `MINOR` — something new is recommended; apps should pick it up when touched.
- `PATCH` — wording, clarification, examples; nothing to do.

## Precedence
Aaron's latest word > the app's own CLAUDE.md exceptions > these principles > habit.
These documents are the summary. The full design reference (tokens, components, motion maths) lives in the Nexus
UI kit: `Nexus/kit/docs` and the skills `nexus-ui`, `nexus-adopt`, `nexus-theme-forge`. Those are today's
implementation of the look described in 02 (see 02 → *Where the look comes from*).

## Changing the principles
- Use PROMPTS → *Change the principles*: pick MAJOR / MINOR / PATCH, bump VERSION, and add a CHANGELOG entry that
  says what an app must do. Never edit a rule without a changelog entry.
- Keep documents short and binding. Detail belongs in the Nexus UI kit or in the app's own docs.
- Aaron's latest word wins. When he states a new standing preference in any app's chat, it belongs here too.
