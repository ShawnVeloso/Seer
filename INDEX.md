# Seer — Agent Log Index

> **Purpose:** Persistent state-tracking for AI agents and the lead developer.
> **Last Updated:** 2026-08-17T16:14 (+08:00)

---

## Current Focus
- **Working on:** Nothing (merging feature branches into main)
- **Next up:** Status strip percentage bindings
- **Then:** System tray integration
- **Blocked on:** nothing

> This block must always reflect current reality. Update it as the LAST step of
> every task, in the same commit as the code change. See AGENTS.md §Sync Order.

---

## Project Status Overview

| Feature | Status | Notes |
|---------|--------|-------|
| Project scaffold (WPF/.NET 8) | ✅ Complete | PR pending review |
| LibreHardwareMonitorLib smoke test | ✅ Complete | 3 devices / 96 sensors detected; CPU temps/clocks need admin |
| Design system → WPF ResourceDictionary | ✅ Complete | `Styles/Theme.xaml` |
| Placeholder UI shell | ✅ Complete | Title bar, status strip, 3 panels |
| Live CPU/Memory Panels | ✅ Complete | Wired to DispatcherTimer; admin fallback logic implemented |
| Live GPU Panel | ✅ Complete | Adds Hot Spot and Fan Speed; fits in 2 columns |
| Trend History Charts | ✅ Complete | 120-second rolling buffer charts for CPU/MEM/GPU Load |
| Elevation-on-demand | ✅ Complete | Defaults to non-elevated, in-app UX for admin relaunch |

---

## File Manifest

> The single most valuable section for an agent — links feature concepts directly
> to absolute file paths, so no directory crawling is needed for known work.
> Keep this current. If ARCHITECTURE.md is added later, it explains *why*;
> this table stays the map of *where*.

### Project Root

| File | Purpose |
|------|---------|
| `src/Seer/Seer.csproj` | .NET 8 WPF project file; NuGet ref to LibreHardwareMonitorLib |
| `src/Seer/app.manifest` | Requests admin elevation for hardware sensor access |
| `src/Seer/App.xaml` / `src/Seer/App.xaml.cs` | WPF application entry point; merges Theme.xaml, runs sensor smoke test |
| `src/Seer/MainWindow.xaml` / `src/Seer/MainWindow.xaml.cs` | Main UI shell — custom chrome title bar, status strip, placeholder panels |
| `.gitignore` | Standard .NET gitignore (bin/, obj/, .vs/, etc.) |
| `seer_design_system.md` | Front-end design reference (colors, typography, layout rules) |
| `AGENTS.md` | Agent rulebook (all project rules in one place) |
| `INDEX.md` | This file — project state, file manifest, log entries |
| `CHANGELOG.md` | Archive for INDEX.md log entries once they exceed 10 |

### Styles

| File | Purpose |
|------|---------|
| `src/Seer/Styles/Theme.xaml` | WPF ResourceDictionary — all design system tokens (colors, brushes, typography, panel/button styles) |

### Models

| File | Purpose |
|------|---------|
| `src/Seer/Models/SensorSnapshot.cs` | Typed records for sensor readings (`CpuMetrics`, `MemoryMetrics`, `GpuMetrics`), handling nullable/elevation-gated values |

### Services

| File | Feature | Purpose |
|------|---------|---------|
| `src/Seer/Services/HardwareMonitorService.cs` | Sensor access | Wraps `Computer`; `GetCpuMetrics()`, `GetMemoryMetrics()`, `GetGpuMetrics()`, and smoke test |

---

## Architecture Summary

```
Single-process WPF app (requires admin elevation for sensor access).

  src/Seer/App.xaml.cs          → startup, smoke test trigger
  src/Seer/MainWindow.xaml      → UI shell (custom chrome, placeholder panels)
  src/Seer/Styles/Theme.xaml    → design tokens (colors, typography, styles)
  src/Seer/Services/            → sensor logic (separated from UI per AGENTS.md §4)
```

> A full ARCHITECTURE.md is intentionally NOT created at project start — see
> AGENTS.md for the rationale. Once the backend/frontend split solidifies,
> spin up a lightweight ARCHITECTURE.md to record the *why* behind that
> decision, not just restate this summary.

---

## Key Configuration

| Variable | Source | Value |
|----------|--------|-------|
| Admin elevation | `app.manifest` | `asInvoker` by default; in-app relaunch uses `requireAdministrator` equivalent via `Verb="runas"` |
| Target framework | `src/Seer/Seer.csproj` | `net8.0-windows` |
| LibreHardwareMonitorLib | NuGet | `0.9.4` |

> **Constraint Note:** Admin elevation (`requireAdministrator`) is a hard requirement. Without it, LibreHardwareMonitorLib cannot access the Ring0/kernel drivers needed to read CPU metrics like Core Temperature, Core Clock speeds, and Package Power (they return `0` or `NaN`), which are primary metrics for the Seer HUD. While GPU and Memory sensors *can* be read without admin, the core CPU telemetry requires it.

---

## Running the App

```bash
# Build
dotnet build src/Seer/Seer.csproj

# Run (defaults to non-elevated)
dotnet run --project src/Seer/Seer.csproj
```

---

## Log Entries

> **Keep only the last 10 entries here.** Once this table exceeds 10 rows,
> move the oldest entries to CHANGELOG.md verbatim (don't summarize —
> just relocate). This table is for recent momentum only; full history
> lives in CHANGELOG.md.
>
> Every entry must be added as part of the SAME commit/PR as the code
> change it describes — never a standalone "docs sync" commit. See
> AGENTS.md §Sync Order for the full rule set.

| Date | Agent | Action |
|------|-------|--------|
| 2026-08-17 | Antigravity | feat: implement elevation-on-demand default behavior and UI |
| 2026-08-17 | Antigravity | feat: add live trend history charts for CPU, Memory, GPU |
| 2026-08-17 | Antigravity | feat: wire live GPU sensor data to UI panel |
| 2026-08-17 | Antigravity | feat: wire live CPU and memory sensor data to UI panels |
| 2026-08-17 | Antigravity | docs: document admin elevation constraint for CPU sensor reads |
| 2026-08-17 | Antigravity | feat: initial project scaffold — .NET 8 WPF project, LibreHardwareMonitorLib smoke test (3 devices/96 sensors on Ryzen 7 5700X3D + RTX 3060), design system ResourceDictionary, placeholder UI shell with custom chrome |
