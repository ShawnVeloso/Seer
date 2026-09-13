# Seer — Building & Shipping a Tester Build

Everything needed to hand Seer to a Windows tester, and what to tell them.

---

## Cutting a build

```bash
dotnet publish src/Seer/Seer.csproj -p:PublishProfile=TesterBuild
```

Output: **`.dist/Seer.exe`** — one self-contained file, ~64 MB, no installer.
Settings live in `src/Seer/Properties/PublishProfiles/TesterBuild.pubxml`.

Before cutting a build, bump `<Version>` and `<InformationalVersion>` in
`src/Seer/Seer.csproj`. The informational version is what shows in the title
bar and in every crash report — if two testers are on different builds and
neither can name which, their reports aren't comparable.

Self-contained is deliberate: testers don't need the .NET 8 Desktop Runtime,
because "install a runtime first" gets reported as "it doesn't work."
`PublishTrimmed` is not an option — WPF doesn't support trimming.

---

## Publishing a release

Testers download from a GitHub pre-release, so there is one stable link and
every build handed out is on record.

1. Cut the build (above) from the merged `main` commit, and launch
   `.dist/Seer.exe` once non-elevated: window opens, tray icon appears, no
   new file in `%AppData%\Seer\logs`.
2. Hash it, so a tester can confirm the download:
   `certutil -hashfile .dist\Seer.exe SHA256`
3. Tag and publish — the notes are the tester message below plus the hash:

   ```bash
   git tag v0.1.0-alpha && git push origin v0.1.0-alpha
   gh release create v0.1.0-alpha .dist/Seer.exe --prerelease \
     --title "Seer v0.1.0-alpha" --notes-file <notes.md>
   ```

4. Send the tester the release page link, not the raw exe — chat apps and
   mail often block a 64 MB unsigned executable.

A new build for testers means a new version string and a new tag; never
replace the exe on an existing release, or two testers on "the same" build
aren't.

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
> administrator rights to read. Click **RUN AS ADMIN** in the app to restart
> with them; you'll get a UAC prompt. Everything else (CPU load, memory,
> GPU, disk, network, processes) works without it. Disk health shows each
> drive's OK / warning verdict either way; the detail behind it
> (temperature, wear, lifetime writes) also needs RUN AS ADMIN.
>
> **Closing the window doesn't quit.** Seer minimises to the system tray —
> look for the cyan eye. Right-click it for *Show Seer*, the desktop
> overlay (OSD) toggles, and *Exit*.
>
> **Temperatures in the taskbar.** Seer can draw readings as numbers next
> to the clock (pick which in *Settings → Readouts → Taskbar*). Windows
> hides new tray icons behind the **^** arrow at first. To keep them on the
> taskbar, open Windows *Settings → Personalization → Taskbar → Other
> system tray icons* and switch the Seer entries on — or drag them out of
> the ^ flyout onto the taskbar. You only need to do this once.
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
| ~64 MB download | Cost of self-contained. Framework-dependent would be ~2 MB but requires a runtime install. |
| ~80 MB more private memory than a plain build | Measured v0.1.0-alpha: ~247 MB private for `.dist/Seer.exe` vs ~167 MB for the Debug build, after 30 s. Likely `EnableCompressionInSingleFile` holding decompressed assemblies in memory — not yet confirmed. Worth a measured trade-off (bigger download vs lower footprint) before a wider release. |
| Tray readouts hidden behind ^ by default | Windows 11 gives apps no supported way to pin their own tray icons. The icons keep a stable identity, so the tester's one-time pin or arrangement sticks. |
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
