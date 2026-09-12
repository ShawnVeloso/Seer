using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Seer.Controls;

/// <summary>
/// The per-core load block, drawn rather than composed from elements.
///
/// Threads read top-to-bottom down each column, so the number of rows has to
/// be known before any cell is placed — which no wrapping panel can do. The
/// previous version solved that by composing each row as one string and
/// measuring the panel's ActualWidth from the poll, which meant the column
/// count was always one tick stale and simply guessed on the first tick.
/// Doing the same arithmetic in MeasureOverride puts it in layout, where it
/// belongs: resizing reflows immediately.
///
/// Every cell is exactly <see cref="CellChars"/> characters of a monospace
/// face, so the columns align by construction rather than by layout.
/// </summary>
public class CoreMatrix : FrameworkElement
{
    /// <summary>Characters in one cell: " 0[|||  39%]".</summary>
    public const int CellChars = 13;

    /// <summary>Gap between columns, in characters.</summary>
    public const int GapChars = 2;

    /// <summary>Characters before the bar segments start: two index digits and "[".</summary>
    private const int BarOffsetChars = 3;

    /// <summary>Bar segments per cell; each one is worth 20%.</summary>
    private const int BarSegments = 5;

    /// <summary>Used before the element has a width to measure against.</summary>
    private const int DefaultColumns = 4;

    private (string Name, float Load)[] _cores = Array.Empty<(string, float)>();
    private double[] _peaks = Array.Empty<double>();

    private Typeface? _typeface;
    private double _charWidth;
    private double _pixelsPerDip;

