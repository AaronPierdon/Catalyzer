# 08 — Reusable assets (growing)

Make something once, use it everywhere. Nothing here is required yet. The direction is:
- **Look:** 02 defines it; the Nexus kit (`Nexus.Kit`) is the shared WPF source for theme roles, controls, glass, glow and motion.
  An app uses the kit, or a thin bridge to it (Catalyzer's NexusPlugin), and never copies it.
- **Templates:** [`templates/`](templates/) here, for the repo docs. New apps start from them.
- **Candidates to collect** (add to this list when one shows up twice):
  - icons and glyph sets;
  - UI sounds;
  - the install / snap / build scripts (`tools\`);
  - the settings and log helpers;
  - the single-instance / tray / run-at-start pattern;
  - the Catalyzer plugin skeleton.
- **The rule, when it's time:** a shared asset lives in one repo (the kit, or a future `assets` repo), is
  versioned, and is pulled in, not pasted. The app's ARCHITECTURE says which shared assets it uses.
