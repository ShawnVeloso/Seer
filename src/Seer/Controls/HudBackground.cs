using System.Windows;
using System.Windows.Media;

namespace Seer.Controls;

/// <summary>
/// The faint dot lattice behind the panels. Pure drawing — it reads no
/// window state, so it lives here rather than in the window code-behind.
/// </summary>
public static class HudBackground
{
    /// <summary>
    /// Spacing between dots. Tighter than the old 48px gridlines, because a
    /// dot covers far less area than a line and a sparse lattice stops
    /// reading as a field.
    /// </summary>
    private const double CellSize = 24;

    /// <summary>Size of each dot. Small enough to stay texture, not pattern.</summary>
    private const double DotSize = 1.5;

    /// <summary>
    /// Dot opacity. Dots are drawn in the dim text colour rather than the
    /// panel hairline: a line reads at a much lower alpha than a 1.5px dot
    /// does, so the lattice needs a slightly brighter ink to register at all.
    /// Lower this to quieten it further.
    /// </summary>
    private const byte DotAlpha = 110;

    /// <summary>
    /// Builds the tiling lattice brush, or returns null when the effect is
    /// switched off in <see cref="HudConfig"/>. The brush is frozen, so
    /// it's safe to reuse and cheap to render.
    /// </summary>
    public static Brush? CreateGridBrush()
    {
        if (!HudConfig.EnableBackgroundGrid)
            return null;

        // #6A6A70 — the SeerTextDim token, at low alpha.
        var fill = new SolidColorBrush(Color.FromArgb(DotAlpha, 0x6A, 0x6A, 0x70));
        fill.Freeze();

        // One dot per cell, filled rather than stroked: a stroked shape this
        // small lands on half-pixels and shimmers when the window moves.
        var geometry = new RectangleGeometry(new Rect(0, 0, DotSize, DotSize));
        geometry.Freeze();

        var drawing = new GeometryDrawing(fill, null, geometry);
        drawing.Freeze();

        // Viewbox AND viewport, both absolute and both one cell, so the tile
        // maps 1:1 and the dot stays 1.5px. Without an explicit viewbox a
        // DrawingBrush stretches its content to fill the tile — which turns
        // this lattice into a solid grey wash over the whole window.
        var brush = new DrawingBrush(drawing)
        {
            Viewbox = new Rect(0, 0, CellSize, CellSize),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, CellSize, CellSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            TileMode = TileMode.Tile
        };
        brush.Freeze();

        return brush;
    }
}
