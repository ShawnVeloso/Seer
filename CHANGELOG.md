# [Project Name TBD] — Changelog Archive

> Full historical log. INDEX.md's "Log Entries" table only keeps the most
> recent 10 entries for agent context efficiency — once it exceeds that,
> the oldest entries move here verbatim (not summarized).
>
> Newest archived entries go at the top.

---

| Date | Agent | Action |
|------|-------|--------|
| 2026-08-19 | Antigravity | feat: Desktop OSD Integration — interactive (draggable) and locked (click-through) modes, AppSettings binding, and system tray lifecycle integration |
| 2026-08-19 | Antigravity | fix: link OSD window to MainWindow lifecycle and wire live stats to update on polling timer |
| 2026-08-19 | Antigravity | fix: set ShutdownMode to OnMainWindowClose so hidden OSD window doesn't keep app alive |
| 2026-08-19 | Antigravity | feat: OSD feasibility spike — added transparent topmost window with Win32 click-through |
| 2026-08-18 | Antigravity | feat: threshold alerts — extracted ThresholdEvaluator, added session-only Alert Log UI panel with state-change logging |
| 2026-08-18 | Antigravity | feat: settings persistence — window geometry saved/restored via AppSettings + SettingsService; off-screen and corruption fallbacks |
| 2026-08-18 | Antigravity | fix: use SingleBorderWindow and 8px padding trigger to prevent maximized window taskbar overlap |
| 2026-08-18 | Antigravity | feat: add static system-info panel (WMI + LHM, collapsible, one-shot fetch at startup) |
| 2026-08-17 | Antigravity | feat: wire per-core CPU load breakdown UI using LibreHardwareMonitorLib |
| 2026-08-17 | Antigravity | feat: wire real NOMINAL/WARNING/CRITICAL logic to status badge based on hardware metrics |
| 2026-08-17 | Antigravity | feat: add HUD aesthetic polish (chart glow, brackets, grid, hover states) |
| 2026-08-17 | Antigravity | feat: implement elevation-on-demand default behavior and UI |
| 2026-08-17 | Antigravity | feat: add live trend history charts for CPU, Memory, GPU |
| 2026-08-17 | Antigravity | feat: wire live GPU sensor data to UI panel |
| 2026-08-17 | Antigravity | feat: wire live CPU and memory sensor data to UI panels |
| 2026-08-17 | Antigravity | docs: document admin elevation constraint for CPU sensor reads |
| 2026-08-17 | Antigravity | feat: initial project scaffold — .NET 8 WPF project, LibreHardwareMonitorLib smoke test (3 devices/96 sensors on Ryzen 7 5700X3D + RTX 3060), design system ResourceDictionary, placeholder UI shell with custom chrome |
