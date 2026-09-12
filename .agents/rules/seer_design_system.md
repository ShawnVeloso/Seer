# Seer HUD Design System (v1)

This document is the complete front-end design reference for **Seer**, a
minimal real-time PC hardware monitor. It exists so any AI coding agent
(or human) can recreate the exact aesthetic without guessing.

Seer inherits its structural DNA (flat panels, hairline borders, monospace
typography, no glassmorphism) from a prior project's design system, but
uses its own color identity and layout conventions — see §"What Changed
From the Prior System" at the bottom for the reasoning.

---

## Core Aesthetic

- **Theme:** Terminal/HUD-inspired, cool cyan telemetry palette.
- **Style:** Monospace-first, flat panels, hairline borders, sharp corners
  on structure — fully-rounded on status indicators only (see Components).
- **Constraints:** No glassmorphism, no heavy drop shadows, no large border
  radii on panels or cards. Should feel like a precise instrument panel,
  not a SaaS analytics dashboard.
- **Density:** Minimal. Prefer fewer, larger panels over many small tiles.
  This is a background/always-on tool — it needs to be glanceable, not
  data-dense like a BI dashboard.

---

## Color Palette

Dark, near-black foundation with a cyan-led multi-hue accent system. Unlike
a single-accent-color system, Seer needs multiple simultaneously
distinguishable hues, because several data channels (GPU, CPU, RAM, fan,
clock) will often be plotted on screen at once.

### Backgrounds & Panels
- **App Background (`--bg`):** `#08080a`
- **Panel Background (`--panel`):** `#0e0e11`

### Borders
- **Standard Border (`--border`):** `#2a2a2e`
- **Active Border (`--border-active`):** `#4dd8ff` (matches primary accent)

### Typography Colors
- **Primary Text (`--text`):** `#c9c9ce`
- **Dim/Secondary Text (`--text-dim`):** `#6a6a70`

### Accent / Channel Colors
Use these to distinguish simultaneous data series (e.g. CPU vs. GPU vs.
RAM on the same chart). Don't reassign a channel's color once picked in a
given view — consistency matters more than variety.

- **Primary Accent (`--accent`):** `#4dd8ff` (cyan — default/primary series, active borders, focus states)
- **Secondary (`--accent-2`):** `#5b8dff` (blue)
- **Tertiary (`--accent-3`):** `#b083ff` (purple)
- **Success (`--success`):** `#3ddc84` (green — nominal/healthy state)
- **Warning (`--warning`):** `#ffb020` (amber — elevated/approaching limit)
- **Danger (`--danger`):** `#ff5c5c` (red — critical state only)
- **Peak (`--peak`):** `#ff4fb0` (magenta — **strictly reserved**)

### The peak accent

Magenta marks *moments*, never quantities: peak-hold ticks, the newest point
on a trend line, and event timestamps. Nothing else may use it.

The restriction is not stylistic. Magenta sits between the GPU channel's
violet and the critical red, so a magenta fill or border reads as a third
alarm colour and weakens both of the real ones. At 1px, or on a 9px
timestamp, it reads as punctuation instead — which is what it is.

---

## Typography

- **Primary Font Stack:** `'JetBrains Mono', 'IBM Plex Mono', ui-monospace, SFMono-Regular, monospace`
- **Base Settings:** `font-size: 13px; line-height: 1.6;`
- **Panel Labels & Headers:** `font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.08em;`
- **Tags & Metadata:** `9px`–`10px`, uppercase.
- **Numeric Data:** always `font-variant-numeric: tabular-nums;` so values
  don't shift width as they update in real time.

---

## Layout & Structure

### 1. App Shell
`100vh`, `overflow: hidden`, background `--bg`.

### 2. Title Bar (Top)
- Fixed `32px` height, `--panel` background, `1px` bottom border `--border`.
- Wordmark: `SEER` (cyan, uppercase).
- If Electron: native window controls via `titleBarOverlay`, `#0e0e11`
  background, `#c9c9ce` symbol color.

