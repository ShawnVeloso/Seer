# Seer

A real-time PC hardware monitor for Windows — a terminal-styled HUD that shows
what your machine is doing, at a glance, without getting in the way.

Single-process WPF app. No installer, no service, no telemetry, no account.

---

## What it shows

| Panel | Metrics |
|---|---|
| **CPU** | Temperature, load, clock, package power, and a per-core load breakdown |
| **Memory** | Used / total, load %, available |
| **GPU** | Temperature, hot spot, load, clock, fan speed, VRAM |
| **Disk I/O** | Read / write throughput |
| **Network I/O** | Up / down Mbps |
| **Ping** | Latency, average, jitter and packet loss to a host you choose — started and stopped by hand |
| **Top processes** | The heaviest processes by CPU, with memory use |
| **Trend charts** | 120-second rolling history for CPU, memory and GPU load |
| **Alerts** | A session log of every threshold crossing |
| **System info** | Motherboard, BIOS, CPU, RAM and GPU — fetched once at startup |

A status strip across the top reads `NOMINAL`, `WARNING` or `CRITICAL` so you
can judge system health from across the room. Chosen values can also be shown
outside the window: as numbers drawn into taskbar tray icons, or in a desktop
overlay that floats over other windows including fullscreen games. Both are
toggled from the tray menu, and which metrics appear is up to you.

Alert thresholds are editable in Settings, reachable from the title bar or the
tray. Nothing is sent anywhere — the only component that touches the network is
the ping panel, and it stays off until you start it.

---

## Running it

Download `Seer.exe` and double-click it. Windows 10 or 11, 64-bit — nothing
else to install.

**Windows will warn you on first run.** The build isn't code-signed, so
SmartScreen shows "Windows protected your PC". Click **More info → Run
anyway**.

### Some readings show `--` in amber

That's expected. CPU temperature, clock speed and package power are read
through kernel-level drivers that require administrator rights. Click
**ELEVATE** in the app to restart with them — you'll get a UAC prompt.

Everything else — CPU load, memory, GPU, disk, network, processes — works
without elevation. Seer deliberately starts unprivileged and only asks when
you want the gated metrics.

### It lives in the tray

Closing the window doesn't quit; Seer minimises to the system tray. Right-click
the cyan eye for *Show Seer*, the overlay toggles, and *Exit*.

Window geometry and settings are stored in `%AppData%\Seer\settings.json`.
Crash reports, if any, land in `%AppData%\Seer\logs`.

---

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build src/Seer/Seer.csproj
dotnet run --project src/Seer/Seer.csproj

# Distributable build — one self-contained .dist/Seer.exe
dotnet publish src/Seer/Seer.csproj -p:PublishProfile=TesterBuild
```

Hardware access comes from
[LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor);
disk, network, process and system-info readings use the .NET BCL and WMI
directly. Dependencies are kept deliberately few — this is a background tool,
so its own overhead matters.

---

## Project documentation

| Document | Contents |
|---|---|
| [`INDEX.md`](INDEX.md) | Current state, file manifest, recent log |
| [`AGENTS.md`](AGENTS.md) | Rules for AI agents working in this repo |
| [`CLAUDE.md`](CLAUDE.md) | Condensed map and conventions for Claude |
| [`MILESTONE.md`](MILESTONE.md) | Feature backlog, ranked by effort |
| [`RELEASE.md`](RELEASE.md) | Cutting a build for testers |
| [`CHANGELOG.md`](CHANGELOG.md) | Full historical log |

---

## License

MIT — see [LICENSE](LICENSE).
