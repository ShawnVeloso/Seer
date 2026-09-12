# Seer — Agent Log Index

> **Purpose:** Persistent state-tracking for AI agents and the lead developer.
> **Last Updated:** 2026-09-06 (+08:00)

---

## Current Focus
- **Working on:** HUD restyle (direction C) + living details — complete on
  `feature/hud-restyle`. Built clean, 79 tests pass, verified by off-screen
  render at three window sizes. Motion, glow weight and everything
  elevation-gated still need a real run by the lead developer.
- **Next up:** Tier 4 only — fan speeds beyond GPU, motherboard/VRM temps,
  disk SMART. Each is hardware-dependent and needs a feasibility smoke test
  before any UI work is committed to. One known follow-up: `OsdWindow` uses
  no theme key at all (hardcoded `#4dd8ff`), so it did not follow the retheme
  and now diverges from the main window.
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
| HUD Polish | ✅ Complete | Chart glow, panel brackets, background grid, hover states |
| Status Badge Logic | ✅ Complete | Dynamic NOMINAL/WARNING/CRITICAL badge based on metrics |
| Per-core CPU load breakdown | ✅ Complete | Added dense inline bar format, neatly packed in a UniformGrid inside the CPU Panel |
| Static system-info panel | ✅ Complete | Collapsible panel — mobo, BIOS, CPU, RAM, GPU via WMI + LHM; fetched once at startup |
| Settings persistence | ✅ Complete | Window geometry saved/restored via `%AppData%/Seer/settings.json`; off-screen and corruption fallbacks |
| Threshold alerts | ✅ Complete | Extracted `ThresholdEvaluator` as single source of truth; added session-only Alert Log UI panel |
| Desktop OSD Overlay | ✅ Complete | Interactive (draggable) and Locked (click-through) modes; system tray lifecycle integration |
| Top Processes Panel | ✅ Complete | Uses `System.Diagnostics.Process` with graceful admin/access denied fallback |
| Tester packaging | ✅ Complete | Single-file self-contained `.dist/Seer.exe` (~64 MB), icon, versioning, crash logs to `%AppData%` |
| Settings window | ✅ Complete | Editable load/temp thresholds + start-with-Windows; validated, applies without restart |
| Configurable readouts | ✅ Complete | Metrics as taskbar tray icons and in the OSD strip, chosen per surface; severity-coloured |
| Ping / latency panel | ✅ Complete | Start/stop control; latency, average, jitter, packet loss. Only component that sends traffic |
| Scrollable panel layout | ✅ Complete | Panel rows size to content inside a styled `ScrollViewer`; per-core bars reflow via `WrapPanel` |
| Unit tests | ✅ Started | `tests/Seer.Tests` — 79 tests over `ThresholdEvaluator`, `ReadoutFormatter`, `PingMonitor` and the living-detail maths |
| HUD restyle (direction C) | ✅ Complete | Inset panel titles, chamfered corners, dot lattice, drawn log-scale meters, per-core matrix, halo glow |
| Living details | ✅ Complete | Heartbeat, write-head, peak holds, severity edges, activity LEDs, rank marks, status line, graticule, core heat, ping tape, launch report; each behind a `HudConfig` flag |
| Off-screen render harness | ✅ Complete | `--render-shot` renders the window to PNG without a desktop session; proves every `StaticResource` resolves |

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
| `src/Seer/MainWindow.xaml` | Main UI layout — custom chrome title bar, status strip, all panels |
| `src/Seer/MainWindow.xaml.cs` | Window lifecycle + orchestration — services, poll timer, geometry, tray and OSD ownership |
| `src/Seer/MainWindow.Panels.cs` | Partial class holding all panel rendering (`Update*Panel`, status badge, history buffers) |
| `src/Seer/MainWindow.Living.cs` | Partial class holding the parts that move — heartbeat, status line, activity LEDs, trend arrows |
| `src/Seer/RenderShot.cs` | `--render-shot`: renders the window off-screen to PNG and exits. The only way to check the UI without a desktop session |
| `src/Seer/SettingsWindow.xaml(.cs)` | Settings dialog — alert thresholds and start-with-Windows, with validation |
| `tests/Seer.Tests/` | xUnit project; `ThresholdEvaluatorTests` covers severity boundaries, escalation and missing sensors |
| `src/Seer/OsdWindow.xaml` / `src/Seer/OsdWindow.xaml.cs` | Desktop OSD overlay with locked (click-through) and unlocked (draggable) modes |
| `.gitignore` | Standard .NET gitignore (bin/, obj/, .vs/, etc.) |
| `.agents/rules/seer_design_system.md` | Front-end design reference (colors, typography, layout rules) |
| `src/Seer/Assets/seer.ico` | App + tray icon (multi-size); regenerate with `tools/make-icon.ps1` |
| `src/Seer/Properties/PublishProfiles/TesterBuild.pubxml` | Publish profile for the tester build (single-file, self-contained, → `.dist/`) |
| `tools/make-icon.ps1` | Generates `seer.ico` from the design system palette |
| `AGENTS.md` | Agent rulebook (all project rules in one place) |
| `README.md` | Project front page — what Seer shows, how to run it, how to build it |
| `RELEASE.md` | How to cut a tester build, what to send testers, known friction |
| `CLAUDE.md` | Claude-specific working notes — distilled map/conventions + when to read the longer docs |
| `INDEX.md` | This file — project state, file manifest, log entries |
| `CHANGELOG.md` | Archive for INDEX.md log entries once they exceed 10 |
| `graphify-out/graph.json` | Structural codebase map generated by Graphify for rapid context mapping |

