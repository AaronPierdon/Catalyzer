# 02 — Design: the look

Short and binding. The full reference (laws, all themes, components, motion maths) is the Nexus UI kit
(`Nexus/kit/docs`, skill `nexus-ui`).

## Colour: roles, never literals
- UI code uses **roles**, never hex values:
  - backgrounds: `bg0 / bg1 / bg2`;
  - text: `text / textDim / textFaint`;
  - accents: `accent / accent2 / select`;
  - states: `ok / warn / danger`;
  - surfaces: `glass / glassEdge / line / halo`.
- Hex values live only in theme definitions.
- **accent** is the dominant colour: frames, icons, hover, sliders, loaders.
- **select** is a different hue, for *the chosen / important thing*: selected row, active tab, primary button, focus,
  toggle on, whatever is being touched.
- The default theme *Nexus* uses emerald `#1BF28C` for accent and electric blue `#2EB6FF` for select. Every theme
  keeps that split.
- **Dark is home.** Light themes must work (ink instead of glow) but are never the default.
- **Status speaks through edges:**
  - busy = blue-green pulse;
  - warning = amber pulse;
  - error = red tint.

  Worst wins. Never ad-hoc red banners, toasts or error boxes.

## Light, glass, depth
- **Glass panels:**
  - tinted;
  - scan lines;
  - a sheen;
  - a bright hairline rim;
  - a soft light where the energy sits.

  Transparency is a user setting.
- **Glow lives on its own layer** and never blurs text: a wide, faint halo around things, never offset copies.
- **3D and motion where they add life:** tumbling cores, rings, extruded numbers, traces that pulse with data.
  Drawn as vectors so they stay crisp at any size.
- Every effect has an **efficient fallback** (flat, no blur) for weak GPUs and the Static tier.

## Shape and type
- **Chamfers:** panels and word buttons cut two opposite corners (top-left + bottom-right).
- **Brackets** mark selection and focus.
- Hex and half-hex shapes for badges and tabs.
- **Fonts have roles:**

  | Role | Nexus font | Fallback |
  |---|---|---|
  | UI | Rajdhani | Segoe UI Variable |
  | Headings | Orbitron, UPPERCASE, tracked | Bahnschrift |
  | Live numbers and data | Share Tech Mono | Cascadia Mono |
  | Codes | Zen Dots | — |

  Captions use ` // ` and ` · `.
- **Crisp, not bold.** Text is pixel-snapped. Labels are sharp with a soft coloured glow; bold is for big readouts.
- Small labels over busy scenes get contrast (halo, darker plate); never shrink the scene instead.
- Never scale bitmaps up; draw vectors.

## Motion and energy
- **Nothing pops.**
  - Panels slide or roll in as one piece.
  - Text decodes in.
  - Numbers count.
  - Rings draw in.
  - Cards rise and fade, staggered.
- **Ambient motion is random but smooth** (spring drift in bounds): never a visible loop, never a jump.
- **One energy dial** scales speed and travel; **one shared clock** drives animation with an fps cap.
- Animation pauses when hidden, minimized or behind a fullscreen app.
- **Reduce motion** keeps the meaning (colours still change) and drops pulses and sweeps.
- Waiting never looks frozen: busy dots, comets, progress glow.
- **Motion is the signpost:** keep the ambient field calm and put the energy where attention should go.

## Layout and behaviour
- **Floating parts, not boxes:**
  - no grid lines;
  - no row backgrounds;
  - no stock title bars.

  Text over the scene, held apart by space.
- **Selection** = a select-coloured bar, a fading underline and a ~10 % wash.
- **Interaction:**
  - hit areas ≥ 40 px;
  - the wheel works on every slider and dial;
  - right-click / long-press opens a menu or radial (≤ 8 items);
  - hover shows a decoding tooltip.
- **Responsive:**
  - layouts reflow instead of squashing;
  - minimum window sizes so nothing is crushed;
  - grids fill the width (no dead gutter).
- **Simple when asked:** every app has a calm, plain mode reachable from settings.
- **No stock controls:**
  - sliders are the picture slider (thin track, gradient fill, chamfered knob, value beside it, double-click = default);
  - choices are chamfered chips;
  - toggles are drawn.
- Changing values get a tiny live history graph where it helps.

## Checking a UI change
Render it (`--snapshot`, every theme) and look at every image. Then look on the real screen:
- Is text sharp?
- Are labels readable over the scene?
- Is the glow centred, not smeared?
- Does it resize without clipping?
- Is the efficient mode sane?
- Is reduce motion still usable?
