namespace Seer;

/// <summary>
/// Centralized configuration flags for aesthetic HUD effects.
/// Each effect can be toggled independently for rapid prototyping and testing.
/// </summary>
public static class HudConfig
{
    public static bool EnableChartGlow { get; set; } = true;
    public static bool EnablePanelBrackets { get; set; } = true;
    public static bool EnableBackgroundGrid { get; set; } = true;
    public static bool EnableHoverGlow { get; set; } = true;
}
