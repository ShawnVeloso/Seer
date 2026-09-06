# Seer — Claude Working Notes

Real-time PC hardware monitor. Single-process **WPF / .NET 8** desktop app
(`net8.0-windows`, C# nullable enabled), Windows-only, always-on, low overhead.

`AGENTS.md` is the binding rulebook for all agents. This file distills the parts
that apply every session so you don't have to re-read the long docs for routine
work. **Read the source docs only when the trigger below fires.**

| Doc | Read when |
|---|---|
| `INDEX.md` | Starting any task — skim **Current Focus** + **Log Entries** only (the File Manifest is mirrored below). Update it at task end (mandatory). |
| `AGENTS.md` | Git/branch/merge decisions, rollback incidents, or anything about process authority. Full ruleset. |
| `.agents/rules/seer_design_system.md` | Touching XAML, colors, typography, layout, or panel chrome. |
| `MILESTONE.md` | Picking what to build next / scoping a new feature. |
| `CHANGELOG.md` | Archaeology only. Never read to orient. |
| `graphify-out/GRAPH_REPORT.md` | Last resort for broad architecture questions — the map below is faster. |

Don't read `AGENTS.md` + `INDEX.md` + design system all up front "to be safe" —
that's ~8k tokens for what is usually a one-file change.

---

## Codebase map

Everything lives under `src/Seer/`. Flat by design — no deep nesting.

```
App.xaml(.cs)          entry point, merges Theme.xaml, ShutdownMode=OnMainWindowClose
MainWindow.xaml(.cs)   THE UI shell (~770 LOC .cs / 640 XAML) — custom chrome,
                       status strip, all panels, 1s DispatcherTimer, tray icon
OsdWindow.xaml(.cs)    desktop overlay; locked = Win32 click-through, unlocked = draggable
HudConfig.cs           static bool toggles for aesthetic effects (glow/brackets/grid/hover)
Controls/HudPanel.cs   panel container w/ corner brackets
Controls/TrendChart    120-sample rolling sparkline
Styles/Theme.xaml      ALL design tokens (SeerAccent, SeerWarning, PanelStyle, …)
Models/                immutable records only, no logic
Services/              all data acquisition, never touches UI
```

**Services → what they read**

| Service | Source |
|---|---|
| `HardwareMonitorService` | LibreHardwareMonitorLib `Computer` — CPU/GPU/Memory. Owns the shared `Computer` instance (exposed read-only). |
| `SystemInfoService` | WMI (`System.Management`) one-shot at startup + LHM for GPU name |
| `ProcessMonitorService` | `System.Diagnostics.Process`, CPU% via delta between polls |
| `DiskMonitorService` | `PerformanceCounter` on PhysicalDisk |
| `NetworkMonitorService` | `NetworkInterface` byte-counter deltas → Mbps |
| `SettingsService` | `%AppData%/Seer/settings.json`, `System.Text.Json` |
| `ThresholdEvaluator` | pure logic; single source of truth for NOMINAL/WARNING/CRITICAL |

`MainWindow.xaml.cs` is the only place polling and rendering meet:
`PollTimer_Tick` → `UpdatePanels()` → `UpdateCpuPanel()` / `UpdateMemoryPanel()` /
`UpdateGpuPanel()` / `UpdateDiskPanel()` / `UpdateNetworkPanel()` /
`UpdateTopProcessesPanel()` → `UpdateStatusBadge()`.

---

## Build & run

```bash
dotnet build src/Seer/Seer.csproj      # this is the verification you can actually do
dotnet run --project src/Seer/Seer.csproj
```

There is **no test suite**. "Tested" means built clean + the lead developer ran
it, or you state exactly what you observed. Never report a GUI/hardware behavior
as verified when you only compiled it.

---

## Conventions that are already in the code — follow them

- **Every sensor value is `float?`.** Null means unavailable (no sensor, or not
  elevated). Never coerce to 0 for display.
- **Null render pattern:** show `"--"` and switch the brush to `_warningBrush`
  (amber); the populated path uses `_normalBrush`. Brushes are cached from
  theme resources in the constructor — `FindResource("SeerText")`, don't
  re-resolve per tick.
- **Never hardcode a color/font in XAML.** Use a `Theme.xaml` key
  (`SeerAccent`, `SeerWarning`, `SeerDanger`, `SeerTextDim`, `PanelStyle`,
  `PanelHeaderStyle`…). Panels: `0px` radius, hairline `SeerBorder`, header
  numbered `[4] DISK`. Numeric readouts are monospace + tabular.
- **Services never touch `System.Windows`.** Models don't either (that's why
  `AppSettings.WindowState` is a `string`).
- **Settings failures are silent** — `SettingsService` returns defaults on any
  IO/JSON error. The app must never die over a config file. New settings are
  just new properties on `AppSettings`; nothing else changes.
- **Anything hardware-dependent degrades, never throws.** Wrap
  `PerformanceCounter` / WMI / process enumeration; `AccessDenied` is expected.
- **Adding a metric** = new `Models/XMetrics.cs` record + `Services/XService.cs`
  + an `UpdateXPanel()` called from `UpdatePanels()`. Keep that shape.
- **Prefer BCL over new packages.** Only 3 deps exist; adding one needs the
  lead developer's OK (AGENTS.md §3).

---

## Elevation — the recurring gotcha

Ships **non-elevated** (`app.manifest` = `asInvoker`); an in-app button relaunches
with `Verb="runas"`. Without admin, LibreHardwareMonitorLib can't reach Ring0, so
**CPU temperature, core clock, and package power come back null**. GPU and memory
work either way.

You cannot self-elevate (UAC needs interactive consent). So: you may write and
compile elevation-gated code and test the non-elevated path, but any report on
elevated behavior must say it was **not** verified elevated. That's the lead
developer's check before merge.

---

## Git

Full rules in AGENTS.md §5; the short version:

- Branch off freshly-pulled `main` (`feature/…`, `fix/…`). Never commit to `main`.
- One feature per branch, commits as `<type>: <description>`.
- `INDEX.md` (Current Focus + File Manifest + a Log Entries row) ships **in the
  same commit as the code**, written last. If Log Entries exceeds 10 rows, move
  the oldest verbatim to `CHANGELOG.md`.
- Push and print the PR link, then stop. **Do not open or merge the PR.**
- Anything `--force` / `reset --hard` / history-rewriting: stop and ask.
