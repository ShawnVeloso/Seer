using System;
using System.Collections.Generic;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// One reading, formatted for display, with the severity that decides
/// its colour.
/// </summary>
/// <param name="Metric">Which value this is.</param>
/// <param name="Label">Short caption, e.g. "CPU".</param>
/// <param name="Unit">Unit suffix, e.g. "°C". Empty for unitless.</param>
/// <param name="Compact">
/// Value alone, no unit, sized for a 16px tray icon — e.g. "66".
/// "--" when the sensor is unavailable.
/// </param>
/// <param name="Full">Label, value and unit, e.g. "CPU 66°C".</param>
/// <param name="Severity">Nominal / Warning / Critical, for colouring.</param>
public record ReadoutValue(
    ReadoutMetric Metric,
    string Label,
    string Unit,
    string Compact,
    string Full,
    AlertSeverity Severity);

/// <summary>
/// Turns a metrics snapshot into display-ready readouts.
///
/// Shared by the overlay and the tray icons so the two can never disagree
/// about what "GPU 55°C" means or when it turns amber. Severity comes
/// from <see cref="ThresholdEvaluator"/>, which stays the single source
/// of truth for thresholds.
/// </summary>
public static class ReadoutFormatter
{
    /// <summary>Every metric, in the order they're offered in the UI.</summary>
    public static readonly ReadoutMetric[] All =
    {
        ReadoutMetric.CpuLoad,
        ReadoutMetric.CpuTemp,
        ReadoutMetric.GpuLoad,
        ReadoutMetric.GpuTemp,
        ReadoutMetric.MemLoad,
        ReadoutMetric.MemUsed,
        ReadoutMetric.GpuVram
    };

    /// <summary>Human-readable name for settings UI, e.g. "CPU temperature".</summary>
    public static string DisplayName(ReadoutMetric metric) => metric switch
    {
        ReadoutMetric.CpuLoad => "CPU load",
        ReadoutMetric.CpuTemp => "CPU temperature",
        ReadoutMetric.GpuLoad => "GPU load",
        ReadoutMetric.GpuTemp => "GPU temperature",
        ReadoutMetric.MemLoad => "Memory load",
        ReadoutMetric.MemUsed => "Memory used",
        ReadoutMetric.GpuVram => "GPU VRAM used",
        _ => metric.ToString()
    };

    /// <summary>
    /// Reads one metric out of the current snapshot.
    /// </summary>
    public static ReadoutValue Read(
        ReadoutMetric metric,
        CpuMetrics cpu,
        MemoryMetrics mem,
        GpuMetrics gpu,
        AppSettings settings)
    {
        return metric switch
        {
            ReadoutMetric.CpuLoad => Percent(metric, "CPU", cpu.TotalLoad, settings, isTemperature: false),
            ReadoutMetric.GpuLoad => Percent(metric, "GPU", gpu.Load, settings, isTemperature: false),
            ReadoutMetric.MemLoad => Percent(metric, "RAM", mem.Load, settings, isTemperature: false),

            ReadoutMetric.CpuTemp => Temperature(metric, "CPU", cpu.Temperature, settings),
            ReadoutMetric.GpuTemp => Temperature(metric, "GPU", gpu.Temperature, settings),

            // Absolute sizes have no threshold to cross — a machine with
            // 30 GB in use is not "critical", it's just being used.
            ReadoutMetric.MemUsed => Gigabytes(metric, "RAM", mem.UsedGb),
            ReadoutMetric.GpuVram => Gigabytes(metric, "VRAM", gpu.VramUsedGb),

            _ => new ReadoutValue(metric, "?", string.Empty, "--", "--", AlertSeverity.Nominal)
        };
    }

    /// <summary>Reads several metrics, skipping any duplicates.</summary>
    public static List<ReadoutValue> ReadAll(
        IEnumerable<ReadoutMetric> metrics,
        CpuMetrics cpu,
        MemoryMetrics mem,
        GpuMetrics gpu,
        AppSettings settings)
    {
        var seen = new HashSet<ReadoutMetric>();
        var results = new List<ReadoutValue>();

        foreach (var metric in metrics)
        {
            if (seen.Add(metric))
                results.Add(Read(metric, cpu, mem, gpu, settings));
        }

        return results;
    }

    private static ReadoutValue Percent(
        ReadoutMetric metric, string label, float? value, AppSettings settings, bool isTemperature)
    {
        var severity = Severity(value, settings, isTemperature);
        var compact = value.HasValue ? value.Value.ToString("F0") : "--";
        var full = $"{label} {compact}%";
        return new ReadoutValue(metric, label, "%", compact, full, severity);
    }

    private static ReadoutValue Temperature(
        ReadoutMetric metric, string label, float? value, AppSettings settings)
    {
        var severity = Severity(value, settings, isTemperature: true);
        var compact = value.HasValue ? value.Value.ToString("F0") : "--";
        var full = $"{label} {compact}°C";
        return new ReadoutValue(metric, label, "°C", compact, full, severity);
    }

    private static ReadoutValue Gigabytes(ReadoutMetric metric, string label, float? value)
    {
        // One decimal reads well in the overlay; the tray gets whole GB,
        // because "11.9" will not fit in a 16px icon.
        var full = value.HasValue ? $"{label} {value.Value:F1}GB" : $"{label} --GB";
        var compact = value.HasValue ? value.Value.ToString("F0") : "--";
        return new ReadoutValue(metric, label, "GB", compact, full, AlertSeverity.Nominal);
    }

    private static AlertSeverity Severity(float? value, AppSettings settings, bool isTemperature)
    {
        if (!value.HasValue)
            return AlertSeverity.Nominal;

        return isTemperature
            ? ThresholdEvaluator.Classify(value.Value, settings.TempWarningThreshold, settings.TempCriticalThreshold)
            : ThresholdEvaluator.Classify(value.Value, settings.LoadWarningThreshold, settings.LoadCriticalThreshold);
    }
}
