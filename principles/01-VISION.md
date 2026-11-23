# 01 — Vision

## One line
**One world across every screen Aaron owns.** PC, laptop, phone, TV, file server: wherever he sits down, it should
feel like the same starship console, with the same colours, light, motion and controls, and his things in reach.

## The feeling
- **Holographic display:** a *Star Trek: Discovery* bridge console with cyberpunk energy.
- Light floating over a living scene, not widgets in boxes.
- Things spin, breathe and pulse; they're colourful and lit from inside.
- Hairline-fine edges and sharp text, as if rendered at 8K even at 1440p.
- Never chunky, never pixelated, never "Windows dialog".
- References: Discovery precision, Mass Effect chamfers, Minority Report glass, Cyberpunk neon.

Beautiful and cool is a requirement, not a polish pass. But these are tools Aaron uses all day, so they must also be
**fast, readable and calm enough to live in.**

## The family
| Layer | What |
|---|---|
| **Nexus** | the Windows shell: bar, Start, Command Center, Explorer, Tasks, wallpaper, groups, InputShare |
| **Nexus kit** | today's WPF implementation of the look (02), plus the Nexus API (theme, visual settings, energy) Nexus apps subscribe to |
| **MAWP** | the cornerstone media player and archive (music now; video, photos, e-reader later) |
| **Catalyzer** | the developer hub: every repo on glass, its notes, its hidden terminal (the link), new projects, and the home of these principles |
| **Everything else** | utilities and apps, adopting the kit and these principles one by one |

## Principles
1. **One look, everywhere.** Every app reads the same theme and visual settings. Change the theme once and
   everything follows, live.
2. **The platform stays underneath.** Windows keeps drivers, security and compatibility; our apps replace what you
   see and touch. If an app dies, it never takes the user's way of working with it (the taskbar, the files).
3. **Simple by default, deep on demand.** Things behave the way people expect out of the box. Every extra is a
   switch with a sane default. Every app can be made **simpler and calmer**, not only more elaborate.
4. **Energy is a dial.** One setting scales all motion, from nearly still to full show. Performance tiers keep it
   cheap; hidden things stop animating.
5. **Our own UI, never stock dialogs.** Confirmations, progress, pickers and menus are ours, in the same look.
6. **Extensible, not hardwired.** Apps expose small, stable APIs and plugin points. Integrations (Nexus ↔ Catalyzer,
   themes, repos) live in plugins, not in base code.
7. **Many devices, one system.** Settings sync, data is shared where it should be, installs are one step.
8. **One hub for building.** Every app lives in its own GitHub repo, shows up in Catalyzer, and carries its own
   notes and docs. Any machine and any chat can pick it up from there, even from the phone.
9. **It's a real product.** Even when built for one person, it's built so someone else could pick it up: docs,
   versioned APIs, clean installs, no secrets in the repo.
