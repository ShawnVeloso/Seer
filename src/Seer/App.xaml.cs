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

        // RunSensorSmokeTest(); // Preserved for debugging, but disabled for live UI
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
