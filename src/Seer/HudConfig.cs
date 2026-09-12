using System.Windows;

namespace Seer;

/// <summary>
/// Centralized configuration flags for aesthetic HUD effects.
/// Each effect can be toggled independently for rapid prototyping and testing.
///
/// Two kinds of flag live here, and they answer different questions:
///
///   <c>Enable*</c>       — does this detail exist at all?
///   <c>MotionAllowed</c> — may it move between polls?
///
/// Every living detail renders its resting state from the poll regardless of
/// <see cref="MotionAllowed"/>; only the transient blink or fade is skipped.
/// So with Windows animations switched off the heartbeat still redraws each
/// second, and still turns amber when a poll was late — it just doesn't fade.
/// </summary>
public static class HudConfig
{
    // ── Chrome ──
    public static bool EnableChartHalo { get; set; } = true;
    public static bool EnablePanelBrackets { get; set; } = true;
    public static bool EnableBackgroundGrid { get; set; } = true;
    public static bool EnableHoverGlow { get; set; } = true;
    public static bool EnableChamferedCorner { get; set; } = true;
    public static bool EnableIndexTabs { get; set; } = true;

    // ── Living details ──
    public static bool EnableHeartbeatDot { get; set; } = true;
    public static bool EnableChartWriteHead { get; set; } = true;
    public static bool EnablePeakHold { get; set; } = true;
    public static bool EnableSeverityEdges { get; set; } = true;
    public static bool EnableIoActivityLeds { get; set; } = true;
    public static bool EnableProcessRankArrows { get; set; } = true;
    public static bool EnableStatusLine { get; set; } = true;
    public static bool EnableTimeGraticule { get; set; } = true;
    public static bool EnableCoreHeat { get; set; } = true;
    public static bool EnablePingTape { get; set; } = true;
    public static bool EnableLaunchLog { get; set; } = true;

    // ── Readouts that add information rather than movement ──
    public static bool EnableTrendArrows { get; set; } = true;
    public static bool EnableSessionStats { get; set; } = true;
    public static bool EnableSessionPeakLine { get; set; } = true;
    public static bool EnableRelativeEventTime { get; set; } = true;
    public static bool EnableInlineMeters { get; set; } = true;

    /// <summary>
    /// Mirrors the Windows "Show animations in Windows" setting. Read once at
    /// startup and refreshed when the system reports a change, rather than
    /// queried per tick.
    /// </summary>
    public static bool SystemAnimationsEnabled { get; private set; } = SystemParameters.ClientAreaAnimation;

    /// <summary>Manual override, independent of the system setting.</summary>
    public static bool ForceStatic { get; set; }

    /// <summary>
    /// The single gate every animation goes through. Nothing else in the app
    /// should consult the system setting directly.
    /// </summary>
    public static bool MotionAllowed => SystemAnimationsEnabled && !ForceStatic;

    /// <summary>
    /// Re-reads the system animation setting. Called from the window's
    /// <c>SystemParameters.StaticPropertyChanged</c> handler.
    /// </summary>
    public static void RefreshSystemAnimationSetting()
        => SystemAnimationsEnabled = SystemParameters.ClientAreaAnimation;
}
