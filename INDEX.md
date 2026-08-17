# Seer — Agent Log Index

> **Purpose:** Persistent state-tracking for AI agents and the lead developer.
> **Last Updated:** 2026-08-17T16:14 (+08:00)

---

## Current Focus
- **Working on:** Nothing (scaffold complete, awaiting PR review)
- **Next up:** Wire live sensor data into the placeholder panels
- **Then:** Status strip with real CPU/MEM/GPU percentage bars
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

---

## File Manifest

> The single most valuable section for an agent — links feature concepts directly
> to absolute file paths, so no directory crawling is needed for known work.
> Keep this current. If ARCHITECTURE.md is added later, it explains *why*;
> this table stays the map of *where*.

### Project Root

| File | Purpose |
|------|---------|
| `Seer.csproj` | .NET 8 WPF project file; NuGet ref to LibreHardwareMonitorLib |
| `app.manifest` | Requests admin elevation for hardware sensor access |
| `App.xaml` / `App.xaml.cs` | WPF application entry point; merges Theme.xaml, runs sensor smoke test |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | Main UI shell — custom chrome title bar, status strip, placeholder panels |
| `.gitignore` | Standard .NET gitignore (bin/, obj/, .vs/, etc.) |
| `seer_design_system.md` | Front-end design reference (colors, typography, layout rules) |
| `AGENTS.md` | Agent rulebook (all project rules in one place) |
| `INDEX.md` | This file — project state, file manifest, log entries |
| `CHANGELOG.md` | Archive for INDEX.md log entries once they exceed 10 |

### Styles

| File | Purpose |
|------|---------|
| `Styles/Theme.xaml` | WPF ResourceDictionary — all design system tokens (colors, brushes, typography, panel/button styles) |

### Services

| File | Feature | Purpose |
|------|---------|---------|
| `Services/HardwareMonitorService.cs` | Sensor access | Wraps LibreHardwareMonitorLib `Computer`; `RunSmokeTest()` enumerates hardware/sensors |

---

## Architecture Summary

```
Single-process WPF app (requires admin elevation for sensor access).

  App.xaml.cs          → startup, smoke test trigger
  MainWindow.xaml      → UI shell (custom chrome, placeholder panels)
  Styles/Theme.xaml    → design tokens (colors, typography, styles)
  Services/            → sensor logic (separated from UI per AGENTS.md §4)
```

> A full ARCHITECTURE.md is intentionally NOT created at project start — see
> AGENTS.md for the rationale. Once the backend/frontend split solidifies,
> spin up a lightweight ARCHITECTURE.md to record the *why* behind that
> decision, not just restate this summary.

---

## Key Configuration

| Variable | Source | Value |
|----------|--------|-------|
| Admin elevation | `app.manifest` | `requireAdministrator` — needed for LibreHardwareMonitorLib kernel drivers |
| Target framework | `Seer.csproj` | `net8.0-windows` |
| LibreHardwareMonitorLib | NuGet | `0.9.4` |

---

## Running the App

```bash
# Build
dotnet build

# Run (requires admin elevation — will trigger UAC prompt)
dotnet run
# or launch bin/Debug/net8.0-windows/Seer.exe as Administrator
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
| 2026-08-17 | Antigravity | feat: initial project scaffold — .NET 8 WPF project, LibreHardwareMonitorLib smoke test (3 devices/96 sensors on Ryzen 7 5700X3D + RTX 3060), design system ResourceDictionary, placeholder UI shell with custom chrome |
