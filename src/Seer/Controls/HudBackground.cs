using System.Windows;
using System.Windows.Media;

namespace Seer.Controls;

/// <summary>
/// The faint 40px grid behind the panels. Pure drawing — it reads no
/// window state, so it lives here rather than in the window code-behind.
/// </summary>
public static class HudBackground
{
    private const double CellSize = 40;

    /// <summary>
    /// Builds the tiling grid brush, or returns null when the effect is
    /// switched off in <see cref="HudConfig"/>. The brush is frozen, so
    /// it's safe to reuse and cheap to render.
    /// </summary>
    public static Brush? CreateGridBrush()
    {
        if (!HudConfig.EnableBackgroundGrid)
            return null;

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0);
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
