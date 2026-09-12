using System;
using System.Collections.Generic;
using Seer.Models;

namespace Seer.Services;

public class ThresholdEvaluator
{
    private AlertSeverity _prevCpuLoad = AlertSeverity.Nominal;
    private AlertSeverity _prevCpuTemp = AlertSeverity.Nominal;
    private AlertSeverity _prevMemLoad = AlertSeverity.Nominal;
    private AlertSeverity _prevGpuLoad = AlertSeverity.Nominal;
    private AlertSeverity _prevGpuTemp = AlertSeverity.Nominal;

    /// <summary>
    /// The per-metric severities from the most recent <see cref="Evaluate"/>.
    ///
    /// Exposed separately rather than folded into the return value, because
    /// the tuple is part of this type's contract and the panels needed a
    /// detail the badge does not: which metric is the one that escalated.
    /// A missing sensor stays Nominal here — absent is not the same as calm,
    /// but the panel shows "--" in amber for that case already.
    /// </summary>
    public SeverityBreakdown Last { get; private set; } = SeverityBreakdown.Nominal;

    public (AlertSeverity Overall, List<AlertEvent> NewAlerts) Evaluate(CpuMetrics cpu, MemoryMetrics mem, GpuMetrics gpu, AppSettings settings)
    {
        var alerts = new List<AlertEvent>();
        var overall = AlertSeverity.Nominal;

        var cpuLoad = AlertSeverity.Nominal;
        var cpuTemp = AlertSeverity.Nominal;
        var memLoad = AlertSeverity.Nominal;
        var gpuLoad = AlertSeverity.Nominal;
        var gpuTemp = AlertSeverity.Nominal;

        // CPU Load
        if (cpu.TotalLoad.HasValue)
        {
            cpuLoad = EvaluateThreshold(cpu.TotalLoad.Value, settings.LoadWarningThreshold, settings.LoadCriticalThreshold);
            UpdateOverall(ref overall, cpuLoad);
            CheckAndAddAlert(alerts, "CPU Load", cpuLoad, ref _prevCpuLoad, cpu.TotalLoad.Value, "%");
        }

        // CPU Temp
        if (cpu.Temperature.HasValue)
        {
            cpuTemp = EvaluateThreshold(cpu.Temperature.Value, settings.TempWarningThreshold, settings.TempCriticalThreshold);
            UpdateOverall(ref overall, cpuTemp);
            CheckAndAddAlert(alerts, "CPU Temp", cpuTemp, ref _prevCpuTemp, cpu.Temperature.Value, " °C");
        }

        // Mem Load
        if (mem.Load.HasValue)
        {
            memLoad = EvaluateThreshold(mem.Load.Value, settings.LoadWarningThreshold, settings.LoadCriticalThreshold);
            UpdateOverall(ref overall, memLoad);
            CheckAndAddAlert(alerts, "MEM Load", memLoad, ref _prevMemLoad, mem.Load.Value, "%");
        }

        // GPU Load
        if (gpu.Load.HasValue)
        {
            gpuLoad = EvaluateThreshold(gpu.Load.Value, settings.LoadWarningThreshold, settings.LoadCriticalThreshold);
            UpdateOverall(ref overall, gpuLoad);
            CheckAndAddAlert(alerts, "GPU Load", gpuLoad, ref _prevGpuLoad, gpu.Load.Value, "%");
        }

        // GPU Temp
        if (gpu.Temperature.HasValue)
        {
            gpuTemp = EvaluateThreshold(gpu.Temperature.Value, settings.TempWarningThreshold, settings.TempCriticalThreshold);
            UpdateOverall(ref overall, gpuTemp);
            CheckAndAddAlert(alerts, "GPU Temp", gpuTemp, ref _prevGpuTemp, gpu.Temperature.Value, " °C");
        }

        Last = new SeverityBreakdown(cpuLoad, cpuTemp, memLoad, gpuLoad, gpuTemp);

        return (overall, alerts);
    }

    /// <summary>
    /// Classifies a single value against a warning/critical pair.
    /// Both bounds are inclusive.
    ///
    /// Public and static so the tray icons and overlay colour their
    /// readouts by exactly the rule that drives the status badge and the
    /// alert log — one definition of "warm", not three.
    /// </summary>
    public static AlertSeverity Classify(float value, float warningThreshold, float criticalThreshold)
    {
        if (value >= criticalThreshold) return AlertSeverity.Critical;
        if (value >= warningThreshold) return AlertSeverity.Warning;
        return AlertSeverity.Nominal;
    }

    private AlertSeverity EvaluateThreshold(float value, float warningThreshold, float criticalThreshold)
        => Classify(value, warningThreshold, criticalThreshold);

    private void UpdateOverall(ref AlertSeverity overall, AlertSeverity current)
    {
        if (current > overall)
            overall = current;
    }

    private void CheckAndAddAlert(List<AlertEvent> alerts, string metricName, AlertSeverity current, ref AlertSeverity previous, float value, string unit)
    {
        // Log an alert if severity escalated to Warning or Critical
        if (current > previous && current != AlertSeverity.Nominal)
        {
            alerts.Add(new AlertEvent(DateTime.Now, metricName, current, value, unit));
        }
        previous = current;
    }
}
