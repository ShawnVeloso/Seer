using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Seer.Models;

namespace Seer;

/// <summary>
/// The rendering half of the main window: everything that reads a metrics
/// record and writes it into the visual tree.
///
/// Split from MainWindow.xaml.cs per AGENTS.md §4 — that file is now
/// window lifecycle and orchestration only. These methods stay a partial
/// class rather than moving to standalone types because each one writes
/// directly to a dozen x:Name'd elements; decoupling them properly means
/// binding to a view model, which is a much larger change than this
/// behaviour-preserving split.
/// </summary>
public partial class MainWindow
{
    private void UpdatePanels()
    {
        var cpu = UpdateCpuPanel();
        var mem = UpdateMemoryPanel();
        var gpu = UpdateGpuPanel();

        UpdateDiskPanel();
        UpdateNetworkPanel();
        UpdateTopProcessesPanel();
        
        _osdWindow?.UpdateStats(cpu, gpu, mem);
        
        var (overallSeverity, newAlerts) = _thresholdEvaluator.Evaluate(cpu, mem, gpu, _appSettings);
        
        foreach (var alert in newAlerts)
        {
            _alerts.Insert(0, alert); // Newest at top
        }

        while (_alerts.Count > MaxAlerts)
        {
            _alerts.RemoveAt(_alerts.Count - 1);
        }

        UpdateStatusBadge(overallSeverity);
    }

