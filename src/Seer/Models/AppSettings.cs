using System.Collections.Generic;

namespace Seer.Models;

/// <summary>
/// Flat, JSON-serializable settings POCO.
/// Currently holds window geometry only. Future settings (OSD metric
/// selection, threshold values, etc.) add properties here — the
/// SettingsService and serialization layer don't need to change.
/// </summary>
public class AppSettings
{
    public double WindowWidth { get; set; } = 720;
    public double WindowHeight { get; set; } = 520;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;

    /// <summary>
    /// Stored as a string to avoid tight coupling to System.Windows in
    /// this model class. Valid values: "Normal", "Maximized".
    /// Minimized is never persisted — it would relaunch hidden.
    /// </summary>
    public string WindowState { get; set; } = "Normal";

    // --- OSD Settings ---
    public bool ShowOsd { get; set; } = false;
    public bool LockOsd { get; set; } = true;
    public double OsdX { get; set; } = double.NaN;
    public double OsdY { get; set; } = double.NaN;

    // --- Startup ---

    /// <summary>
    /// Mirrors the HKCU Run entry. The registry is the real source of
    /// truth; this is persisted so the Settings UI can show the intended
    /// state without a registry read on every open.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    // --- Readouts (desktop overlay + taskbar tray icons) ---

    /// <summary>
    /// Draw the selected metrics as numbers in the notification area,
    /// one icon per metric, the way MSI Afterburner does.
    /// </summary>
    public bool ShowTrayReadouts { get; set; } = false;

    /// <summary>
    /// Metrics drawn as tray icons. Kept short by default: each entry
    /// costs a slot in the user's notification area.
    /// </summary>
    public List<ReadoutMetric> TrayMetrics { get; set; } = new()
    {
        ReadoutMetric.CpuTemp,
        ReadoutMetric.GpuTemp
    };

    /// <summary>Metrics shown in the desktop overlay strip.</summary>
    public List<ReadoutMetric> OsdMetrics { get; set; } = new()
    {
        ReadoutMetric.CpuLoad,
        ReadoutMetric.CpuTemp,
        ReadoutMetric.GpuLoad,
        ReadoutMetric.GpuTemp,
        ReadoutMetric.MemUsed
    };

    // --- Thresholds (Shared by OSD and Alert logic) ---
    public float LoadWarningThreshold { get; set; } = 85f;
    public float LoadCriticalThreshold { get; set; } = 95f;
    
    public float TempWarningThreshold { get; set; } = 75f;
    public float TempCriticalThreshold { get; set; } = 85f;
}
