using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Seer.Controls;

/// <summary>
/// A rectangle with its top-right corner cut at 45°, drawn as one closed
/// figure so that the fill and the outline are the same shape.
///
/// A WPF <see cref="System.Windows.Controls.Border"/> can round a corner but
/// cannot cut one, and laying a triangle over a Border leaves the border line
/// running underneath the cut. Deriving from <see cref="Shape"/> keeps fill
/// and stroke in a single geometry, so the outline follows the chamfer.
///
/// The geometry is cached per (size, chamfer, stroke thickness): panels
/// re-render on every poll, and rebuilding a <see cref="StreamGeometry"/> each
/// time would allocate constantly for a shape that only changes on resize.
/// </summary>
public class ChamferShape : Shape
{
    public static readonly DependencyProperty ChamferProperty =
        DependencyProperty.Register(
            nameof(Chamfer),
            typeof(double),
            typeof(ChamferShape),
            new FrameworkPropertyMetadata(
                10.0,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure,
                OnChamferChanged));

    /// <summary>Length of the 45° cut, in device-independent pixels.</summary>
    public double Chamfer
    {
        get => (double)GetValue(ChamferProperty);
        set => SetValue(ChamferProperty, value);
    }

    private Geometry? _cached;
    private Size _cachedSize;
    private double _cachedChamfer = -1;
    private double _cachedThickness = -1;

    private static void OnChamferChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ChamferShape)d)._cached = null;

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        _cached = null;
    }

    protected override Geometry DefiningGeometry => BuildGeometry();

    private Geometry BuildGeometry()
    {
        var size = RenderSize;
        var thickness = StrokeThickness;

        if (_cached != null
            && size == _cachedSize
            && Chamfer == _cachedChamfer
            && thickness == _cachedThickness)
        {
            return _cached;
        }

        // Inset by half the stroke, so the line sits inside the element's
        // bounds rather than straddling the edge and being clipped in half.
        var inset = thickness / 2.0;
        var right = size.Width - inset;
        var bottom = size.Height - inset;

        // First layout pass: the element has no size yet, so there is
        // nothing to draw. OnRenderSizeChanged brings us back.
        if (right <= inset || bottom <= inset)
            return Geometry.Empty;

        // A cut larger than the panel would fold the top edge back on itself.
        var cut = Math.Max(0, Math.Min(Chamfer, Math.Min(right - inset, bottom - inset) / 2.0));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(inset, inset), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(right - cut, inset), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(right, inset + cut), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(right, bottom), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(inset, bottom), isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();

        _cached = geometry;
        _cachedSize = size;
        _cachedChamfer = Chamfer;
        _cachedThickness = thickness;
        return geometry;
    }
}
