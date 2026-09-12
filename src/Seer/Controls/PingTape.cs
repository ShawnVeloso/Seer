using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Seer.Models;

namespace Seer.Controls;

/// <summary>
/// One bar per ping reply, newest on the right, with lost packets drawn as
/// red hairlines where a bar would have been.
///
/// Latency as a shape rather than a number: an average of 40ms that swings
/// between 10 and 200 and a steady 40ms read identically in the statistics,
/// and completely differently here.
///
/// Drawn rather than built from elements, and drawn with rectangles rather
/// than block characters — the eighth-block glyphs a text sparkline would
/// need are outside the character set every candidate monospace face shares.
/// </summary>
public class PingTape : FrameworkElement
{
    /// <summary>A reply time of this value means the request was lost.</summary>
    public const long Timeout = PingSnapshot.Lost;

    private static FrameworkPropertyMetadata Redraw(object? defaultValue = null)
        => new(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender);

    public static readonly DependencyProperty SamplesProperty =
        DependencyProperty.Register(nameof(Samples), typeof(IReadOnlyList<long>), typeof(PingTape), Redraw());

    /// <summary>Recent round trips in milliseconds, oldest first; <see cref="Timeout"/> for a loss.</summary>
    public IReadOnlyList<long>? Samples
    {
        get => (IReadOnlyList<long>?)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public static readonly DependencyProperty MaxMsProperty =
        DependencyProperty.Register(nameof(MaxMs), typeof(double), typeof(PingTape), Redraw(60.0));

    /// <summary>Full-height latency. Anything slower is clamped to the top.</summary>
    public double MaxMs
    {
        get => (double)GetValue(MaxMsProperty);
        set => SetValue(MaxMsProperty, value);
    }

    public static readonly DependencyProperty BarWidthProperty =
        DependencyProperty.Register(nameof(BarWidth), typeof(double), typeof(PingTape), Redraw(4.0));

    public double BarWidth
    {
        get => (double)GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    public static readonly DependencyProperty BarGapProperty =
        DependencyProperty.Register(nameof(BarGap), typeof(double), typeof(PingTape), Redraw(1.0));

    public double BarGap
    {
        get => (double)GetValue(BarGapProperty);
        set => SetValue(BarGapProperty, value);
    }

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(PingTape), Redraw());

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public static readonly DependencyProperty LossBrushProperty =
        DependencyProperty.Register(nameof(LossBrush), typeof(Brush), typeof(PingTape), Redraw());

    public Brush? LossBrush
    {
        get => (Brush?)GetValue(LossBrushProperty);
        set => SetValue(LossBrushProperty, value);
    }

    public static readonly DependencyProperty LatestBrushProperty =
        DependencyProperty.Register(nameof(LatestBrush), typeof(Brush), typeof(PingTape), Redraw());

    /// <summary>The newest reply, picked out so "now" is identifiable.</summary>
    public Brush? LatestBrush
    {
        get => (Brush?)GetValue(LatestBrushProperty);
        set => SetValue(LatestBrushProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var samples = Samples;
        if (samples == null || samples.Count == 0)
            return;

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        var advance = BarWidth + BarGap;
        if (advance <= 0)
            return;

        // Newest on the right: fill from the right edge back, and stop when
        // the strip runs out rather than squeezing the bars.
        var capacity = Math.Max(1, (int)((width + BarGap) / advance));
        var count = Math.Min(capacity, samples.Count);
        var first = samples.Count - count;

        for (var i = 0; i < count; i++)
        {
            var value = samples[first + i];
            var x = width - ((count - i) * advance) + BarGap;
            var isLatest = first + i == samples.Count - 1;

            if (value == Timeout)
            {
                // A loss is a full-height hairline in the gap the reply would
                // have filled: visible at a glance, and unmistakably not a
                // very fast reply.
                if (LossBrush != null)
                    dc.DrawRectangle(LossBrush, null, new Rect(x + ((BarWidth - 1) / 2), 0, 1, height));
                continue;
            }

            var brush = isLatest ? LatestBrush ?? Fill : Fill;
            if (brush == null)
                continue;

            var scale = MaxMs > 0 ? value / MaxMs : 0;
            var barHeight = Math.Max(1, Math.Clamp(scale, 0.05, 1.0) * height);

            dc.DrawRectangle(brush, null, new Rect(x, height - barHeight, BarWidth, barHeight));
        }
    }
}
