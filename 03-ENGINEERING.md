# 03 — Engineering

## Stack
- **Windows desktop apps:** C# / .NET 10, WPF, code-built UI (custom `FrameworkElement`s drawn in `OnRender`, little
  or no XAML).
- **Native renderers** (MAWP): Direct3D / D2D with the kit's `UiTheme`.
- **Web / Electron:** the kit's web tokens.
- **Builds:**
  - Visual Studio or the .NET 10 SDK (`dotnet build <sln>`);
  - solutions build every app with one command;
  - Linux cloud builds use `-p:EnableWindowsTargeting=true`.
- **Shared look:** the Nexus kit (`Nexus.Kit`). An app either references it directly (Nexus apps) or reaches it
  through a plugin (Catalyzer). It never copies colours out of it.

## Theme layer
- Every app has **one theme service**. It writes role brushes and fonts into application resources (`Nx*Brush`,
  `Cz*Brush`, …) and raises `Changed`.
- UI binds with `SetResourceReference` / `DynamicResource`. Code-drawn parts read the current theme and repaint on
  `Changed`.
- A theme is data: colours, fonts, glow, cut, scan, light/dark, an optional backdrop factory. Themes can be
  **registered at runtime** (plugins) and **re-registered under the same id to update live**.
- Following Nexus is a choice ("Follow Nexus"), never a hard dependency. An app must look right with Nexus absent.

## Settings
- **Shared (Nexus-wide):** `%APPDATA%\Nexus\settings.json` (Nexus API v2) and `visual.json`, read via the kit's
  settings service. Changes arrive live.
- **Per app:** `%APPDATA%\<App>\settings.json`.
  - Plain JSON.
  - Written atomically (temp file + move).
  - Tolerant of missing or extra keys.
  - Never written in snapshot / test mode.
- **Logs:**
  - `%LOCALAPPDATA%\<App>\log.txt` (Nexus apps: `%LOCALAPPDATA%\Nexus\logs\<app>.txt`);
  - rolling at ~1 MB;
  - one line per event, timestamped;
  - user-facing messages also go to the app's info bar.
- **Secrets** (passwords, tokens, keys) never sit in settings files, logs, docs or commits. Use DPAPI or the OS
  credential store. Never print them.

## Extensibility
- **Base app + SDK + plugins.**
  - The SDK is a small assembly of interfaces and plain types.
  - Plugins are dlls in their own folders: bundled ones under `<install>\plugins\`, user ones under
    `%APPDATA%\<App>\plugins\`.
  - Plugins switch on or off in Settings (checkboxes) and can be imported from a .dll or a .zip.
- **API compatibility:**
  - `Api.Version` only goes up; members are only added, never removed or renamed.
  - Plugins declare `MinApi`, and the host refuses newer ones with a clear reason.
- **Isolation:** a failing plugin is caught, logged, marked in settings, and never takes the app down.
- **Integrations go in plugins.** The base app never references another app (e.g. Catalyzer knows nothing about
  Nexus; NexusPlugin bridges them). Apps that others integrate with publish a **public API facade** (e.g.
  `NexusPublicApi`) with the same add-only rule.
- **Self-describing data over code:** projects, buttons and the like come from small JSON manifests
  (`catalyzer.json`), so new things need no code changes.

## Processes and the OS
- **Never a console window.** Child processes (git, scripts, helpers) run with `CreateNoWindow` / hidden and with
  timeouts. Git runs with `GIT_TERMINAL_PROMPT=0`.
- **Single instance** where it makes sense: a named mutex; a second launch brings the first forward.
- **Run at Windows start** is opt-in: an HKCU Run key with `--autostart`, which starts minimized and quiet.
- **Command line:**
  - every app supports `--snapshot <dir>` (render main surfaces per theme to PNG, then exit; never touch settings);
  - `--restart` waits for the old instance.
- Installs go to `%LOCALAPPDATA%\Programs\<App>` with a Start menu shortcut. The install script builds Release,
  replaces bundled plugins whole, keeps user data, and updates the Run key if it's set.

## Performance
- **Idle is nearly invisible on the meters** (Nexus bar at idle ≈ 0.3 core / 4 % GPU).
- **Draw only what changed:**
  - one shared clock;
  - an fps cap per tier (Static / Eco / Balanced / Beast);
  - idle throttling after a few seconds;
  - stop when hidden.
- **Nothing that can block runs on the UI thread:** IO, git, network and scans go on background tasks, and results
  are marshalled back.
- Freeze brushes, pens and geometries. Cache geometry that doesn't change.
- **Layout traps we've hit:**
  - a stroked `Path` whose geometry is built from `ActualWidth` grows 1 px per layout pass. Keep such shapes out of
    layout (a `Canvas` host) or draw them in `OnRender`;
  - `ScrollViewer.ViewportWidth` changes when the scroll bar appears, so re-fit on `ScrollChanged`.

## Reliability
- **Fail soft:**
  - catch at boundaries (event handlers, plugin calls, background tasks);
  - log;
  - show a calm message in the info bar;
  - keep running.
- **Never leave the user stranded.** An app that replaces something essential (taskbar, file manager) has a
  watchdog, a panic key and a restore command.
- **Removals are decisions.** Removed features are listed in the app's `STATE.md` → *Removed / decided against* and
  don't come back unless Aaron asks.
- **Tests:** plain-.NET cores (no UI) get unit tests. Latency-critical paths (e.g. InputShare hooks) never allocate,
  block, log or touch UI.
