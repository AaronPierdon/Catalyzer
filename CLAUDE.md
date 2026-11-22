# CLAUDE.md — working in the Catalyzer repo

Catalyzer is Aaron's project command center: a WPF / .NET 10 dashboard, extensible through plugins. GitHub: `AaronPierdon/Catalyzer`, branch `main`. On the PC it lives at `C:\Users\Aaron\source\repos\Catalyzer`, next to `Nexus`.

## Shape
- `src/Catalyzer.Sdk` — **the plugin API**.
  - Change it only by adding. Bump `Api.Version` when something new arrives. Never remove or rename anything.
- `src/Catalyzer` — the app.
  - Owns its theme layer (`Cz*` resources). It must never reference Nexus or any plugin.
- `plugins/*` — plugins. Each builds to `<install>\plugins\<Name>\`.
  - `Catalyzer.Nexus` → `NexusPlugin.dll`. It uses Nexus only through `Nexus.UI.Wpf.NexusApi` (Nexus repo, `src/Nexus.Kit/Parts/NexusApi.cs`).
- Docs: `docs/PLUGINS.md` (plugin API), `docs/MANIFEST.md` (catalyzer.json).

## Rules
- Look matters: chamfered glass, glow, decode-in text, nothing pops. Every colour comes from the theme. No literals in UI code.
- No console windows, ever: git and helper processes run hidden (`CreateNoWindow`).
- Settings: `%APPDATA%\Catalyzer\settings.json`. Use `--snapshot <dir>` to render every theme, and look at the PNGs after UI changes.
- The Nexus repo's hard rules still apply to anything there:
  - never print the SQL password;
  - never look in `repos\archives`;
  - warn Aaron before installs that restart Nexus.
- Commit messages end with the attribution lines the session asks for.
