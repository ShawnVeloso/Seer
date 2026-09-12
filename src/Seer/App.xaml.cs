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
        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            CrashLogService.Write("AppDomain.UnhandledException", ev.ExceptionObject);

        DispatcherUnhandledException += (s, ev) =>
            CrashLogService.Write("DispatcherUnhandledException", ev.Exception);
        base.OnStartup(e);

        // Off-screen verification hook. Renders the window to PNG and exits
        // without showing anything; see RenderShot for why a build alone
        // proves nothing about XAML.
        if (RenderShot.IsRequested(e.Args))
        {
            var outputDir = RenderShot.Run(e.Args);
            Console.WriteLine(outputDir);
            Shutdown();
            return;
        }

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
