using System;
using System.Collections.Generic;
using Seer.Controls;
using Seer.Models;
using Seer.Services;
using Xunit;

namespace Seer.Tests;

/// <summary>
/// The arithmetic behind the living details.
///
/// These are the parts of the restyle that can be checked without a screen:
/// decay rules, deadbands and column counts. What a peak marker looks like is
/// for the lead developer to judge; whether the peak decays after ten seconds
/// is not.
/// </summary>
public class PeakHoldTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void First_value_becomes_the_peak()
    {
        var peak = new PeakHold();
        Assert.Equal(40, peak.Push(40, T0));
    }

    [Fact]
    public void Higher_value_replaces_the_peak_immediately()
    {
        var peak = new PeakHold();
        peak.Push(40, T0);

        Assert.Equal(90, peak.Push(90, T0.AddSeconds(1)));
    }

    [Fact]
    public void Lower_value_does_not_move_the_peak_within_the_hold()
    {
        var peak = new PeakHold(TimeSpan.FromSeconds(10));
        peak.Push(90, T0);

        Assert.Equal(90, peak.Push(10, T0.AddSeconds(5)));
    }

    [Fact]
    public void Peak_falls_to_the_current_reading_once_the_hold_expires()
    {
        var peak = new PeakHold(TimeSpan.FromSeconds(10));
        peak.Push(90, T0);

        // Not to zero: the marker must never claim a quieter moment than is
        // actually happening.
        Assert.Equal(10, peak.Push(10, T0.AddSeconds(11)));
    }

    [Fact]
    public void A_new_high_restarts_the_hold()
    {
        var peak = new PeakHold(TimeSpan.FromSeconds(10));
        peak.Push(50, T0);
        peak.Push(95, T0.AddSeconds(9));

        Assert.Equal(95, peak.Push(20, T0.AddSeconds(18)));
    }
}

public class SeverityHoldTests
{
    [Fact]
    public void Escalation_is_immediate()
    {
        var hold = new SeverityHold();
        Assert.Equal(AlertSeverity.Warning, hold.Push(AlertSeverity.Warning));
    }

    [Fact]
    public void Clearing_waits_for_consecutive_calmer_polls()
    {
        var hold = new SeverityHold(pollsBeforeClearing: 3);
        hold.Push(AlertSeverity.Warning);

        Assert.Equal(AlertSeverity.Warning, hold.Push(AlertSeverity.Nominal));
        Assert.Equal(AlertSeverity.Warning, hold.Push(AlertSeverity.Nominal));
        Assert.Equal(AlertSeverity.Nominal, hold.Push(AlertSeverity.Nominal));
    }

    [Fact]
    public void A_reading_flickering_on_its_threshold_holds_steady()
    {
        var hold = new SeverityHold(pollsBeforeClearing: 3);
        hold.Push(AlertSeverity.Warning);

        // 84.9, 85.1, 84.9 ... the case the hold exists for.
        hold.Push(AlertSeverity.Nominal);
        hold.Push(AlertSeverity.Warning);
        hold.Push(AlertSeverity.Nominal);

        Assert.Equal(AlertSeverity.Warning, hold.Push(AlertSeverity.Nominal));
    }

    [Fact]
    public void Critical_outranks_a_pending_clear()
    {
        var hold = new SeverityHold(pollsBeforeClearing: 3);
        hold.Push(AlertSeverity.Warning);
        hold.Push(AlertSeverity.Nominal);

        Assert.Equal(AlertSeverity.Critical, hold.Push(AlertSeverity.Critical));
    }
}

public class SlopeTrackerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void One_sample_has_no_direction()
    {
        var slope = new SlopeTracker();
        slope.Push(50, T0);

        Assert.Equal(TrendDirection.Flat, slope.Direction);
    }

    [Fact]
    public void A_climb_reads_as_rising()
    {
        var slope = new SlopeTracker(deadbandPerSecond: 0.05);
        slope.Push(60, T0);
        slope.Push(70, T0.AddSeconds(5));

        Assert.Equal(TrendDirection.Rising, slope.Direction);
    }

    [Fact]
    public void A_drop_reads_as_falling()
    {
        var slope = new SlopeTracker(deadbandPerSecond: 0.05);
        slope.Push(70, T0);
        slope.Push(60, T0.AddSeconds(5));

        Assert.Equal(TrendDirection.Falling, slope.Direction);
    }

    [Fact]
    public void Noise_inside_the_deadband_stays_flat()
    {
        // A tenth of a degree over ten seconds is sensor noise, not a trend.
        var slope = new SlopeTracker(deadbandPerSecond: 0.05);
        slope.Push(70.0, T0);
        slope.Push(70.1, T0.AddSeconds(10));

        Assert.Equal(TrendDirection.Flat, slope.Direction);
    }

    [Fact]
    public void Samples_older_than_the_window_are_dropped()
    {
        var slope = new SlopeTracker(TimeSpan.FromSeconds(10), deadbandPerSecond: 0.05);
        slope.Push(0, T0);
        slope.Push(90, T0.AddSeconds(30));
        slope.Push(90, T0.AddSeconds(31));

        // The climb from 0 fell out of the window; what remains is flat.
        Assert.Equal(TrendDirection.Flat, slope.Direction);
    }
}

