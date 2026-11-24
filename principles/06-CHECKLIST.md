# 06 — Definition of done

## Every change
- [ ] Builds clean (no new warnings in our code).
- [ ] Triple-tested: normal path, edge cases, conflicts.
- [ ] UI changes rendered with `--snapshot` in every theme and **looked at**; checked on the real screen when it's
      about look or motion.
- [ ] Reduce motion and the Static / efficient tier still usable.
- [ ] No colour literals, no stock dialogs or controls, no console windows.
- [ ] Nothing removed came back.
- [ ] HANDOFF updated. SUMMARY updated if a feature changed; ARCHITECTURE if a part, library, class, flow or
      decision did; ROADMAP if a chunk started or shipped or an idea was dropped. NOTES untouched.
- [ ] Pushed.

## Every app (conformance)
**Look**
- [ ] Theme service with role brushes; switches live; follows Nexus when Nexus is present (plugin or kit), works
      without it.
- [ ] Glass panels, chamfers, brackets, glow on its own layer, halo behind text.
- [ ] Fonts by role; headings uppercase and tracked; crisp text.
- [ ] Things arrive (decode / rise / draw-in), nothing pops; energy-scaled ambient motion; pauses when hidden.
- [ ] Status through the info bar and edge glow (busy / warning / error), not message boxes.
- [ ] A simple / calm mode.

**Build**
- [ ] Settings in `%APPDATA%\<App>\settings.json` (atomic, tolerant); log in `%LOCALAPPDATA%\<App>`.
- [ ] `--snapshot <dir>`; single instance if it's a window app; optional run-at-start.
- [ ] Hidden child processes with timeouts; nothing blocking on the UI thread.
- [ ] Extensible where it makes sense: SDK + plugins (add-only API, MinApi, isolated failures).
- [ ] An install script to `%LOCALAPPDATA%\Programs\<App>` + a Start menu shortcut.

**Repo**
- [ ] The files from [07-APP-REPO](07-APP-REPO.md): `CLAUDE.md`, `.catalyzer-principles`, README, `catalyzer.json`, and
      docs (NOTES, SUMMARY, ARCHITECTURE, HANDOFF, ROADMAP), filled in with no placeholders left.
- [ ] Its own GitHub repo (`AaronPierdon/<App>`), with `main` pushed. Shows up in Catalyzer.
- [ ] Build, snap and install are `tools\` scripts that a linked chat can run.
- [ ] Spartan (07): nothing at the root beyond the listed files; one solution; `_devSpace/` ignored; no
      `docs/history/`; no big files or one-off scripts in git; no untracked code that matters.
