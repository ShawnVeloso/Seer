namespace Seer.Models;

/// <summary>
/// The per-metric severities behind a single evaluation.
///
/// The evaluator has always computed these and returned only their maximum,
/// which is all the status badge needs. Panels need more than that: to colour
/// its own edge, a panel has to know whether *it* is the one holding the
/// badge at WARNING.
/// </summary>
public record SeverityBreakdown(
    AlertSeverity CpuLoad,
    AlertSeverity CpuTemp,
    AlertSeverity MemLoad,
    AlertSeverity GpuLoad,
    AlertSeverity GpuTemp)
{
    public static readonly SeverityBreakdown Nominal = new(
        AlertSeverity.Nominal,
        AlertSeverity.Nominal,
        AlertSeverity.Nominal,
        AlertSeverity.Nominal,
        AlertSeverity.Nominal);

    /// <summary>Worst of the CPU readings.</summary>
    public AlertSeverity Cpu => Worst(CpuLoad, CpuTemp);

    /// <summary>Memory has one reading, but the panel asks the same question.</summary>
    public AlertSeverity Mem => MemLoad;

    /// <summary>Worst of the GPU readings.</summary>
    public AlertSeverity Gpu => Worst(GpuLoad, GpuTemp);

    private static AlertSeverity Worst(AlertSeverity a, AlertSeverity b) => a > b ? a : b;
}
