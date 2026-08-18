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
}
