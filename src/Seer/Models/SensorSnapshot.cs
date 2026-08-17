namespace Seer.Models;

/// <summary>
/// Snapshot of CPU sensor readings. Nullable floats indicate sensors
/// that may not be available — either because the hardware doesn't
/// expose them, or because admin elevation is required.
///
/// Elevation-gated fields (require admin on AMD Ryzen via WinRing0):
///   Temperature, Clock, Power
///
/// Non-elevation fields (work without admin):
///   TotalLoad
/// </summary>
public record CpuMetrics
{
    /// <summary>CPU package/die temperature in °C. Requires admin.</summary>
    public float? Temperature { get; init; }

    /// <summary>CPU total load across all cores, 0–100%. Works without admin.</summary>
    public float? TotalLoad { get; init; }

    /// <summary>Average/representative core clock in MHz. Requires admin.</summary>
    public float? Clock { get; init; }

    /// <summary>CPU package power draw in watts. Requires admin.</summary>
    public float? Power { get; init; }
}

/// <summary>
/// Snapshot of memory sensor readings. All fields work without admin.
/// </summary>
public record MemoryMetrics
{
    /// <summary>Physical memory currently used, in GB.</summary>
    public float? UsedGb { get; init; }

    /// <summary>Physical memory available, in GB.</summary>
    public float? AvailableGb { get; init; }

    /// <summary>Physical memory load percentage, 0–100%.</summary>
    public float? Load { get; init; }

    /// <summary>Total physical memory in GB (UsedGb + AvailableGb).</summary>
    public float? TotalGb => (UsedGb.HasValue && AvailableGb.HasValue)
        ? UsedGb.Value + AvailableGb.Value
        : null;
}

/// <summary>
/// Snapshot of GPU sensor readings. Generally all fields work without admin.
/// </summary>
public record GpuMetrics
{
    public float? Temperature { get; init; }
    public float? HotSpotTemperature { get; init; }
    public float? FanRpm { get; init; }
    public float? FanPercent { get; init; }
    public float? Load { get; init; }
    public float? Clock { get; init; }
    public float? VramUsedGb { get; init; }
    public float? VramTotalGb { get; init; }
}
