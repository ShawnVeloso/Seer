using System.Windows;
using System.Windows.Media;

namespace Seer.Controls;

/// <summary>
/// The faint 40px grid behind the panels. Pure drawing — it reads no
/// window state, so it lives here rather than in the window code-behind.
/// </summary>
public static class HudBackground
{
    /// <summary>Spacing between gridlines. Wider reads calmer.</summary>
    private const double CellSize = 48;

    /// <summary>
    /// Gridline opacity. The line is drawn in the design system's border
    /// colour rather than white: white over a near-black background
    /// reads as grey haze and competes with the readouts, whereas the
    /// hairline colour used on every panel edge reads as part of the
    /// same structure. Lower this to quieten it further.
    /// </summary>
    private const byte LineAlpha = 180;

    /// <summary>
    /// Builds the tiling grid brush, or returns null when the effect is
    /// switched off in <see cref="HudConfig"/>. The brush is frozen, so
    /// it's safe to reuse and cheap to render.
    /// </summary>
    public static Brush? CreateGridBrush()
    {
        if (!HudConfig.EnableBackgroundGrid)
            return null;

        // #2A2A2E — the SeerBorder token, matching every panel hairline.
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(LineAlpha, 0x2A, 0x2A, 0x2E)), 1.0);
        pen.Freeze();

        // One cell's worth of lines — top edge and left edge — tiled.
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(new Point(0, 0), new Point(CellSize, 0)));
        geometry.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, CellSize)));
        geometry.Freeze();

        var drawing = new GeometryDrawing(null, pen, geometry);
        drawing.Freeze();

        var brush = new DrawingBrush(drawing)
        {
            Viewport = new Rect(0, 0, CellSize, CellSize),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile
        };
        brush.Freeze();

        return brush;
    }
}
