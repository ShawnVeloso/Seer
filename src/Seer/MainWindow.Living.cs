using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Seer.Controls;
using Seer.Models;
using Seer.Services;

namespace Seer;

/// <summary>
/// The parts of the window that move.
///
/// Kept apart from MainWindow.Panels.cs, which renders readings, because
/// these render the act of reading: the heartbeat, the status line, the
/// activity lights. Every one of them is driven by the poll — nothing here
/// runs on a clock of its own, and nothing repeats forever. When the data
/// stops, the motion stops, which is the point: a frozen detail means Seer
/// has stopped sampling.
/// </summary>
public partial class MainWindow
{
    /// <summary>Throughput below this reads as idle rather than as activity.</summary>
    private const double ActivityFloor = 0.05;

    private void UpdateLivingDetails()
    {
        UpdateHeartbeat();
        UpdateStatusLine();
    }

    /// <summary>
    /// One blink per completed poll. The lateness it reports is measured
    /// after the fact — the dispatcher cannot warn about a delay it is
    /// currently causing — so a late poll colours the blink that follows it.
    /// </summary>
    private void UpdateHeartbeat()
    {
        if (!HudConfig.EnableHeartbeatDot)
        {
            HeartbeatDot.Visibility = Visibility.Collapsed;
            PollLateText.Text = string.Empty;
            return;
        }

        HeartbeatDot.Visibility = Visibility.Visible;

        var late = _pollClock.WasLate;
        HeartbeatDot.Fill = late ? _warningBrush : _successBrush;
        PollLateText.Text = late
            ? $"LATE {_pollClock.SincePreviousPoll.TotalSeconds:F1} S"
            : string.Empty;

        Pulse.Blink(HeartbeatDot);
    }

    private void UpdateStatusLine()
    {
        if (!HudConfig.EnableStatusLine)
        {
            StatusLine.Visibility = Visibility.Collapsed;
            return;
        }

        StatusLine.Visibility = Visibility.Visible;

        // Padded: these change every second, and a label that changes width
        // re-measures the whole line with it.
        StatusPollText.Text = $"POLL {_pollClock.LastPollDuration.TotalMilliseconds,3:F0} MS";

        var uptime = PollClock.SystemUptime;
        StatusUptimeText.Text = $"UP {uptime.Days}D {uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";

        StatusElevationText.Text = ElevationService.IsElevated ? "ELEVATED" : "NOT ELEVATED";
        StatusElevationText.Foreground = ElevationService.IsElevated ? _successBrush : _warningBrush;

        StatusClockText.Text = DateTime.Now.ToString("HH:mm:ss");

        UpdateLastEventText();
    }

    /// <summary>
    /// The most recent threshold crossing, with how long ago it happened.
    /// The relative time is recomputed here rather than bound: a bound
    /// property has no way to notice that a minute has passed.
    /// </summary>
    private void UpdateLastEventText()
    {
        if (_events.Latest is not EventEntry latest)
        {
            StatusEventText.Text = "NO EVENTS THIS SESSION";
            StatusEventText.Foreground = _dimBrush;
            return;
        }

        var ago = HudConfig.EnableRelativeEventTime
            ? $"  {FormatAgo(DateTime.Now - latest.Timestamp)}"
            : string.Empty;

        StatusEventText.Text = $"{latest.FormattedTime}  {latest.Source} {latest.Text}{ago}";

        StatusEventText.Foreground = latest.Severity switch
        {
            AlertSeverity.Critical => _dangerBrush,
            AlertSeverity.Warning => _warningBrush,
            _ => _dimBrush
        };
    }

    private static string FormatAgo(TimeSpan ago)
    {
        if (ago.TotalSeconds < 60) return $"{ago.TotalSeconds:F0}s ago";
        if (ago.TotalMinutes < 60) return $"{ago.TotalMinutes:F0}m ago";
        return $"{ago.TotalHours:F0}h ago";
    }

    /// <summary>
    /// Lights a row when bytes moved since the last poll. This catches what
    /// the meter cannot: activity below the resolution of a single segment
    /// still lights the lamp.
    /// </summary>
    private static void UpdateActivityLed(FrameworkElement led, double rate)
    {
        if (!HudConfig.EnableIoActivityLeds)
        {
            led.Visibility = Visibility.Collapsed;
            return;
        }

        led.Visibility = Visibility.Visible;

        if (rate >= ActivityFloor)
        {
            Pulse.Blink(led, 0.55, 700);
        }
        else
        {
            led.BeginAnimation(UIElement.OpacityProperty, null);
            led.Opacity = 0.15;
        }
    }

    // Drawn, not typed: an arrow glyph would fall back to another face where
    // the monospace font is missing. Frozen, because they never change.
    private static readonly Geometry RisingArrow = CreateFrozen("M 0,7 L 3.5,0 L 7,7 Z");
    private static readonly Geometry FallingArrow = CreateFrozen("M 0,0 L 7,0 L 3.5,7 Z");
    private static readonly Geometry SteadyBar = CreateFrozen("M 0,3 L 7,3 L 7,4.5 L 0,4.5 Z");

    private static Geometry CreateFrozen(string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Points an arrow the way a reading is moving over the last ten seconds.
    ///
    /// Where a temperature is heading matters more than where it is: 76
    /// degrees climbing and 76 degrees falling are the same number and two
    /// different situations. A deadband in the tracker keeps sensor noise
    /// from flipping the arrow every poll.
    /// </summary>
    private static void UpdateTrendArrow(Path arrow, SlopeTracker slope, float? value, Brush risingBrush, Brush steadyBrush)
    {
        if (!HudConfig.EnableTrendArrows || value is not float reading)
        {
            arrow.Visibility = Visibility.Collapsed;
            return;
        }

        slope.Push(reading, DateTime.UtcNow);

        arrow.Visibility = Visibility.Visible;
        (arrow.Data, arrow.Fill) = slope.Direction switch
        {
            TrendDirection.Rising => (RisingArrow, risingBrush),
            TrendDirection.Falling => (FallingArrow, steadyBrush),
            _ => (SteadyBar, steadyBrush)
        };
    }

    /// <summary>Bytes at a readable magnitude, padded so the width holds steady.</summary>
    private static string FormatBytes(double bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;

        if (bytes >= gb) return $"{bytes / gb,5:F1} GB";
        if (bytes >= mb) return $"{bytes / mb,5:F0} MB";
        return $"{bytes / kb,5:F0} KB";
    }
}
