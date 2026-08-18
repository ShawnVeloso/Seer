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

    public (AlertSeverity Overall, List<AlertEvent> NewAlerts) Evaluate(CpuMetrics cpu, MemoryMetrics mem, GpuMetrics gpu)
    {
        var alerts = new List<AlertEvent>();
        var overall = AlertSeverity.Nominal;

        // CPU Load
        if (cpu.TotalLoad.HasValue)
        {
            var severity = EvaluateThreshold(cpu.TotalLoad.Value, 85f, 95f);
            UpdateOverall(ref overall, severity);
            CheckAndAddAlert(alerts, "CPU Load", severity, ref _prevCpuLoad, cpu.TotalLoad.Value, "%");
        }

        // CPU Temp
        if (cpu.Temperature.HasValue)
        {
            var severity = EvaluateThreshold(cpu.Temperature.Value, 75f, 85f);
            UpdateOverall(ref overall, severity);
            CheckAndAddAlert(alerts, "CPU Temp", severity, ref _prevCpuTemp, cpu.Temperature.Value, " °C");
        }

        // Mem Load
        if (mem.Load.HasValue)
        {
            var severity = EvaluateThreshold(mem.Load.Value, 85f, 95f);
            UpdateOverall(ref overall, severity);
            CheckAndAddAlert(alerts, "MEM Load", severity, ref _prevMemLoad, mem.Load.Value, "%");
        }

        // GPU Load
        if (gpu.Load.HasValue)
        {
            var severity = EvaluateThreshold(gpu.Load.Value, 85f, 95f);
            UpdateOverall(ref overall, severity);
            CheckAndAddAlert(alerts, "GPU Load", severity, ref _prevGpuLoad, gpu.Load.Value, "%");
        }

        // GPU Temp
        if (gpu.Temperature.HasValue)
        {
            var severity = EvaluateThreshold(gpu.Temperature.Value, 75f, 85f);
            UpdateOverall(ref overall, severity);
            CheckAndAddAlert(alerts, "GPU Temp", severity, ref _prevGpuTemp, gpu.Temperature.Value, " °C");
        }

        return (overall, alerts);
    }

    private AlertSeverity EvaluateThreshold(float value, float warningThreshold, float criticalThreshold)
    {
        if (value >= criticalThreshold) return AlertSeverity.Critical;
        if (value >= warningThreshold) return AlertSeverity.Warning;
        return AlertSeverity.Nominal;
    }

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
