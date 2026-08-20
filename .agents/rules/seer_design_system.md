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
- **Panel headers:** numbered convention, e.g. `[1] CPU`, `[2] MEMORY`,
  `[3] NETWORK` — evokes a terminal multiplexer / instrument-panel feel.
  Fixed height, `8px 12px` padding, `1px` bottom border separating header
  from content.
- Panels displaying multi-core or multi-channel data (e.g. per-core CPU
  load) use a dense inline bar format:
  `0[||||      16.7%]` — compact, scannable, avoids needing a full chart
  for data that's really just N small numbers.
- Below the status strip and metric panels, the trend section holds up to three compact per-metric charts (CPU / GPU / Memory), each in its own channel color, rather than one overlaid multi-series chart. Chosen over a single overlaid chart because separate per-metric lines stay readable even when values cross frequently — clarity per metric outweighs the space savings of a single chart. Still bounded: three is the ceiling, not a starting point. Don't scatter small sparklines beyond these three, and don't add a fourth without revisiting this section first.

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
- **Glow/bloom effect: ON TRIAL.** Chart lines may use a soft outer glow
  (light bloom around the stroke) for a "futuristic" feel, matching the
  Dashtera-style references. This is explicitly not locked in — if it
  reads as eye strain during extended use (this is a background tool
  people will have open for hours), cut it in favor of crisp, glow-free
  strokes. Revisit after real usage, not in isolation.
- Use the channel colors above consistently per data series.
- No 3D effects, no gradients-as-fill under lines unless subtle and dark.

### Micro-Animations
- **Global transition:** fast, crisp (`120ms ease`). Nothing should feel
  soft or bouncy.
- **Boot sequence:** staggered fade-in (`fadeIn 0.4s ease-out`) — fine to
  keep for an app-launch moment.
- **Status dots:** `6px` circular, success/danger colors. Info-state dots
  may pulse slowly (`2s ease-in-out`) to indicate "live/updating," not
  "attention needed."

---

## Rules for AI Implementation

1. **Never use drop shadows (`box-shadow`)** on panels or cards.
2. **Panels and cards: `0px` radius, no exceptions.** Only status
   badges get slight rounding (`4px`) — see Components.
3. **Monospace only.** `JetBrains Mono` or an equivalent fallback,
   everywhere, no exceptions for body text or headers.
4. **Use exact hex codes** from the palette above — do not approximate.
5. **Glow on chart lines is experimental** — implement it, but keep it
   isolated/toggleable if reasonably easy, so it can be cut without a
   rewrite if it doesn't hold up.
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
