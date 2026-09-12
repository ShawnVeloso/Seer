using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Seer.Controls;

public partial class TrendChart : UserControl
{
    /// <summary>Width of the plot in samples; one sample per poll, so 120 seconds.</summary>
    private const int MaxPoints = 120;

    /// <summary>A gridline every 30 seconds of wall clock, on the :00 and :30.</summary>
    private const int GraticuleSeconds = 30;

    private IEnumerable<float> _data = Enumerable.Empty<float>();

    public TrendChart()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Chart name. Sits on the panel's border line, so it costs no plot area.
    /// </summary>
    public string Title
    {
        get => ChartPanel.Header;
        set => ChartPanel.Header = value;
    }

    public Brush Stroke
    {
        get => ChartLine.Stroke;
        set
        {
            ChartLine.Stroke = value;

            // The halo is the same brush drawn wider and fainter, rather than
            // a DropShadowEffect: an effect forces an intermediate render
            // target every time the chart redraws, which is once a second for
            // the life of the app.
            ChartHalo.Stroke = HudConfig.EnableChartHalo ? value : null;
        }
    }

    /// <summary>
    /// Highest value seen this session. Drawn as a faint dashed line, so a
    /// peak that has already scrolled off the plot is still legible.
    /// </summary>
    public float? SessionPeak { get; set; }

    /// <summary>
    /// Session low / mean / high, shown on the border line. Padded to a fixed
    /// width: the header re-measures whenever this string changes length, and
    /// it changes every poll.
    /// </summary>
    public void SetSessionStats(float? min, float? average, float? max)
    {
        if (!HudConfig.EnableSessionStats || min is null || average is null || max is null)
        {
            ChartPanel.Meta = string.Empty;
            return;
        }

        ChartPanel.Meta = $"{min.Value,3:F0} {average.Value,3:F0} {max.Value,3:F0}";
    }

    public void UpdateData(IEnumerable<float> data)
    {
        _data = data;
        Redraw();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    private void Redraw()
    {
        var dataList = _data as IList<float> ?? _data.ToList();

        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;

        if (dataList.Count == 0 || width <= 0 || height <= 0)
        {
            Clear();
            return;
        }

        // Plotted right-to-left: the newest sample sits at the right edge, and
        // a partly-filled history starts partway across rather than stretching.
        var stepX = width / (MaxPoints - 1);
        var startIndex = MaxPoints - dataList.Count;

        var points = new PointCollection(dataList.Count);
        for (var i = 0; i < dataList.Count; i++)
        {
            var value = Math.Clamp(dataList[i], 0f, 100f);
            points.Add(new Point(
                (startIndex + i) * stepX,
                height - (value / 100.0 * height)));
        }

        // Frozen, then shared by both polylines: one allocation per redraw
        // instead of two identical ones.
        points.Freeze();
        ChartLine.Points = points;
        ChartHalo.Points = points;

        DrawWriteHead(points[^1], dataList[^1]);
        DrawGraticule(dataList.Count, startIndex, stepX, height);
        DrawSessionPeak(width, height);
    }

    private void Clear()
    {
        var empty = new PointCollection();
        empty.Freeze();

        ChartLine.Points = empty;
        ChartHalo.Points = empty;
        Graticule.Data = null;
        WriteHead.Visibility = Visibility.Collapsed;
        WriteHeadLabel.Text = string.Empty;
        SessionPeakLine.Visibility = Visibility.Collapsed;
    }

    private void DrawWriteHead(Point head, float value)
    {
        if (!HudConfig.EnableChartWriteHead)
        {
            WriteHead.Visibility = Visibility.Collapsed;
            WriteHeadLabel.Text = string.Empty;
            return;
        }

        Canvas.SetLeft(WriteHead, head.X - (WriteHead.Width / 2));
        Canvas.SetTop(WriteHead, head.Y - (WriteHead.Height / 2));
        WriteHead.Visibility = Visibility.Visible;
        WriteHeadLabel.Text = $"{Math.Clamp(value, 0f, 100f):F0}%";
    }

    /// <summary>
    /// Vertical lines on the wall-clock :00 and :30. No timestamps are stored:
    /// one sample is one poll, so a sample's age in seconds is its distance
    /// from the end of the buffer. The lines scroll left with the data, which
    /// is what makes "how long ago" readable.
    /// </summary>
    private void DrawGraticule(int count, int startIndex, double stepX, double height)
    {
        if (!HudConfig.EnableTimeGraticule)
        {
            Graticule.Data = null;
            return;
        }

        var now = DateTime.Now;
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < count; i++)
            {
                var secondsAgo = count - 1 - i;
                if (now.AddSeconds(-secondsAgo).Second % GraticuleSeconds != 0)
                    continue;

                var x = (startIndex + i) * stepX;
                ctx.BeginFigure(new Point(x, 0), isFilled: false, isClosed: false);
                ctx.LineTo(new Point(x, height), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        Graticule.Data = geometry;
    }

    private void DrawSessionPeak(double width, double height)
    {
        if (!HudConfig.EnableSessionPeakLine || SessionPeak is not float peak || peak <= 0)
        {
            SessionPeakLine.Visibility = Visibility.Collapsed;
            return;
        }

        var y = height - (Math.Clamp(peak, 0f, 100f) / 100.0 * height);
        SessionPeakLine.X1 = 0;
        SessionPeakLine.X2 = width;
        SessionPeakLine.Y1 = y;
        SessionPeakLine.Y2 = y;
        SessionPeakLine.Visibility = Visibility.Visible;
    }
}