### 3. Status Strip (below title bar)
Borrowed from reference dashboards: a thin horizontal strip showing the
highest-priority metrics as slim percentage bars (e.g. CPU / MEM / GPU),
plus a single system-state badge (see Components → Status Badge) reading
something like `NOMINAL` or `CRITICAL`. This is the "glance" layer —
someone should be able to read system health from across the room without
reading any numbers.

### 4. Main Panels (body)
- Flat `--panel` background, `1px solid --border`.
- **Border radius: `0px`.** No exceptions for structural panels.
- **Panel headers sit *in* the top border line**, btop-style, not in a row
  beneath it: the title knocks a gap in the rule it sits on, with a real fact
  about the panel right-aligned on the same line (thread count, VRAM size,
  ping target, session totals). This is worth about a header row per panel
  across nine panels, and it is why the app no longer needs to scroll at its
  default size.
- **Numbered convention retained:** `[1] CPU`, `[2] MEMORY`. The index is a
  tinted tab at the start of the border line.
- **Top-right corner is chamfered** at 45°, roughly 10px. A WPF `Border`
  cannot cut a corner, so panels are drawn with `Controls/ChamferShape.cs` —
  fill and outline in one geometry, so the edge follows the cut.
- **Corner brackets appear only when they mean something:** on hover, or on a
  panel whose own readings have left nominal, in that severity's colour.
  Brackets on every panel at all times say nothing. There is no top-right
  bracket; the chamfer has that corner.
- Panels displaying multi-core or multi-channel data (e.g. per-core CPU
  load) use a dense inline bar format:
  `0[||||      16.7%]` — compact, scannable, avoids needing a full chart
  for data that's really just N small numbers.
- Below the status strip and metric panels, the trend section holds up to three compact per-metric charts (CPU / GPU / Memory), each in its own channel color, rather than one overlaid multi-series chart. Chosen over a single overlaid chart because separate per-metric lines stay readable even when values cross frequently — clarity per metric outweighs the space savings of a single chart. Still bounded: three is the ceiling, not a starting point. Don't scatter small sparklines beyond these three, and don't add a fourth without revisiting this section first.

**Amended once, deliberately:** the ping tape is a fourth plot. It earns the
exception because it charts a different axis from the other three — one mark
per reply rather than one per second — and because jitter and packet loss
have a shape that the four numbers beside it cannot show. It appears only
while the ping monitor is running, so it is absent from the default window.
The ceiling is now three continuous charts plus the tape; the next addition
reopens this paragraph again.

---

## Components & UI Patterns

### Status Badge / Tags
**Exception to the sharp-corners rule.** Status indicators — system state
badges (`NOMINAL`/`CRITICAL`), small metadata tags — use slight
rounding (`border-radius: 4px`). This is intentional: a rounded
shape gives an instant visual cue "this is a state marker," distinct from
the rectangular grid of data panels around it. Sharp corners stay
reserved for panels, cards, and containers.

### Buttons & Inputs
- `border-radius: 2px` — slightly softened, not sharp, not pill.
- `1px solid --border`, `--border-active` on focus/hover.

### Scrollbars
- `width: 6px` (`4px` in tight spaces).
- Track: transparent or `--panel`.
- Thumb: `--border` or `--text-dim`, `border-radius: 2px`, brightens on hover.

### Charts / Trend Lines
- **Glow is settled: it is a halo, not a shadow.** The soft outer light is a
  second copy of the stroke, drawn wider (6px against 1.5px) at low opacity,
  beneath the real one. It is *not* a `DropShadowEffect`. An effect forces an
  intermediate render target every time its subtree invalidates, which on a
  chart redrawing every second, for hours, is a recurring cost for something
  that looks static. The halo costs one extra polyline sharing the same point
  collection.
- The trial the earlier version called for is over: at 18% opacity the light
  reads without eye strain over long sessions.
