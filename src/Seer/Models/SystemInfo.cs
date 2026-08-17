namespace Seer.Models;

/// <summary>
/// Immutable snapshot of static system hardware info, fetched once at startup.
/// None of these values change during a running session — no polling needed.
/// </summary>
public record SystemInfo
{
    /// <summary>Motherboard manufacturer and product name.</summary>
    public string MotherboardName { get; init; } = "Unknown";

    /// <summary>BIOS/UEFI firmware version string.</summary>
    public string BiosVersion { get; init; } = "Unknown";

    /// <summary>BIOS release date, formatted as yyyy-MM-dd.</summary>
    public string BiosDate { get; init; } = "Unknown";

    /// <summary>Full CPU model name (e.g. "AMD Ryzen 7 5700X3D 8-Core Processor").</summary>
    public string CpuModel { get; init; } = "Unknown";

    /// <summary>Physical core count.</summary>
    public int CpuCores { get; init; }

    /// <summary>Logical thread count.</summary>
    public int CpuThreads { get; init; }

    /// <summary>Human-readable RAM summary, e.g. "32 GB DDR4 @ 3200 MHz".</summary>
    public string RamSummary { get; init; } = "Unknown";

    /// <summary>Number of DIMM slots populated.</summary>
    public int RamSlotsUsed { get; init; }

    /// <summary>Total DIMM slots on the motherboard.</summary>
    public int RamSlotsTotal { get; init; }

    /// <summary>GPU model name from LibreHardwareMonitorLib.</summary>
    public string GpuModel { get; init; } = "Unknown";
}
