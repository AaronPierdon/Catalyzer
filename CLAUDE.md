# CLAUDE.md — working in the Catalyzer repo

Follows **Nexus Development Principles** (`repos\Nexus Development Principles`, github.com/AaronPierdon/Nexus-Development-Principles);
this repo's version is in `.nexus-principles`. Read them before working (at least the CHANGELOG since that version).
Rules below only add to them. New chat: read this, then `docs/HANDOFF.md`, `docs/STATE.md`, `docs/BACKLOG.md`, `git log --oneline -20`.

Catalyzer is Aaron's project command center: a WPF / .NET 10 dashboard, extensible through plugins. GitHub: `AaronPierdon/Catalyzer`, branch `main`. On the PC it lives at `C:\Users\Aaron\source\repos\Catalyzer`, next to `Nexus`.

## Shape
- `src/Catalyzer.Sdk` — **the plugin API**.
  - Change it only by adding. Bump `Api.Version` when something new arrives. Never remove or rename anything.
- `src/Catalyzer` — the app.
  - Owns its theme layer (`Cz*` resources). It must never reference Nexus or any plugin.
- `plugins/*` — plugins. Each builds to `<install>\plugins\<Name>\`.
  - `Catalyzer.Nexus` → `NexusPlugin.dll`. It uses Nexus only through `Nexus.UI.Wpf.NexusPublicApi` (Nexus repo, `src/Nexus.Kit/Parts/NexusPublicApi.cs`).
  - `Catalyzer.Link` → `LinkPlugin.dll`. The LINK chip: a hidden Windows terminal per repo, for chats (`docs/LINK.md`).
  - `Catalyzer.Helpers` → `HelpersPlugin.dll`. A project's long-running scripts, hidden, from its card.
- Docs: `docs/LINK.md` (the link), `docs/PLUGINS.md` (plugin API), `docs/MANIFEST.md` (catalyzer.json).

## Running things on the PC (start here)
```bash
bash .catalyzer/link/run.sh --status            # ACTIVE = you have a hidden Windows terminal in this repo
bash .catalyzer/link/run.sh -t 600 -f tools/snap.ps1          # build + render every theme to .scratch/snap
bash .catalyzer/link/run.sh 'git status; git log --oneline -5'
bash .catalyzer/link/run.sh -s -f tools/install.ps1           # install: survive mode (it restarts Catalyzer itself)
```
- If it's OFF, ask Aaron to click **LINK** on the Catalyzer card. Until then, the fallback is the Nexus helper
  (`script catalyzer build|snap|install|repo|git`, see `Nexus/_tools/scripts/catalyzer.ps1`).
- With the link on, **edit the PC copy directly** (the device shell sees this folder) and run git, builds and installs
  through the link. Git runs on Windows with Aaron's credentials; don't run git from the Linux VM (it can't delete
  its lock files there). The tarball sync in HANDOFF is only for when nothing else works.

## Rules
- Look matters: chamfered glass, glow, decode-in text, nothing pops. Every colour comes from the theme. No literals in UI code.
- No console windows, ever: git and helper processes run hidden (`CreateNoWindow`).
- Installing Catalyzer restarts it (and everything its link and helpers were running): say so to Aaron first.
- Settings: `%APPDATA%\Catalyzer\settings.json`. Use `--snapshot <dir>` to render every theme, and look at the PNGs after UI changes.
- The Nexus repo's hard rules still apply to anything there:
  - never print the SQL password;
  - never look in `repos\archives`;
  - warn Aaron before installs that restart Nexus.
- Commit messages end with the attribution lines the session asks for.
