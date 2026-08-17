using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Seer.Services;

namespace Seer;

public partial class MainWindow : Window
{
    private readonly HardwareMonitorService _monitor;
    private readonly DispatcherTimer _pollTimer;

    // Cached brushes from theme resources for elevation-aware display
    private readonly SolidColorBrush _normalBrush;
    private readonly SolidColorBrush _warningBrush;

    public MainWindow()
    {
        InitializeComponent();

        _normalBrush = (SolidColorBrush)FindResource("SeerText");
        _warningBrush = (SolidColorBrush)FindResource("SeerWarning");

        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                ElevateButton.Visibility = Visibility.Collapsed;
                // Optional subtle indicator could go here, or we just hide the button
            }
        }

        _monitor = new HardwareMonitorService();
        _monitor.Open();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pollTimer.Tick += PollTimer_Tick;
        _pollTimer.Start();

        // Run an immediate first update so panels don't sit empty for 1s
        UpdatePanels();
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        UpdatePanels();
    }

    private void UpdatePanels()
    {
        UpdateCpuPanel();
        UpdateMemoryPanel();
    }

    private void ElevateButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var exeName = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exeName)) return;

        var startInfo = new ProcessStartInfo(exeName)
        {
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
            Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled the UAC prompt.
            // Degrade gracefully by doing nothing; stay running non-elevated.
        }
    }

    /// <summary>
    /// Updates the CPU panel values. For elevation-gated fields
    /// (Temperature, Clock, Power): displays "--" in warning amber
    /// when the value is unavailable (null = NaN/0 from non-elevated run).
    /// CPU Load works without elevation and always uses normal text color.
    /// </summary>
    private void UpdateCpuPanel()
    {
        var cpu = _monitor.GetCpuMetrics();

        // Temperature — elevation-gated
        if (cpu.Temperature.HasValue)
        {
            CpuTempValue.Text = cpu.Temperature.Value.ToString("F0");
            CpuTempValue.Foreground = _normalBrush;
            CpuTempUnit.Foreground = _normalBrush;
        }
        else
        {
            CpuTempValue.Text = "--";
            CpuTempValue.Foreground = _warningBrush;
            CpuTempUnit.Foreground = _warningBrush;
        }

        // Load — NOT elevation-gated
        if (cpu.TotalLoad.HasValue)
        {
            CpuLoadValue.Text = cpu.TotalLoad.Value.ToString("F1");
            CpuLoadUnit.Text = " %";

            // Also update the status strip CPU bar (percentage of parent width)
            // The status strip track is inside a Grid, so we approximate
            // using a fixed max width matching the column
            UpdateStatusBar(CpuStatusBar, cpu.TotalLoad.Value);
        }
        else
        {
            CpuLoadValue.Text = "--";
            CpuLoadUnit.Text = " %";
        }

        // Clock — elevation-gated
        if (cpu.Clock.HasValue)
        {
            CpuClockValue.Text = cpu.Clock.Value.ToString("F0");
            CpuClockValue.Foreground = _normalBrush;
            CpuClockUnit.Foreground = _normalBrush;
        }
        else
        {
            CpuClockValue.Text = "--";
            CpuClockValue.Foreground = _warningBrush;
            CpuClockUnit.Foreground = _warningBrush;
        }

        // Power — elevation-gated
        if (cpu.Power.HasValue)
        {
            CpuPowerValue.Text = cpu.Power.Value.ToString("F1");
            CpuPowerValue.Foreground = _normalBrush;
            CpuPowerUnit.Foreground = _normalBrush;
        }
        else
        {
            CpuPowerValue.Text = "--";
            CpuPowerValue.Foreground = _warningBrush;
            CpuPowerUnit.Foreground = _warningBrush;
        }
    }

    /// <summary>
    /// Updates the Memory panel values. All memory sensors work without
    /// admin elevation — no amber fallback needed.
    /// </summary>
    private void UpdateMemoryPanel()
    {
        var mem = _monitor.GetMemoryMetrics();

        if (mem.UsedGb.HasValue && mem.TotalGb.HasValue)
        {
            MemUsedValue.Text = $"{mem.UsedGb.Value:F1} / {mem.TotalGb.Value:F1} GB";
        }
        else
        {
            MemUsedValue.Text = "-- / -- GB";
        }

        if (mem.Load.HasValue)
        {
            MemLoadValue.Text = $"{mem.Load.Value:F1} %";

            // Update status strip MEM bar
            UpdateStatusBar(MemStatusBar, mem.Load.Value);
        }
        else
        {
            MemLoadValue.Text = "-- %";
        }

        if (mem.AvailableGb.HasValue)
        {
            MemAvailValue.Text = $"{mem.AvailableGb.Value:F1} GB";
        }
        else
        {
            MemAvailValue.Text = "-- GB";
        }
    }

    /// <summary>
    /// Updates a status strip bar width based on a 0–100 percentage value.
    /// The bar's parent Grid provides the available width.
    /// </summary>
    private static void UpdateStatusBar(FrameworkElement bar, float percentage)
    {
        // Clamp to 0–100
        percentage = Math.Clamp(percentage, 0f, 100f);

        // The bar is inside a Grid that's inside a StackPanel column.
        // We use the parent Grid's actual width to calculate the bar width.
        if (bar.Parent is FrameworkElement parent && parent.ActualWidth > 0)
        {
            bar.Width = parent.ActualWidth * (percentage / 100.0);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _pollTimer.Stop();
        _monitor.Dispose();
        base.OnClosed(e);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
