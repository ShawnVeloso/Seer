# Seer — Feature Goals & Milestones

> Backlog of planned features, ranked by relative implementation effort.
> Not a commitment or a sprint plan — a shared reference for picking
> what's next. Update as items are built (move to "Shipped") or
> reprioritized. See INDEX.md for what's actually in progress right now.

---

## Tier 1 — Low effort (extends existing data/systems)

- [x] **Per-core CPU load breakdown** — design doc already specs the format
  (`0[||||   16.7%]`). LibreHardwareMonitorLib's CPU sub-hardware already
  exposes per-core sensors (seen in the original smoke test). Mostly a
  UI/layout task, not a new data pipeline.
- [x] **Static system-info panel** — motherboard model, BIOS version, RAM
  speed/timings. Read-once, no polling loop needed, simplest kind of
  panel to build.

## Tier 2 — Medium effort (new logic, same data source)

- [x] **Threshold alerts** — shipped as `ThresholdEvaluator` plus a
  session-only Alert Log panel.
- [x] **Top processes by CPU/RAM** — shipped via `System.Diagnostics.Process`
  enumeration with an access-denied fallback.
- [x] **Disk I/O (read/write speed)** — shipped, but *not* via
  LibreHardwareMonitorLib's Storage type as speculated here: it uses
  `System.Diagnostics.PerformanceCounter` against PhysicalDisk instead.

## Tier 2b — Agreed, not yet started

- [x] **Split `MainWindow.xaml.cs`** — 770+ lines carrying window chrome,
  the poll loop, six panel updaters, tray lifecycle, OSD and settings.
  Violates AGENTS.md §4. Blocks the two items below, which would
  otherwise add more code to it.
- [x] **Editable thresholds** — `AppSettings` already persists warning and
  critical values for load and temp, but nothing in the UI reaches them;
  they can only be changed by hand-editing settings.json.
- [x] **Start with Windows** — standard expectation for an always-on tray
  tool.
- [x] **Configurable readouts** — temps and other metrics shown as taskbar
  tray icons (MSI Afterburner style) and as a Seer-styled overlay strip,
  each an optional toggle in the tray right-click menu, with a choice of
  which metrics appear.
- [x] **Unit tests for `ThresholdEvaluator`** — pure logic, no WPF or
  hardware; currently the severity/escalation rules can only be checked
  by heating the machine up. Worth having before thresholds become
  user-editable.

## Known issues

- [x] **Intermittent process death after a few minutes** — most likely
  explained, not a defect. It was observed only while a second Seer
  instance was running alongside the test instance, and the lead
  developer confirmed afterwards having left one running and then closed
  both; closing a window or using a tray Exit with several instances up
  shuts down whichever one the click landed on. That fits every symptom:
  a clean exit, no crash log, no Windows Application Error event, and no
  reproducibility. A single-instance run afterwards stayed up for ten
  minutes with flat GDI handles and steady memory. Reopen this if it is
  ever seen with exactly one instance running.

## Tier 3 — Medium-high effort (new domain: networking)

- [x] **Ping/latency check with start/stop control** — shipped as
  `PingMonitorService`: a background loop with its own cadence publishing
  an immutable snapshot the UI polls, rather than awaiting a ping on the
  dispatcher. Reports latency, average, jitter and packet loss.
- [x] **Network throughput (up/down Mbps)** — shipped; `NetworkInterface`
  byte counters sampled between polls, as anticipated.

## Tier 4 — Higher effort / hardware-dependent (may not be reliably available)

- [ ] **Fan speeds beyond GPU** (case fans, CPU fan) — depends on your
  specific motherboard's SuperIO chip being supported by
  LibreHardwareMonitorLib. Hit-or-miss per machine; needs an early
  feasibility check (smoke test) before committing to full UI work.
- [ ] **Motherboard/VRM temps** — same hardware-support caveat as above.
- [ ] **Disk health/SMART data** (temp, wear level) — typically needs deeper
  low-level access than basic sensor reads; may hit elevation
  requirements similar to CPU thermals, worth an early spike to confirm
  scope before treating as a normal task.

---

## Shipped
- [x] Real NOMINAL/CRITICAL status badge logic (with WARNING state)
- [x] CPU / Memory / GPU live panels
- [x] Elevation-aware graceful degradation (amber `--` fallback)
- [x] Trend history charts (120s rolling, CPU/GPU/Memory)
- [x] Elevation-on-demand (default non-elevated, in-app admin relaunch)
- [x] HUD polish pass (glow, corner brackets, scan-lines, hover glow)
- [x] Per-core CPU load breakdown
- [x] Static system-info panel (WMI + LHM, collapsible, one-shot fetch)
- [x] Settings persistence (window geometry, `%AppData%/Seer/settings.json`)
- [x] Threshold alerts (`ThresholdEvaluator` + session Alert Log panel)
- [x] Desktop OSD overlay (draggable / click-through, tray lifecycle)
- [x] Top processes by CPU / RAM
- [x] Disk I/O throughput (PerformanceCounter)
- [x] Network throughput up / down (NetworkInterface)
- [x] Tester packaging (single-file self-contained build, icon, versioning,
  crash logs to `%AppData%`)
- [x] Settings window (editable thresholds, start with Windows)
- [x] Configurable readouts (taskbar tray icons + OSD strip)
- [x] Ping / latency panel with start-stop control
- [x] HUD restyle, direction C — inset panel titles, chamfered corners, dot
  lattice, drawn log-scale meters, per-core matrix with heat and peak-hold.
  Supersedes the earlier "HUD polish pass" line above
- [x] Living details — heartbeat, chart write-head, peak holds, severity edges,
  activity LEDs, rank marks, status line, time graticule, per-core heat, ping
  tape, launch report; plus trend arrows, session I/O totals, per-chart
  min/avg/max, relative event times, the session event log and inline meters.
  Each behind its own `HudConfig` flag