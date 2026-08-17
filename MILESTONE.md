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
- [ ] **Static system-info panel** — motherboard model, BIOS version, RAM
  speed/timings. Read-once, no polling loop needed, simplest kind of
  panel to build.

## Tier 2 — Medium effort (new logic, same data source)

- [ ] **Threshold alerts** — builds directly on the NOMINAL/CRITICAL work
  above; adds a lightweight alert/log of when a metric crossed a
  threshold. Depends on Tier 1's badge logic existing first.
- [ ] **Top processes by CPU/RAM** — new data source (`System.Diagnostics.
  Process` enumeration, not LibreHardwareMonitorLib), but conceptually
  simple: poll, sort, display top N. No new external dependencies.
- [ ] **Disk I/O (read/write speed)** — LibreHardwareMonitorLib may already
  expose this via its Storage hardware type (needs confirming against a
  fresh smoke test) — if so, closer to Tier 1; if not, bump to Tier 3.

## Tier 3 — Medium-high effort (new domain: networking)

- [ ] **Ping/latency check with start/stop control** — genuinely new domain
  (`System.Net.NetworkInformation.Ping`), needs async task management and
  a start/stop UI state — no prior branch has built a user-toggleable
  background process before.
- [ ] **Network throughput (up/down Mbps)** — needs NIC byte counters sampled
  over time (delta between polls), a new sensor-reading pattern distinct
  from LibreHardwareMonitorLib's snapshot-style values.

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