    /// <summary>
    /// Updates the CPU panel values. For elevation-gated fields
    /// (Temperature, Clock, Power): displays "--" in warning amber
    /// when the value is unavailable (null = NaN/0 from non-elevated run).
    /// CPU Load works without elevation and always uses normal text color.
    /// </summary>
    private CpuMetrics UpdateCpuPanel()
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
            AddHistory(_cpuHistory, cpu.TotalLoad.Value);
        }
        else
        {
            CpuLoadValue.Text = "--";
            CpuLoadUnit.Text = " %";
            
            AddHistory(_cpuHistory, 0f);
        }
        CpuChart.UpdateData(_cpuHistory);

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

        // Per-core Load Bars
        if (cpu.CoreLoads != null && cpu.CoreLoads.Length > 0)
        {
            var coreStrings = new string[cpu.CoreLoads.Length];
            for (int i = 0; i < cpu.CoreLoads.Length; i++)
            {
                var core = cpu.CoreLoads[i];
                float load = core.Load;
                int bars = (int)Math.Round(load / 10.0f);
                bars = Math.Clamp(bars, 0, 10);
                string barStr = new string('|', bars).PadRight(10);
                coreStrings[i] = $"{i,2}[{barStr} {load,5:F1}%]";
            }
            CpuCoreBarsControl.ItemsSource = coreStrings;
            CpuCoreBarsControl.Visibility = Visibility.Visible;
        }
        else
        {
            CpuCoreBarsControl.Visibility = Visibility.Collapsed;
        }

        return cpu;
    }

    /// <summary>
    /// Updates the Memory panel values. All memory sensors work without
    /// admin elevation — no amber fallback needed.
    /// </summary>
    private MemoryMetrics UpdateMemoryPanel()
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
            AddHistory(_memHistory, mem.Load.Value);
        }
        else
        {
            MemLoadValue.Text = "-- %";
            
            AddHistory(_memHistory, 0f);
        }
        MemChart.UpdateData(_memHistory);

        if (mem.AvailableGb.HasValue)
        {
            MemAvailValue.Text = $"{mem.AvailableGb.Value:F1} GB";
        }
        else
        {
            MemAvailValue.Text = "-- GB";
        }

        return mem;
    }

    /// <summary>
    /// Updates the GPU panel values. 
    /// Adds warning brush fallback for null values, even though GPU sensors 
    /// generally do not require elevation.
    /// </summary>
    private GpuMetrics UpdateGpuPanel()
    {
        var gpu = _monitor.GetGpuMetrics();

        // Temperature
        if (gpu.Temperature.HasValue)
        {
            GpuTempValue.Text = gpu.Temperature.Value.ToString("F0");
            GpuTempValue.Foreground = _normalBrush;
            GpuTempUnit.Foreground = _normalBrush;
        }
        else
        {
            GpuTempValue.Text = "--";
            GpuTempValue.Foreground = _warningBrush;
            GpuTempUnit.Foreground = _warningBrush;
        }

        // Load
        if (gpu.Load.HasValue)
        {
            GpuLoadValue.Text = gpu.Load.Value.ToString("F1");
            GpuLoadValue.Foreground = _normalBrush;
            GpuLoadUnit.Foreground = _normalBrush;
            UpdateStatusBar(GpuStatusBar, gpu.Load.Value);
            AddHistory(_gpuHistory, gpu.Load.Value);
        }
        else
        {
            GpuLoadValue.Text = "--";
            GpuLoadValue.Foreground = _warningBrush;
            GpuLoadUnit.Foreground = _warningBrush;
            
            AddHistory(_gpuHistory, 0f);
        }
        GpuChart.UpdateData(_gpuHistory);

        // Clock
        if (gpu.Clock.HasValue)
        {
            GpuClockValue.Text = gpu.Clock.Value.ToString("F0");
            GpuClockValue.Foreground = _normalBrush;
            GpuClockUnit.Foreground = _normalBrush;
        }
        else
        {
            GpuClockValue.Text = "--";
            GpuClockValue.Foreground = _warningBrush;
            GpuClockUnit.Foreground = _warningBrush;
        }

        // Hot Spot
        if (gpu.HotSpotTemperature.HasValue)
        {
            GpuHotSpotValue.Text = gpu.HotSpotTemperature.Value.ToString("F0");
            GpuHotSpotValue.Foreground = _normalBrush;
            GpuHotSpotUnit.Foreground = _normalBrush;
        }
        else
        {
            GpuHotSpotValue.Text = "--";
            GpuHotSpotValue.Foreground = _warningBrush;
            GpuHotSpotUnit.Foreground = _warningBrush;
        }

        // Fan (prefer RPM, fallback to %)
        if (gpu.FanRpm.HasValue)
        {
            GpuFanValue.Text = gpu.FanRpm.Value.ToString("F0");
            GpuFanUnit.Text = " RPM";
            GpuFanValue.Foreground = _normalBrush;
            GpuFanUnit.Foreground = _normalBrush;
        }
        else if (gpu.FanPercent.HasValue)
        {
            GpuFanValue.Text = gpu.FanPercent.Value.ToString("F1");
            GpuFanUnit.Text = " %";
            GpuFanValue.Foreground = _normalBrush;
            GpuFanUnit.Foreground = _normalBrush;
        }
        else
        {
            GpuFanValue.Text = "--";
            GpuFanUnit.Text = " RPM";
            GpuFanValue.Foreground = _warningBrush;
            GpuFanUnit.Foreground = _warningBrush;
        }

        // VRAM
        if (gpu.VramUsedGb.HasValue && gpu.VramTotalGb.HasValue)
        {
            GpuVramValue.Text = $"{gpu.VramUsedGb.Value:F1} / {gpu.VramTotalGb.Value:F1} GB";
            GpuVramValue.Foreground = _normalBrush;
        }
        else
        {
            GpuVramValue.Text = "-- / -- GB";
            GpuVramValue.Foreground = _warningBrush;
        }

        return gpu;
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

    private void UpdateTopProcessesPanel()
    {
        var topProcs = _processMonitor.GetTopProcesses(5);
        TopProcessesControl.ItemsSource = topProcs;
    }

    private void UpdateDiskPanel()
    {
        var metrics = _diskMonitor.GetMetrics();
        DiskReadBar.Text = metrics.ReadBar;
        DiskReadValue.Text = $"{metrics.ReadBytesPerSec / (1024.0 * 1024.0):F1} MB/s";
        
        DiskWriteBar.Text = metrics.WriteBar;
        DiskWriteValue.Text = $"{metrics.WriteBytesPerSec / (1024.0 * 1024.0):F1} MB/s";
    }

    private void UpdateNetworkPanel()
    {
        var metrics = _networkMonitor.GetMetrics();
        NetDownBar.Text = metrics.DownloadBar;
        NetDownValue.Text = $"{metrics.DownloadMbps:F1} Mbps";
        
        NetUpBar.Text = metrics.UploadBar;
        NetUpValue.Text = $"{metrics.UploadMbps:F1} Mbps";
    }

    private void UpdateStatusBadge(AlertSeverity severity)
    {
        if (severity == AlertSeverity.Critical)
        {
            StatusBadgeText.Text = "CRITICAL";
            StatusBadgeText.Foreground = (SolidColorBrush)FindResource("SeerDanger");
            StatusBadgeBorder.BorderBrush = (SolidColorBrush)FindResource("SeerDanger");
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(26, 239, 68, 68)); // #1AEF4444 (10% opacity)
        }
        else if (severity == AlertSeverity.Warning)
        {
            StatusBadgeText.Text = "WARNING";
            StatusBadgeText.Foreground = (SolidColorBrush)FindResource("SeerWarning");
            StatusBadgeBorder.BorderBrush = (SolidColorBrush)FindResource("SeerWarning");
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(26, 245, 158, 11)); // #1AF59E0B (10% opacity)
        }
        else
        {
            StatusBadgeText.Text = "NOMINAL";
            StatusBadgeText.Foreground = (SolidColorBrush)FindResource("SeerSuccess");
            StatusBadgeBorder.BorderBrush = (SolidColorBrush)FindResource("SeerSuccess");
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(26, 61, 220, 132)); // #1A3DDC84 (10% opacity)
        }
    }

    private void AddHistory(Queue<float> queue, float value)
    {
        queue.Enqueue(value);
        if (queue.Count > MaxHistory)
        {
            queue.Dequeue();
        }
    }
}