### Styles

| File | Purpose |
|------|---------|
| `src/Seer/Styles/Theme.xaml` | WPF ResourceDictionary — all design system tokens (colors, brushes, typography, panel/button styles) |

### Controls

| File | Purpose |
|------|---------|
| `src/Seer/Controls/TrayIconController.cs` | Owns the tray icon and context menu; raises events, holds no app state |
| `src/Seer/Controls/TrayMetricIcons.cs` | Draws metric values as taskbar tray icons; owns the HICON lifetime |
| `src/Seer/Controls/HudBackground.cs` | Builds the 40px HUD grid brush |
| `src/Seer/Controls/HudPanel.cs` | Panel container with corner brackets |
| `src/Seer/Controls/TrendChart.xaml(.cs)` | 120-sample rolling sparkline, halo stroke, write-head, time graticule, session peak line |
| `src/Seer/Controls/ChamferShape.cs` | The 45° cut corner, as one closed geometry so fill and outline agree |
| `src/Seer/Controls/SegmentMeter.cs` | Every bar in the app: linear or log scale, threshold ticks, over-threshold colour, peak mark |
| `src/Seer/Controls/CoreMatrix.cs` | Per-core block; computes its own column count in `MeasureOverride`, draws heat and peak ticks |
| `src/Seer/Controls/PingTape.cs` | One bar per ping reply, red hairline for a loss |
| `src/Seer/Controls/Pulse.cs` | The single gate every animation passes through; honours the Windows animation setting |

### Models

| File | Purpose |
|------|---------|
| `src/Seer/Models/SensorSnapshot.cs` | Typed records for sensor readings (`CpuMetrics`, `MemoryMetrics`, `GpuMetrics`), handling nullable/elevation-gated values |
| `src/Seer/Models/SystemInfo.cs` | Immutable record for static system hardware info (mobo, BIOS, CPU, RAM, GPU) |
| `src/Seer/Models/AppSettings.cs` | Flat POCO for persisted settings (window geometry; extensible for future OSD/threshold settings) |
| `src/Seer/Models/AlertEvent.cs` | Record defining a single alert log entry (`AlertSeverity`, Timestamp, Value, etc.) |
| `src/Seer/Models/ProcessMetrics.cs` | Lightweight record for process ID, Name, CPU %, and RAM (MB) |
| `src/Seer/Models/DiskMetrics.cs` | Lightweight record for disk read/write throughput |
| `src/Seer/Models/NetworkMetrics.cs` | Lightweight record for network up/down Mbps throughput |
| `src/Seer/Models/ReadoutMetric.cs` | Enum of values that can be shown in the tray or OSD |
| `src/Seer/Models/PingSnapshot.cs` | Immutable view of ping statistics handed from the background loop to the UI, plus the recent replies for the tape |
| `src/Seer/Models/ProcessSnapshot.cs` | Top processes plus the totals behind them (process and thread counts) |
| `src/Seer/Models/SeverityBreakdown.cs` | Per-metric severities, so a panel can colour its own edge |
| `src/Seer/Models/EventEntry.cs` | One line in the session event log — threshold crossings and app events alike |

### Services