    // ── Appearance ────────────────────────────────────────────────────

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(CoreMatrix),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnFontChanged));

    public FontFamily? FontFamily
    {
        get => (FontFamily?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(CoreMatrix),
            new FrameworkPropertyMetadata(12.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnFontChanged));

    /// <summary>
    /// Size of the cell text. Lives here alone now — it used to be duplicated
    /// between the XAML item template and a const in the window's code, with
    /// a comment warning that the two had to match.
    /// </summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(CoreMatrix),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Colour of an idle core, and of every core when heat is off.</summary>
    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly DependencyProperty HeatMidProperty =
        DependencyProperty.Register(nameof(HeatMid), typeof(Brush), typeof(CoreMatrix),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>A core doing real work, from 30%.</summary>
    public Brush? HeatMid
    {
        get => (Brush?)GetValue(HeatMidProperty);
        set => SetValue(HeatMidProperty, value);
    }

    public static readonly DependencyProperty HeatHighProperty =
        DependencyProperty.Register(nameof(HeatHigh), typeof(Brush), typeof(CoreMatrix),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>A core near saturation, from 70%.</summary>
    public Brush? HeatHigh
    {
        get => (Brush?)GetValue(HeatHighProperty);
        set => SetValue(HeatHighProperty, value);
    }

    public static readonly DependencyProperty PeakBrushProperty =
        DependencyProperty.Register(nameof(PeakBrush), typeof(Brush), typeof(CoreMatrix),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Colour of the peak-hold tick.</summary>
    public Brush? PeakBrush
    {
        get => (Brush?)GetValue(PeakBrushProperty);
        set => SetValue(PeakBrushProperty, value);
    }

    private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var matrix = (CoreMatrix)d;
        matrix._typeface = null;
        matrix._charWidth = 0;
    }

    /// <summary>Height of one row of cells.</summary>
    private double LineHeight => Math.Ceiling(FontSize * 1.5);

    // ── Data ──────────────────────────────────────────────────────────

    /// <summary>
    /// The whole per-poll cost: store and invalidate. Both arrays are held by
    /// the caller, which owns the peak decay.
    /// </summary>
    public void SetCores((string Name, float Load)[] cores, double[] peaks)
    {
        _cores = cores ?? Array.Empty<(string, float)>();
        _peaks = peaks ?? Array.Empty<double>();

        InvalidateMeasure();
        InvalidateVisual();
    }

    // ── Layout ────────────────────────────────────────────────────────

    /// <summary>
    /// How many columns fit a given width. Pure, static and public so the
    /// awkward cases can be tested without standing up WPF.
    /// </summary>
    public static int ColumnsThatFit(double width, double charWidth, int cellCount)
    {
        if (cellCount <= 0)
            return 1;

        if (charWidth <= 0 || double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
            return Math.Min(DefaultColumns, cellCount);

        // n columns occupy n*cell + (n-1)*gap characters, so the number that
        // fits is (chars + gap) / (cell + gap).
        var availableChars = (int)(width / charWidth);
        var columns = (availableChars + GapChars) / (CellChars + GapChars);

        return Math.Clamp(columns, 1, cellCount);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_cores.Length == 0)
            return new Size(0, 0);

        var charWidth = CharWidth();
        var columns = ColumnsThatFit(availableSize.Width, charWidth, _cores.Length);
        var rows = (int)Math.Ceiling(_cores.Length / (double)columns);

        var width = ((columns * CellChars) + ((columns - 1) * GapChars)) * charWidth;
        var height = rows * LineHeight;

        // Never ask for more than offered: the panel is inside a ScrollViewer
        // whose width is the constraint, not a suggestion.
        if (!double.IsInfinity(availableSize.Width))
            width = Math.Min(width, availableSize.Width);

        return new Size(width, height);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        _pixelsPerDip = newDpi.PixelsPerDip;
        _charWidth = 0;
        InvalidateMeasure();
    }

    // ── Drawing ───────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_cores.Length == 0 || Foreground == null)
            return;

        var charWidth = CharWidth();
        if (charWidth <= 0)
            return;

        var columns = ColumnsThatFit(ActualWidth, charWidth, _cores.Length);
        var rows = (int)Math.Ceiling(_cores.Length / (double)columns);
        var cellAdvance = (CellChars + GapChars) * charWidth;
        var lineHeight = LineHeight;
        var typeface = ResolveTypeface();

        for (var column = 0; column < columns; column++)
        {
            for (var row = 0; row < rows; row++)
            {
                // Walk down each column before moving right.
                var index = (column * rows) + row;
                if (index >= _cores.Length)
                    break;

                var load = _cores[index].Load;
                var x = column * cellAdvance;
                var y = row * lineHeight;

                var text = new FormattedText(
                    FormatCell(index, load),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FontSize,
                    HeatBrush(load),
                    _pixelsPerDip);

                dc.DrawText(text, new Point(x, y));
                DrawPeakTick(dc, index, load, x, y, charWidth, lineHeight);
            }
        }
    }

    /// <summary>
    /// A hairline where this core's recent peak sat, drawn only when the peak
    /// is at least one segment above the current reading — otherwise every
    /// cell would carry a tick that says nothing.
    /// </summary>
    private void DrawPeakTick(DrawingContext dc, int index, float load, double x, double y, double charWidth, double lineHeight)
    {
        if (!HudConfig.EnablePeakHold || PeakBrush == null || index >= _peaks.Length)
            return;

        var current = Segments(load);
        var peak = Segments(_peaks[index]);
        if (peak <= current || peak <= 0)
            return;

        var tickX = x + ((BarOffsetChars + peak - 1) * charWidth) + (charWidth / 2) - 0.5;
        dc.DrawRectangle(PeakBrush, null, new Rect(tickX, y + 3, 1, Math.Max(2, lineHeight - 8)));
    }

    private static int Segments(double load)
        => Math.Clamp((int)Math.Round(load / 20.0), 0, BarSegments);

    /// <summary>
    /// One cell: index, bar, whole-number percentage — exactly
    /// <see cref="CellChars"/> characters for any index and any percentage,
    /// which is what keeps the columns lined up.
    /// </summary>
    private static string FormatCell(int index, float load)
    {
        var bars = Segments(load);
        return $"{index,2}[{new string('|', bars).PadRight(BarSegments)}{load,3:F0}%]";
    }

    private Brush? HeatBrush(float load)
    {
        if (!HudConfig.EnableCoreHeat)
            return Foreground;

        return load >= 70 ? HeatHigh ?? Foreground
            : load >= 30 ? HeatMid ?? Foreground
            : Foreground;
    }

    private Typeface ResolveTypeface()
        => _typeface ??= new Typeface(
            FontFamily ?? new FontFamily("Consolas"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

    /// <summary>
    /// Width of one character, measured once and cached. The font cannot
    /// change while the app runs, but the DPI can.
    /// </summary>
    private double CharWidth()
    {
        if (_charWidth > 0)
            return _charWidth;

        if (_pixelsPerDip <= 0)
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var measured = new FormattedText(
            "0",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            ResolveTypeface(),
            FontSize,
            Brushes.White,
            _pixelsPerDip);

        _charWidth = measured.Width;
        return _charWidth;
    }
}