- Use the channel colors above consistently per data series.
- No 3D effects, no gradients-as-fill under lines unless subtle and dark.
- The newest sample carries a write-head dot in the peak accent, so "now" is
  identifiable without reading an axis.

### Micro-Animations
- **Global transition:** fast, crisp (`120ms ease`). Nothing should feel
  soft or bouncy.
- **Status dots:** `6px` circular, success/danger colors.

---

## Motion

Seer is a monitor, so people read movement on it as data. That makes motion a
data channel, and it is governed accordingly.

1. **Every motion names its source.** If you cannot say which reading drives
   it, it does not ship. This rules out the entire vocabulary of decorative
   HUD motion — rotating rings, radar sweeps, scanlines, glitch text,
   scrolling hex, idle "breathing" glows.
2. **Cadence is the data's cadence.** Things move on the poll or on the ping
   interval. Nothing gets a timer of its own.
3. **When the data stops, the motion stops.** A frozen detail is information:
   sampling has stalled. Never fake continuity through a gap.
4. **One moment per tick, then idle.** Animations are started by the tick and
   last at most 600ms. Nothing repeats forever — a single perpetual pulse
   pins the compositor at 60fps for the life of an always-on app. Every
   animation releases its clock when it finishes, or the element stays in the
   animated-properties set permanently.
5. **Everything that moves also has a resting state.** With the Windows
   "Show animations" setting off, each detail still updates on the poll; it
   simply stops transitioning. The gate is `HudConfig.MotionAllowed`, checked
   in `Controls/Pulse.cs` and nowhere else.
6. **Markers are drawn, never typed.** Arrows, ticks, dots and bars are
   geometry. A glyph like `▲` falls back to a different face wherever the
   monospace font is missing, at a different advance width, which silently
   breaks the alignment of anything measured in characters.

Each moving detail sits behind its own `HudConfig` flag, so any single one can
be cut without unpicking the others.

---

## Rules for AI Implementation

1. **Never use drop shadows (`box-shadow`)** on panels or cards.
2. **Panels and cards: `0px` radius, no exceptions.** Only status
   badges get slight rounding (`4px`) — see Components.
3. **Monospace only.** `JetBrains Mono` or an equivalent fallback,
   everywhere, no exceptions for body text or headers.
4. **Use exact hex codes** from the palette above — do not approximate.
5. **Glow is a halo stroke, never an `Effect`.** See Charts. The only blur
   left in the app is the wordmark, and it is `BitmapCache`d because it never
   changes.
8. **Meters are drawn, not typed.** `Controls/SegmentMeter.cs` renders every
   bar in the app — status strip, disk, network, processes, RAM and VRAM.
   Throughput meters are logarithmic: on a linear bar scaled to gigabit,
   ordinary traffic never lights a single segment, which is how the old
   bracket bars came to look permanently empty.
9. **Anything estimated says so.** Disk session totals are integrated from
   one-second rate samples, because Windows exposes disk throughput as a rate
   with no cumulative counter behind it. They are rendered with a leading
   `~`. Network totals come from real byte counters and carry no tilde.
6. **Minimal density.** If a screen is starting to look like a BI
   dashboard (many small tiles crammed edge-to-edge), that's a signal to
   consolidate, not add more panels.
7. **WPF Text Rendering.** Always use `TextOptions.TextFormattingMode="Display"` and `TextOptions.TextRenderingMode="ClearType"` at the Window level to prevent text aliasing.

---

## What Changed From the Prior System

Seer's structure (flat panels, hairline borders, monospace, no
glassmorphism) is inherited from an earlier project's design system.
Everything specific to that project's purpose — chat message bubbles, a
command-line chat input, `user>`/`assistant>` line prefixes — has been
deliberately dropped here, since Seer is a data-monitoring tool, not a
chat interface. The color identity was also replaced outright: a
single-accent amber palette works for a one-stream chat log, but doesn't
give enough distinguishable hues for several simultaneous hardware
channels on one chart, hence the cyan-led multi-hue system above.
