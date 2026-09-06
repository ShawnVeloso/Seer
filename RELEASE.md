# Seer — Building & Shipping a Tester Build

Everything needed to hand Seer to a Windows tester, and what to tell them.

---

## Cutting a build

```bash
dotnet publish src/Seer/Seer.csproj -p:PublishProfile=TesterBuild
```

Output: **`.dist/Seer.exe`** — one self-contained file, ~70 MB, no installer.
Settings live in `src/Seer/Properties/PublishProfiles/TesterBuild.pubxml`.

Before cutting a build, bump `<Version>` and `<InformationalVersion>` in
`src/Seer/Seer.csproj`. The informational version is what shows in the title
bar and in every crash report — if two testers are on different builds and
neither can name which, their reports aren't comparable.

Self-contained is deliberate: testers don't need the .NET 8 Desktop Runtime,
because "install a runtime first" gets reported as "it doesn't work."
`PublishTrimmed` is not an option — WPF doesn't support trimming.

---

## Requirements on the tester's machine

- Windows 10 or 11, 64-bit. That's it — no runtime, no install.

## What to send testers

> **Seer v0.1.0-alpha**
>
> Download `Seer.exe` and double-click it. No installer, nothing to set up.
>
> **Windows will warn you.** The build isn't code-signed, so SmartScreen
> shows "Windows protected your PC." Click **More info → Run anyway**. If
> your browser blocks the download, choose **Keep**.
>
> **Some readings show `--` in amber.** That's expected when running
> normally: CPU temperature, clock speed and package power need
> administrator rights to read. Click **ELEVATE** in the app to restart
> with them; you'll get a UAC prompt. Everything else (CPU load, memory,
> GPU, disk, network, processes) works without it.
>
> **Closing the window doesn't quit.** Seer minimises to the system tray —
> look for the cyan eye. Right-click it for *Show Seer*, the desktop
> overlay (OSD) toggles, and *Exit*.
>
> **If it crashes**, send the newest file from:
> `%AppData%\Seer\logs` — paste that into Explorer's address bar.
> Include what you were doing. The report already records your build
> number, OS and whether you were elevated.
>
> **To remove it:** exit from the tray, delete `Seer.exe`, and delete
> the `%AppData%\Seer` folder.

---

## Known friction

| Issue | Status |
|---|---|
| SmartScreen warning on first run | Expected — unsigned. A code-signing certificate is the only real fix; the warning fades as a signed build builds reputation. Not worth buying for an alpha. |
| ~70 MB download | Cost of self-contained. Framework-dependent would be ~2 MB but requires a runtime install. |
| First launch is slow (~1–2 s) | The single-file bundle decompresses to a temp folder on first run. Subsequent launches are cached. |
| Elevated readings unverifiable by agents | Agents can't accept a UAC prompt — see AGENTS.md §3a. Elevated behavior is checked by the lead developer. |

---

## Crash reports

Written by `src/Seer/Services/CrashLogService.cs` to
`%AppData%\Seer\logs\crash-<timestamp>.log`, newest 20 kept.

Each report stamps the build version, OS, bitness and elevation state
before the stack trace, so a pasted log is self-describing. The path is
absolute by design — a relative path lands in the working directory,
which for a UAC relaunch can be `C:\Windows\System32`.
