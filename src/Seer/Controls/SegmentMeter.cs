using System;
using System.Windows;
using System.Windows.Media;

namespace Seer.Controls;

/// <summary>How a value maps onto the bar.</summary>
public enum MeterScale
{
    /// <summary>Straight proportion. Right for percentages.</summary>
    Linear,

    /// <summary>
    /// Logarithmic. Right for throughput, where the interesting range spans
    /// four orders of magnitude: on a linear bar scaled to gigabit, ordinary
    /// traffic never lights a single segment, which is why the old disk and
    /// network bars looked permanently empty.
    /// </summary>
    Log
}

/// <summary>
/// A segmented bar meter, drawn rather than composed from elements: the
/// status strip, the disk and network rows and the inline RAM/VRAM meters are
/// all this one control, and each instance is a single OnRender rather than
/// dozens of Borders.
///
/// Everything it paints is a brush supplied from the theme — it resolves no
/// resources itself, so there is no string key to break at runtime.
/// </summary>
public class SegmentMeter : FrameworkElement
{
    // Both of these must hand back a NEW instance every call: a
    // PropertyMetadata may only ever be associated with one property, and
    // sharing one across registrations throws in the static constructor.
    private static FrameworkPropertyMetadata Redraw()
        => new(null, FrameworkPropertyMetadataOptions.AffectsRender);

    private static FrameworkPropertyMetadata Render(object defaultValue)
        => new(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender);