public class RateIntegratorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void First_sample_only_establishes_a_start_time()
    {
        var total = new RateIntegrator();
        Assert.Equal(0, total.Add(1000, T0));
    }

    [Fact]
    public void Rate_multiplied_by_elapsed_time_accumulates()
    {
        var total = new RateIntegrator();
        total.Add(1000, T0);

        Assert.Equal(1000, total.Add(1000, T0.AddSeconds(1)));
        Assert.Equal(3000, total.Add(1000, T0.AddSeconds(3)));
    }

    [Fact]
    public void An_implausibly_long_gap_is_discarded()
    {
        // A stalled poll or a clock change would otherwise add one enormous
        // phantom interval to the session total.
        var total = new RateIntegrator();
        total.Add(1000, T0);

        Assert.Equal(0, total.Add(1000, T0.AddMinutes(5)));
    }
}

public class SessionStatsTests
{
    [Fact]
    public void Nothing_pushed_means_nothing_known()
    {
        var stats = new SessionStats();

        Assert.Null(stats.Min);
        Assert.Null(stats.Max);
        Assert.Null(stats.Average);
    }

    [Fact]
    public void Tracks_low_high_and_mean()
    {
        var stats = new SessionStats();
        stats.Push(10);
        stats.Push(20);
        stats.Push(60);

        Assert.Equal(10, stats.Min);
        Assert.Equal(60, stats.Max);
        Assert.Equal(30, stats.Average);
    }
}

public class CoreMatrixLayoutTests
{
    /// <summary>Pixels per character, for a 13-character cell and a 2-character gap.</summary>
    private const double CharWidth = 7;

    [Theory]
    [InlineData(0, 16, 4)]
    [InlineData(double.NaN, 16, 4)]
    [InlineData(double.PositiveInfinity, 16, 4)]
    public void Without_a_measurable_width_it_guesses(double width, int cells, int expected)
        => Assert.Equal(expected, CoreMatrix.ColumnsThatFit(width, CharWidth, cells));

    [Fact]
    public void A_narrow_panel_still_gets_one_column()
        => Assert.Equal(1, CoreMatrix.ColumnsThatFit(20, CharWidth, 16));

    [Fact]
    public void Columns_grow_with_the_available_width()
    {
        var narrow = CoreMatrix.ColumnsThatFit(200, CharWidth, 16);
        var wide = CoreMatrix.ColumnsThatFit(700, CharWidth, 16);

        Assert.True(wide > narrow, $"expected more columns at 700px than at 200px, got {wide} and {narrow}");
    }

    [Fact]
    public void Never_more_columns_than_there_are_cores()
        => Assert.Equal(4, CoreMatrix.ColumnsThatFit(4000, CharWidth, 4));
}

public class ProcessRankTrackerTests
{
    private static ProcessMetrics Process(int pid, string name, double cpu)
        => new() { Pid = pid, Name = name, CpuPercent = cpu };

    [Fact]
    public void The_first_poll_marks_nothing()
    {
        var tracker = new ProcessRankTracker();

        var marked = tracker.Apply(new List<ProcessMetrics> { Process(1, "a", 10) });

        Assert.Equal(RankMark.None, marked[0].RankMark);
    }

    [Fact]
    public void A_newcomer_is_marked_new()
    {
        var tracker = new ProcessRankTracker();
        tracker.Apply(new List<ProcessMetrics> { Process(1, "a", 10) });

        var marked = tracker.Apply(new List<ProcessMetrics>
        {
            Process(1, "a", 10),
            Process(2, "b", 5)
        });

        Assert.Equal(RankMark.New, marked[1].RankMark);
    }

    [Fact]
    public void A_climb_backed_by_real_cpu_is_marked()
    {
        var tracker = new ProcessRankTracker();
        tracker.Apply(new List<ProcessMetrics> { Process(1, "a", 10), Process(2, "b", 5) });

        var marked = tracker.Apply(new List<ProcessMetrics>
        {
            Process(2, "b", 20),
            Process(1, "a", 10)
        });

        Assert.Equal(RankMark.Up, marked[0].RankMark);
    }

    [Fact]
    public void Places_swapped_on_rounding_noise_are_not_marked()
    {
        // Two processes sitting at 2% reorder constantly. Marking that would
        // train the eye to ignore the arrows.
        var tracker = new ProcessRankTracker();
        tracker.Apply(new List<ProcessMetrics> { Process(1, "a", 2.1), Process(2, "b", 2.0) });

        var marked = tracker.Apply(new List<ProcessMetrics>
        {
            Process(2, "b", 2.1),
            Process(1, "a", 2.0)
        });

        Assert.Equal(RankMark.None, marked[0].RankMark);
        Assert.Equal(RankMark.None, marked[1].RankMark);
    }

    [Fact]
    public void Processes_sharing_a_name_are_tracked_separately()
    {
        // A machine runs a dozen processes called "brave". Keying by name
        // would let them overwrite each other and mark the wrong rows.
        var tracker = new ProcessRankTracker();
        tracker.Apply(new List<ProcessMetrics>
        {
            Process(1, "brave", 10),
            Process(2, "brave", 5)
        });

        var marked = tracker.Apply(new List<ProcessMetrics>
        {
            Process(2, "brave", 20),
            Process(1, "brave", 10)
        });

        Assert.Equal(RankMark.Up, marked[0].RankMark);
        Assert.Equal(2, marked[0].Pid);
    }

    [Fact]
    public void A_mark_is_held_for_several_polls()
    {
        var tracker = new ProcessRankTracker();
        tracker.Apply(new List<ProcessMetrics> { Process(1, "a", 10), Process(2, "b", 5) });
        tracker.Apply(new List<ProcessMetrics> { Process(2, "b", 20), Process(1, "a", 10) });

        var marked = tracker.Apply(new List<ProcessMetrics> { Process(2, "b", 20), Process(1, "a", 10) });

        Assert.Equal(RankMark.Up, marked[0].RankMark);
    }
}
