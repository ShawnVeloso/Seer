namespace Seer.Models;

/// <summary>
/// A single value that can be surfaced outside the main window — in the
/// desktop overlay, or as a number drawn into a taskbar tray icon.
///
/// Deliberately a small, curated set rather than every sensor Seer reads.
/// A tray icon is 16 pixels wide and the overlay is meant to be glanced
/// at, so both are for the handful of values worth watching constantly.
/// </summary>
public enum ReadoutMetric
{
    CpuLoad,
    CpuTemp,
    GpuLoad,
    GpuTemp,
    MemLoad,
    MemUsed,
    GpuVram
}