    // ── Value ─────────────────────────────────────────────────────────

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SegmentMeter), Render(0.0));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(nameof(Max), typeof(double), typeof(SegmentMeter), Render(100.0));

    /// <summary>Full-scale value.</summary>
    public double Max
    {
        get => (double)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.Register(nameof(Scale), typeof(MeterScale), typeof(SegmentMeter), Render(MeterScale.Linear));

    public MeterScale Scale
    {
        get => (MeterScale)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public static readonly DependencyProperty LogFloorProperty =
        DependencyProperty.Register(nameof(LogFloor), typeof(double), typeof(SegmentMeter), Render(0.05));

    /// <summary>
    /// The smallest value a log scale still shows. Anything at or below this
    /// reads as empty; the first segment lights just above it.
    /// </summary>
    public double LogFloor
    {
        get => (double)GetValue(LogFloorProperty);
        set => SetValue(LogFloorProperty, value);
    }

    /// <summary>
    /// Peak to mark, or NaN for none. Held by the caller, not by the meter —
    /// a peak that decays is session state, not a drawing concern.
    /// </summary>
    public static readonly DependencyProperty PeakProperty =
        DependencyProperty.Register(nameof(Peak), typeof(double), typeof(SegmentMeter), Render(double.NaN));

    public double Peak
    {
        get => (double)GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
    }

    // ── Thresholds ────────────────────────────────────────────────────

    public static readonly DependencyProperty WarnThresholdProperty =
        DependencyProperty.Register(nameof(WarnThreshold), typeof(double), typeof(SegmentMeter), Render(double.NaN));

    public double WarnThreshold
    {
        get => (double)GetValue(WarnThresholdProperty);
        set => SetValue(WarnThresholdProperty, value);
    }

    public static readonly DependencyProperty CritThresholdProperty =
        DependencyProperty.Register(nameof(CritThreshold), typeof(double), typeof(SegmentMeter), Render(double.NaN));

    public double CritThreshold
    {
        get => (double)GetValue(CritThresholdProperty);
        set => SetValue(CritThresholdProperty, value);
    }

    public static readonly DependencyProperty ShowTicksProperty =
        DependencyProperty.Register(nameof(ShowTicks), typeof(bool), typeof(SegmentMeter), Render(false));

    /// <summary>Draw hairlines where the thresholds sit, so the bar has a scale.</summary>
    public bool ShowTicks
    {
        get => (bool)GetValue(ShowTicksProperty);
        set => SetValue(ShowTicksProperty, value);
    }

    // ── Appearance ────────────────────────────────────────────────────

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(SegmentMeter), Redraw());

    /// <summary>The channel colour — cyan for CPU, blue for memory, violet for GPU.</summary>
    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(SegmentMeter), Redraw());

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public static readonly DependencyProperty WarnBrushProperty =
        DependencyProperty.Register(nameof(WarnBrush), typeof(Brush), typeof(SegmentMeter), Redraw());

    public Brush? WarnBrush
    {
        get => (Brush?)GetValue(WarnBrushProperty);
        set => SetValue(WarnBrushProperty, value);
    }

    public static readonly DependencyProperty CritBrushProperty =
        DependencyProperty.Register(nameof(CritBrush), typeof(Brush), typeof(SegmentMeter), Redraw());

    public Brush? CritBrush
    {
        get => (Brush?)GetValue(CritBrushProperty);
        set => SetValue(CritBrushProperty, value);
    }

    public static readonly DependencyProperty PeakBrushProperty =
        DependencyProperty.Register(nameof(PeakBrush), typeof(Brush), typeof(SegmentMeter), Redraw());

    public Brush? PeakBrush
    {
        get => (Brush?)GetValue(PeakBrushProperty);
        set => SetValue(PeakBrushProperty, value);
    }

    public static readonly DependencyProperty SegmentWidthProperty =
        DependencyProperty.Register(nameof(SegmentWidth), typeof(double), typeof(SegmentMeter), Render(5.0));

    public double SegmentWidth
    {
        get => (double)GetValue(SegmentWidthProperty);
        set => SetValue(SegmentWidthProperty, value);
    }

    public static readonly DependencyProperty SegmentGapProperty =
        DependencyProperty.Register(nameof(SegmentGap), typeof(double), typeof(SegmentMeter), Render(2.0));

    public double SegmentGap
    {
        get => (double)GetValue(SegmentGapProperty);
        set => SetValue(SegmentGapProperty, value);
    }

    // ── Drawing ───────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        var step = SegmentWidth + SegmentGap;
        if (step <= 0)
            return;

        var count = Math.Max(1, (int)((width + SegmentGap) / step));
        var filled = (int)Math.Round(Fraction(Value) * count);

        var warn = Fraction(WarnThreshold);
        var crit = Fraction(CritThreshold);

        for (var i = 0; i < count; i++)
        {
            // Where this segment's middle sits on the scale, so the colour
            // change lands on the segment that crosses the threshold.
            var at = (i + 0.5) / count;

            var brush = i < filled
                ? (crit > 0 && at >= crit ? CritBrush ?? Fill
                    : warn > 0 && at >= warn ? WarnBrush ?? Fill
                    : Fill)
                : TrackBrush;

            if (brush != null)
                dc.DrawRectangle(brush, null, new Rect(i * step, 0, SegmentWidth, height));
        }

        if (ShowTicks)
        {
            DrawTick(dc, warn, WarnBrush, width, height);
            DrawTick(dc, crit, CritBrush, width, height);
        }

        if (!double.IsNaN(Peak) && PeakBrush != null)
        {
            var x = Math.Clamp(Fraction(Peak) * width, 0, Math.Max(0, width - 2));
            dc.DrawRectangle(PeakBrush, null, new Rect(x, -2, 2, height + 4));
        }
    }

    private static void DrawTick(DrawingContext dc, double fraction, Brush? brush, double width, double height)
    {
        if (fraction <= 0 || fraction >= 1 || brush == null)
            return;

        var x = Math.Clamp(fraction * width, 0, Math.Max(0, width - 1));
        dc.DrawRectangle(brush, null, new Rect(x, -2, 1, height + 4));
    }

    /// <summary>Maps a value onto 0..1 for the current scale.</summary>
    private double Fraction(double value)
    {
        if (double.IsNaN(value) || Max <= 0)
            return 0;

        if (value <= 0)
            return 0;

        if (Scale == MeterScale.Linear)
            return Math.Clamp(value / Max, 0, 1);

        var floor = LogFloor > 0 ? LogFloor : 0.05;
        if (value <= floor)
            return 0;

        // log10(1 + v/floor) / log10(1 + max/floor): 0 stays 0, full scale
        // stays 1, and a trickle still lights the first segment.
        var span = Math.Log10(1 + (Max / floor));
        if (span <= 0)
            return 0;

        return Math.Clamp(Math.Log10(1 + (value / floor)) / span, 0, 1);
    }
}
