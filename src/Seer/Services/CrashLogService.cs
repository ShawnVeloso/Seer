using System;
using System.IO;
using System.Reflection;

namespace Seer.Services;

/// <summary>
/// Writes unhandled-exception reports to a known, always-writable location.
///
/// Deliberately does NOT use a relative path: the working directory of a
/// published build depends on how it was launched (Explorer, a shortcut, or
/// the UAC relaunch, which can hand us C:\Windows\System32). A relative
/// write can land somewhere unfindable or throw inside the crash handler
/// itself, losing the report — the one thing that must survive a crash.
/// </summary>
public static class CrashLogService
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Seer", "logs");

    /// <summary>Folder shown to testers when asking them to send a crash report.</summary>
    public static string LogDirectory => LogDir;

    /// <summary>
    /// Writes a timestamped crash report. Never throws — a failure here
    /// would replace a useful stack trace with a useless one.
    /// </summary>
    public static void Write(string source, object? error)
    {
        try
        {
            Directory.CreateDirectory(LogDir);

            var path = Path.Combine(
                LogDir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            var report =
                $"Seer {AppVersion.Full}{Environment.NewLine}" +
                $"Timestamp : {DateTime.Now:O}{Environment.NewLine}" +
                $"Source    : {source}{Environment.NewLine}" +
                $"OS        : {Environment.OSVersion}{Environment.NewLine}" +
                $"64-bit    : {Environment.Is64BitProcess}{Environment.NewLine}" +
                $"Elevated  : {AppVersion.IsElevated}{Environment.NewLine}" +
                new string('-', 60) + Environment.NewLine +
                (error?.ToString() ?? "(no exception object)");

            File.WriteAllText(path, report);
            Prune();
        }
        catch
        {
            // Swallowed by design. There is no fallback worth trying at
            // this point, and throwing here masks the original crash.
        }
    }

    /// <summary>Keeps the 20 most recent reports so the folder can't grow forever.</summary>
    private static void Prune()
    {
        try
        {
            var files = new DirectoryInfo(LogDir).GetFiles("crash-*.log");
            if (files.Length <= 20) return;

            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = 20; i < files.Length; i++)
                files[i].Delete();
        }
        catch
        {
            // Pruning is housekeeping; never let it break crash reporting.
        }
    }
}

/// <summary>
/// Single source of truth for the build identity shown in the UI and
/// stamped into crash reports, so a tester's report names an exact build.
/// </summary>
public static class AppVersion
{
    /// <summary>Informational version from the assembly, e.g. "0.1.0-alpha".</summary>
    public static string Full { get; } = ReadInformationalVersion();

    /// <summary>Same value prefixed for display, e.g. "v0.1.0-alpha".</summary>
    public static string Display => $"v{Full}";

    public static bool IsElevated
    {
        get
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    private static string ReadInformationalVersion()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(info))
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

        // The SDK appends "+<commit sha>" to InformationalVersion; drop it.
        var plus = info.IndexOf('+');
        return plus >= 0 ? info.Substring(0, plus) : info;
    }
}
