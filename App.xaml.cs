using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Seer.Services;

namespace Seer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Sensor smoke test (temporary diagnostic) ──
        // Runs once on startup to verify LibreHardwareMonitorLib can
        // detect hardware on this machine. Output goes to Debug trace
        // and a log file for verification.
        // This will be removed once real sensor polling is implemented.
        RunSensorSmokeTest();
    }

    private static void RunSensorSmokeTest()
    {
        const string header = "═══ Seer Sensor Smoke Test ═══";
        const string footer = "═══ End Smoke Test ═══";

        Debug.WriteLine(header);

        using var monitor = new HardwareMonitorService();
        string result = monitor.RunSmokeTest();

        Debug.WriteLine(result);
        Debug.WriteLine(footer);

        // Also write to a file for non-debugger verification
        string logPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "smoke_test.log");
        File.WriteAllText(logPath, $"{header}\n{result}\n{footer}\n");
    }
}
