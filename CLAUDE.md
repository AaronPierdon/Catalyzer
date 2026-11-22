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
- Docs: `docs/PLUGINS.md` (plugin API), `docs/MANIFEST.md` (catalyzer.json).

## Rules
- Look matters: chamfered glass, glow, decode-in text, nothing pops. Every colour comes from the theme. No literals in UI code.
- No console windows, ever: git and helper processes run hidden (`CreateNoWindow`).
- Build / install from a cloud chat: Nexus's PC helper runs `script catalyzer build|snap|install|repo|git <args>`
  (`Nexus/_tools/scripts/catalyzer.ps1`). Source travels to the PC as a tar into this folder (see HANDOFF).
- Settings: `%APPDATA%\Catalyzer\settings.json`. Use `--snapshot <dir>` to render every theme, and look at the PNGs after UI changes.
- The Nexus repo's hard rules still apply to anything there:
  - never print the SQL password;
  - never look in `repos\archives`;
  - warn Aaron before installs that restart Nexus.
- Commit messages end with the attribution lines the session asks for.
