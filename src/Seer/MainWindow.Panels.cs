using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Seer.Controls;
using Seer.Models;
using Seer.Services;

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
    /// <summary>
    /// One poll's worth of rendering. Internal rather than private so the
    /// off-screen render harness can fill the history buffers before taking a
    /// shot — a chart with one sample has no line to draw.
    /// </summary>
    internal void UpdatePanels()
    {
        _pollClock.BeginPoll(DateTime.Now);

        var cpu = UpdateCpuPanel();
        var mem = UpdateMemoryPanel();
        var gpu = UpdateGpuPanel();

        UpdateDiskPanel();
        UpdateNetworkPanel();
        UpdateTopProcessesPanel();
        UpdatePingPanel();
        
        _osdWindow?.UpdateStats(cpu, gpu, mem);
        _trayMetrics?.Update(cpu, mem, gpu, _appSettings);
        
        var (overallSeverity, newAlerts) = _thresholdEvaluator.Evaluate(cpu, mem, gpu, _appSettings);

        ApplyPanelSeverity(_thresholdEvaluator.Last);
        
        foreach (var alert in newAlerts)
            _events.AddAlert(alert);

        UpdateStatusBadge(overallSeverity);

        // Stop the clock before the living details render, so the figure on
        // the status line is the cost of reading and drawing the data rather
        // than the cost of reporting on itself.
        _pollClock.EndPoll();
        UpdateLivingDetails();
    }

    /// <summary>CPU share at which a process is worth an event.</summary>
    private const double HeavyProcessPercent = 20;

    /// <summary>
    /// Logs a process the first time it crosses the heavy mark, and forgets
    /// it once it drops back. Without the second half, a game running for an
    /// hour would either log nothing or log every single second.
    /// </summary>
    private void RecordHeavyProcesses(IReadOnlyList<ProcessMetrics> top)
    {
        var stillHeavy = new HashSet<int>();

        foreach (var process in top)
        {
            if (process.CpuPercent < HeavyProcessPercent)
                continue;

            stillHeavy.Add(process.Pid);

            if (_heavyProcesses.Add(process.Pid))
                _events.Add(process.Name, $"{process.CpuPercent:F0}% CPU");
        }

        _heavyProcesses.IntersectWith(stillHeavy);
    }

    /// <summary>
    /// Gives a "used of total" readout a shape as well as digits. Hidden
    /// rather than emptied when the sensor is unavailable — a meter stuck at
    /// zero would read as "nothing in use" rather than "nothing known".
    /// </summary>
    private static void UpdateInlineMeter(SegmentMeter meter, float? used, float? total)
    {
        if (!HudConfig.EnableInlineMeters || used is not float amount || total is not float capacity || capacity <= 0)
        {
            meter.Visibility = Visibility.Collapsed;
            return;
        }

        meter.Visibility = Visibility.Visible;
        meter.Max = capacity;
        meter.Value = amount;
    }

    /// <summary>
    /// Feeds one reading into its session statistics and hands the chart the
    /// figures to show: low, mean and high on the border line, and the
    /// session peak as a line across the plot.
    /// </summary>
    private static void UpdateChartSession(TrendChart chart, SessionStats stats, float? value)
    {
        if (value is float reading)
            stats.Push(reading);

        chart.SessionPeak = (float?)stats.Max;
        chart.SetSessionStats((float?)stats.Min, (float?)stats.Average, (float?)stats.Max);
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

            AddHistory(_cpuHistory, cpu.TotalLoad.Value);
        }
        else
        {
            CpuLoadValue.Text = "--";
            CpuLoadUnit.Text = " %";
            
            AddHistory(_cpuHistory, 0f);
        }
        UpdateStatusMeter(CpuStatusBar, CpuStatusValue, _cpuPeak, cpu.TotalLoad);
        UpdateChartSession(CpuChart, _cpuSession, cpu.TotalLoad);
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

        UpdateTrendArrow(CpuTempTrend, _cpuTempSlope, cpu.Temperature, _warningBrush, _dimBrush);
        UpdateTrendArrow(CpuLoadTrend, _cpuLoadSlope, cpu.TotalLoad, _normalBrush, _dimBrush);

        // Per-core load bars. The matrix owns its own layout now, so there is
        // nothing to compose here beyond the peak-hold values.
        if (cpu.CoreLoads is { Length: > 0 })
        {
            CpuCoreMatrix.SetCores(cpu.CoreLoads, CorePeaks(cpu.CoreLoads));
            CpuCoreMatrix.Visibility = Visibility.Visible;
        }
        else
        {
            CpuCoreMatrix.Visibility = Visibility.Collapsed;
        }

        return cpu;
    }

    /// <summary>
    /// Pushes each core's load through its own peak-hold and returns the
    /// values to mark. The matrix draws them; the decay lives here, because
    /// it is session state rather than a drawing concern.
    /// </summary>
    private double[] CorePeaks((string Name, float Load)[] coreLoads)
    {
        if (!HudConfig.EnablePeakHold)
            return Array.Empty<double>();

        if (_corePeaks.Length != coreLoads.Length)
        {
            _corePeaks = new PeakHold[coreLoads.Length];
            for (var i = 0; i < _corePeaks.Length; i++)
                _corePeaks[i] = new PeakHold();
        }

        var peaks = new double[coreLoads.Length];
        for (var i = 0; i < coreLoads.Length; i++)
            peaks[i] = _corePeaks[i].Push(coreLoads[i].Load);

        return peaks;
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

        UpdateInlineMeter(MemUsedMeter, mem.UsedGb, mem.TotalGb);

        if (mem.Load.HasValue)
        {
            MemLoadValue.Text = $"{mem.Load.Value:F1} %";

            AddHistory(_memHistory, mem.Load.Value);
        }
        else
        {
            MemLoadValue.Text = "-- %";
            
            AddHistory(_memHistory, 0f);
        }
        UpdateStatusMeter(MemStatusBar, MemStatusValue, _memPeak, mem.Load);
        UpdateChartSession(MemChart, _memSession, mem.Load);
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
            AddHistory(_gpuHistory, gpu.Load.Value);
        }
        else
        {
            GpuLoadValue.Text = "--";
            GpuLoadValue.Foreground = _warningBrush;
            GpuLoadUnit.Foreground = _warningBrush;
            
            AddHistory(_gpuHistory, 0f);
        }
        UpdateStatusMeter(GpuStatusBar, GpuStatusValue, _gpuPeak, gpu.Load);
        UpdateChartSession(GpuChart, _gpuSession, gpu.Load);
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

        UpdateInlineMeter(GpuVramMeter, gpu.VramUsedGb, gpu.VramTotalGb);

        UpdateTrendArrow(GpuTempTrend, _gpuTempSlope, gpu.Temperature, _warningBrush, _dimBrush);
        UpdateTrendArrow(GpuLoadTrend, _gpuLoadSlope, gpu.Load, _normalBrush, _dimBrush);

        return gpu;
    }

    /// <summary>
    /// Paints one status strip channel: the segmented bar, its printed value,
    /// the warning and critical ticks, and the peak-hold marker.
    ///
    /// The thresholds are pushed every poll rather than bound once, so a
    /// change in the settings window lands on the next tick like every other
    /// setting. The meter reads its own width, which the old version could
    /// not — it sized the bar from its parent's ActualWidth and so drew
    /// nothing at all on the first tick, before layout had run.
    /// </summary>
    private void UpdateStatusMeter(SegmentMeter meter, TextBlock label, PeakHold peak, float? value)
    {
        meter.WarnThreshold = _appSettings.LoadWarningThreshold;
        meter.CritThreshold = _appSettings.LoadCriticalThreshold;

        if (value is not float reading)
        {
            meter.Value = 0;
            meter.Peak = double.NaN;
            peak.Reset();
            label.Text = "  --";
            return;
        }

        meter.Value = reading;
        meter.Peak = HudConfig.EnablePeakHold ? peak.Push(reading) : double.NaN;

        // Padded to a fixed width: this changes every second, and a label
        // that changes width re-measures the whole strip with it.
        label.Text = $"{reading,3:F0}%";
    }

    /// <summary>
    /// The busiest processes, with rank marks, and the totals they came from.
    /// The counts are padded so the header doesn't re-measure every poll as
    /// processes come and go.
    /// </summary>
    private void UpdateTopProcessesPanel()
    {
        var snapshot = _processMonitor.GetSnapshot(5);

        TopProcessesControl.ItemsSource = HudConfig.EnableProcessRankArrows
            ? _rankTracker.Apply(snapshot.Top)
            : snapshot.Top;

        RecordHeavyProcesses(snapshot.Top);

        ProcPanel.Meta = snapshot.TotalThreads > 0
            ? $"{snapshot.Top.Count} OF {snapshot.TotalProcesses,4} · {snapshot.TotalThreads,5} THREADS"
            : $"{snapshot.Top.Count} OF {snapshot.TotalProcesses,4}";
    }

    /// <summary>
    /// Disk throughput. The meters are logarithmic: on the old linear bar,
    /// scaled to 100 MB/s, ordinary activity never lit a single character, so
    /// the row read as idle whenever it wasn't saturated.
    /// </summary>
    private void UpdateDiskPanel()
    {
        var metrics = _diskMonitor.GetMetrics();

        var read = metrics.ReadBytesPerSec / (1024.0 * 1024.0);
        var write = metrics.WriteBytesPerSec / (1024.0 * 1024.0);

        DiskReadMeter.Value = read;
        DiskReadValue.Text = $"{read,6:F1} MB/s";

        DiskWriteMeter.Value = write;
        DiskWriteValue.Text = $"{write,6:F1} MB/s";

        UpdateActivityLed(DiskReadLed, read);
        UpdateActivityLed(DiskWriteLed, write);

        // Marked with "~": Windows reports disk throughput as a rate with no
        // cumulative counter behind it, so the total is integrated from
        // one-second samples and misses anything shorter than the gap.
        var now = DateTime.UtcNow;
        DiskPanel.Meta = HudConfig.EnableSessionStats
            ? $"~{FormatBytes(_diskReadTotal.Add(metrics.ReadBytesPerSec, now))} R   ~{FormatBytes(_diskWriteTotal.Add(metrics.WriteBytesPerSec, now))} W"
            : string.Empty;
    }

    private void UpdateNetworkPanel()
    {
        var metrics = _networkMonitor.GetMetrics();

        NetDownMeter.Value = metrics.DownloadMbps;
        NetDownValue.Text = $"{metrics.DownloadMbps,6:F1} Mbps";

        NetUpMeter.Value = metrics.UploadMbps;
        NetUpValue.Text = $"{metrics.UploadMbps,6:F1} Mbps";

        UpdateActivityLed(NetDownLed, metrics.DownloadMbps);
        UpdateActivityLed(NetUpLed, metrics.UploadMbps);

        // No "~" here: the adapters keep real byte counters, so these totals
        // are exact rather than integrated.
        NetPanel.Meta = HudConfig.EnableSessionStats
            ? $"{FormatBytes(metrics.SessionReceivedBytes)} RX   {FormatBytes(metrics.SessionSentBytes)} TX"
            : string.Empty;
    }

    /// <summary>
    /// Renders the latency panel from the monitor's snapshot.
    ///
    /// Read on the UI timer rather than pushed from the ping loop: the
    /// loop runs on a background task at its own cadence, and polling an
    /// immutable snapshot avoids marshalling every reply onto the
    /// dispatcher just to update four labels.
    /// </summary>
    private void UpdatePingPanel()
    {
        var ping = _pingMonitor.GetSnapshot();

        PingToggleButton.Content = ping.IsRunning ? "STOP" : "START";
        PingHostBox.IsEnabled = !ping.IsRunning;

        if (!ping.IsRunning && ping.Sent == 0)
        {
            PingStateText.Text = "STOPPED";
            PingStateText.Foreground = _dimBrush;
            PingLatencyValue.Text = "--";
            PingAverageValue.Text = "--";
            PingJitterValue.Text = "--";
            PingLossValue.Text = "--";
            PingTapeStrip.Samples = null;
            PingTapeStrip.Visibility = Visibility.Collapsed;
            PingPanel.Meta = string.Empty;
            return;
        }

        if (ping.LastError != null)
        {
            PingStateText.Text = ping.LastError.ToUpperInvariant();
            PingStateText.Foreground = _warningBrush;
        }
        else
        {
            PingStateText.Text = ping.IsRunning ? "RUNNING" : "STOPPED";
            PingStateText.Foreground = ping.IsRunning ? _successBrush : _dimBrush;
        }

        PingLatencyValue.Text = ping.LastMs.HasValue ? $"{ping.LastMs.Value} ms" : "--";
        PingAverageValue.Text = ping.AverageMs.HasValue ? $"{ping.AverageMs.Value:F1} ms" : "--";
        PingJitterValue.Text = ping.JitterMs.HasValue ? $"{ping.JitterMs.Value:F1} ms" : "--";
        PingLossValue.Text = $"{ping.LossPercent:F0} % ({ping.Sent} sent)";

        // Loss is the reading that matters most, so colour it directly
        // rather than leaving it to be spotted among the numbers.
        PingLossValue.Foreground = ping.LossPercent switch
        {
            >= 10 => _dangerBrush,
            > 0 => _warningBrush,
            _ => _normalBrush
        };

        PingTapeStrip.Samples = HudConfig.EnablePingTape ? ping.Recent : null;
        PingTapeStrip.Visibility = HudConfig.EnablePingTape && ping.Recent.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        // The target and cadence belong on the border line, where every other
        // panel states what it is measuring.
        PingPanel.Meta = $"{ping.Host} · EVERY {_appSettings.PingIntervalSeconds:F0} S";
    }

    /// <summary>
    /// Gives each panel the worst severity among its own readings, so the
    /// panel behind a WARNING badge is the one that lights up. The status
    /// strip says something is wrong; this says which thing.
    /// </summary>
    private void ApplyPanelSeverity(SeverityBreakdown breakdown)
    {
        CpuPanel.Severity = _cpuSeverity.Push(breakdown.Cpu);
        MemPanel.Severity = _memSeverity.Push(breakdown.Mem);
        GpuPanel.Severity = _gpuSeverity.Push(breakdown.Gpu);
    }

    /// <summary>
    /// Paints the system-state badge. Every brush here is cached in the
    /// constructor — this runs on the poll, and the previous version resolved
    /// two resources and allocated a fresh brush every second.
    ///
    /// The tints are theme tokens rather than literals now: the old hard-coded
    /// values (#1AF59E0B, #1AEF4444) were 10% of colours the theme had since
    /// moved away from, so the badge and its own palette disagreed.
    /// </summary>
    private void UpdateStatusBadge(AlertSeverity severity)
    {
        var (text, foreground, tint) = severity switch
        {
            AlertSeverity.Critical => ("CRITICAL", _dangerBrush, _dangerTintBrush),
            AlertSeverity.Warning => ("WARNING", _warningBrush, _warningTintBrush),
            _ => ("NOMINAL", _successBrush, _successTintBrush)
        };

        StatusBadgeText.Text = text;
        StatusBadgeText.Foreground = foreground;
        StatusBadgeBorder.BorderBrush = foreground;
        StatusBadgeBorder.Background = tint;
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