| File | Feature | Purpose |
|------|---------|---------|
| `src/Seer/Services/HardwareMonitorService.cs` | Sensor access | Wraps `Computer`; `GetCpuMetrics()`, `GetMemoryMetrics()`, `GetGpuMetrics()`, and smoke test |
| `src/Seer/Services/SystemInfoService.cs` | Static info | One-shot WMI queries for mobo/BIOS/CPU/RAM; reads GPU name from LHM |
| `src/Seer/Services/SettingsService.cs` | Settings persistence | Load/Save `AppSettings` to `%AppData%/Seer/settings.json` via `System.Text.Json`; silent fallback on any failure |
| `src/Seer/Services/ThresholdEvaluator.cs` | Alert generation | Evaluates sensor data against thresholds and detects escalations to WARNING/CRITICAL state |
| `src/Seer/Services/ProcessMonitorService.cs` | Process tracking | Iterates processes to calculate CPU % over time and read RAM; handles AccessDenied gracefully |
| `src/Seer/Services/DiskMonitorService.cs` | Disk I/O | Uses `PerformanceCounter` to track physical disk read/write throughput |
| `src/Seer/Services/NetworkMonitorService.cs` | Network I/O | Calculates active Mbps throughput (up/down) via `NetworkInterface` |
| `src/Seer/Services/CrashLogService.cs` | Diagnostics | Writes unhandled exceptions to `%AppData%/Seer/logs`; also hosts `AppVersion` (build identity for UI + reports) |
| `src/Seer/Services/ElevationService.cs` | Elevation | `IsElevated` and `TryRelaunchElevated()` (UAC relaunch); single source of truth for admin state |
| `src/Seer/Services/WindowPlacement.cs` | Geometry | `IsOnScreen()` — stops restoring the window onto a disconnected monitor |
| `src/Seer/Services/StartupService.cs` | Launch at login | HKCU Run key add/remove; deliberately not HKLM, which would auto-start elevated |
| `src/Seer/Services/ReadoutFormatter.cs` | Readouts | Formats one metric into label/value/severity; shared by the tray icons and the OSD so they can't disagree |
| `src/Seer/Services/PingMonitorService.cs` | Latency | Background ping loop with start/stop; publishes a `PingSnapshot` the UI polls. Never runs unasked |
| `src/Seer/Services/PeakHold.cs` | Living details | Holds a short-term maximum, then lets it fall — the audio-meter rule |
| `src/Seer/Services/SeverityHold.cs` | Living details | Keeps a severity on screen for a few polls after it clears, so a reading on its threshold does not flicker |
| `src/Seer/Services/SlopeTracker.cs` | Living details | Direction of travel over a short window, with a deadband so noise is not a trend |
| `src/Seer/Services/SessionStats.cs` | Living details | Running low/mean/high that outlive the 120-second plot |
| `src/Seer/Services/RateIntegrator.cs` | Living details | Estimates a total from rate samples (disk has no cumulative counter) — always rendered with `~` |
| `src/Seer/Services/PollClock.cs` | Living details | Times the poll and detects a late one |
| `src/Seer/Services/SessionLog.cs` | Events | Threshold crossings and app events in one session log |
| `src/Seer/Services/ProcessRankTracker.cs` | Events | Marks processes entering or climbing the top list, keyed by pid and backed by a real CPU change |
| `src/Seer/Services/LaunchReport.cs` | Diagnostics | What the app could reach at startup: sensor counts and Ring0 availability |

---

## Architecture Summary

```
Single-process WPF app (requires admin elevation for sensor access).

  src/Seer/App.xaml.cs          → startup, crash handlers
  src/Seer/MainWindow.xaml      → UI layout (custom chrome, panels)
  src/Seer/MainWindow.xaml.cs   → lifecycle + orchestration
  src/Seer/MainWindow.Panels.cs → rendering (partial class)
  src/Seer/Controls/            → reusable UI pieces + tray controller
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

# Test
dotnet test tests/Seer.Tests/Seer.Tests.csproj

# Tester build -> .dist/Seer.exe
dotnet publish src/Seer/Seer.csproj -p:PublishProfile=TesterBuild
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
| 2026-09-12 | Claude | feat: HUD restyle (direction C) — titles inset into panel borders, chamfered corners, drawn log-scale meters, per-core matrix with heat and peak-hold, and eleven data-driven living details behind HudConfig flags |
| 2026-09-06 | Claude | fix: panel layout overflow — scrollable panel area with content-sized rows, per-core bars reflow instead of overlapping, ping moved to third-from-last, softer background grid |
| 2026-09-06 | Claude | feat: ping/latency panel with start-stop control — background loop, latency/average/jitter/loss; closes the last Tier 3 item |
| 2026-09-06 | Claude | feat: configurable readouts — metrics as taskbar tray icons (Afterburner style) and a metric-driven OSD strip, both toggled from the tray menu |
| 2026-09-06 | Claude | feat: settings window — editable alert thresholds and start-with-Windows, reachable from the title bar and tray; adds Seer.Tests with 19 ThresholdEvaluator tests |
| 2026-09-06 | Claude | refactor: split MainWindow.xaml.cs (776→321 lines) — rendering to MainWindow.Panels.cs; tray, background grid, elevation and window placement to real classes |
| 2026-09-06 | Claude | docs: add README.md; correct MILESTONE.md (Tier 2 + network throughput were shipped but unchecked) and log the agreed backlog |
| 2026-09-06 | Claude | feat: tester packaging — single-file self-contained publish profile, app/tray icon, build version in title bar + crash reports, %AppData% crash logging, RELEASE.md |
| 2026-09-06 | Claude | docs: add CLAUDE.md working notes (distilled map/conventions + when to read the longer docs) |
| 2026-08-19 | Antigravity | feat: implement network throughput (up/down Mbps) polling using NetworkInterface |
