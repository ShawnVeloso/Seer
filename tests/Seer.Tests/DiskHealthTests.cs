using System;
using System.Collections.Generic;
using Seer.Models;
using Seer.Services;
using Xunit;

namespace Seer.Tests;

/// <summary>
/// Covers the pure half of drive health: the WMI value mapping and the
/// reduction to a single panel verdict.
///
/// The acquisition half isn't here and can't be — it needs real drives and,
/// for the SMART figures, elevation. What *is* testable is every decision
/// made about the values once they arrive, which is where the bugs would be.
/// </summary>
public class DiskHealthTests
{
    // ── MediaType mapping ────────────────────────────────────────────────

    [Theory]
    [InlineData(3, "HDD")]
    [InlineData(4, "SSD")]
    [InlineData(5, "SCM")]
    public void MediaTypeLabel_MapsKnownCodes(int raw, string expected) =>
        Assert.Equal(expected, DiskHealthService.MediaTypeLabel((ushort)raw));

    [Fact]
    public void MediaTypeLabel_FallsBackForUnspecified() =>
        Assert.Equal("Disk", DiskHealthService.MediaTypeLabel((ushort)0));

    [Fact]
    public void MediaTypeLabel_FallsBackForNull() =>
        Assert.Equal("Disk", DiskHealthService.MediaTypeLabel(null));

    // ── Health mapping ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, DriveHealthState.Healthy)]
    [InlineData(1, DriveHealthState.Warning)]
    [InlineData(2, DriveHealthState.Unhealthy)]
    public void HealthFrom_MapsKnownCodes(int raw, DriveHealthState expected) =>
        Assert.Equal(expected, DiskHealthService.HealthFrom((ushort)raw));

    [Fact]
    public void HealthFrom_UnknownCodeIsUnknownNotHealthy()
    {
        // The dangerous failure here would be defaulting to Healthy and
        // telling someone a failing disk is fine.
        Assert.Equal(DriveHealthState.Unknown, DiskHealthService.HealthFrom((ushort)99));
        Assert.Equal(DriveHealthState.Unknown, DiskHealthService.HealthFrom(null));
        Assert.Equal(DriveHealthState.Unknown, DiskHealthService.HealthFrom("nonsense"));
    }

    // ── Capacity ─────────────────────────────────────────────────────────

    [Fact]
    public void DecimalGb_UsesTheFigureOnTheBox()
    {
        // 1 TB drive: decimal GB, not the 931 a binary divide would give.
        Assert.Equal(1000.0, DiskHealthService.DecimalGb(1_000_204_886_016UL)!.Value, 0);
    }

    [Fact]
    public void DecimalGb_TreatsZeroAndNullAsUnavailable()
    {
        Assert.Null(DiskHealthService.DecimalGb(0UL));
        Assert.Null(DiskHealthService.DecimalGb(null));
    }

    [Fact]
    public void DecimalGb_SurvivesAGarbageValue() =>
        Assert.Null(DiskHealthService.DecimalGb("not a number"));

    [Theory]
    [InlineData(500.0, "500 GB")]
    [InlineData(1000.0, "1 TB")]
    [InlineData(2000.0, "2 TB")]
    public void CapacityLabel_SwitchesToTerabytes(double gb, string expected) =>
        Assert.Equal(expected, Drive("D", gb, DriveHealthState.Healthy).CapacityLabel);

    [Fact]
    public void CapacityLabel_ShowsDashesWhenUnknown() =>
        Assert.Equal("--", Drive("D", null, DriveHealthState.Healthy).CapacityLabel);

    // ── Name collapsing ──────────────────────────────────────────────────

    [Fact]
    public void Collapse_SqueezesTheDoubleSpaceWmiReports()
    {
        // Observed verbatim from Win32_DiskDrive on this machine.
        Assert.Equal("WDC WDS100T2B0A-00SM50", DiskHealthService.Collapse("WDC  WDS100T2B0A-00SM50"));
    }

    [Fact]
    public void Collapse_TrimsAndHandlesEmpty()
    {
        Assert.Equal("KINGSTON SA2000M8500G", DiskHealthService.Collapse("  KINGSTON SA2000M8500G  "));
        Assert.Equal(string.Empty, DiskHealthService.Collapse("   "));
        Assert.Equal(string.Empty, DiskHealthService.Collapse(""));
    }

    // ── Worst-of reduction ───────────────────────────────────────────────

    [Fact]
    public void Worst_PicksTheFailingDriveOverHealthyOnes()
    {
        var snapshot = Snapshot(
            Drive("A", 500, DriveHealthState.Healthy),
            Drive("B", 1000, DriveHealthState.Unhealthy),
            Drive("C", 250, DriveHealthState.Healthy));

        Assert.Equal(DriveHealthState.Unhealthy, snapshot.Worst);
    }

    [Fact]
    public void Worst_PrefersWarningOverHealthy()
    {
        var snapshot = Snapshot(
            Drive("A", 500, DriveHealthState.Healthy),
            Drive("B", 1000, DriveHealthState.Warning));

        Assert.Equal(DriveHealthState.Warning, snapshot.Worst);
    }

    [Fact]
    public void Worst_DoesNotLetUnknownOutrankHealthy()
    {
        // Unknown is 0 in the enum, so a naive Math.Max would return Healthy
        // here and an Unknown-only set would report Healthy. Both are wrong.
        Assert.Equal(
            DriveHealthState.Healthy,
            Snapshot(Drive("A", 500, DriveHealthState.Unknown),
                     Drive("B", 500, DriveHealthState.Healthy)).Worst);

        Assert.Equal(
            DriveHealthState.Unknown,
            Snapshot(Drive("A", 500, DriveHealthState.Unknown)).Worst);
    }

    [Fact]
    public void Worst_OfNothingIsUnknown() =>
        Assert.Equal(DriveHealthState.Unknown, DiskHealthSnapshot.Empty.Worst);

    // ── Null rendering ───────────────────────────────────────────────────

    [Fact]
    public void DetailLabels_ShowDashesRatherThanZeroWhenUnavailable()
    {
        var drive = Drive("A", 500, DriveHealthState.Healthy);

        Assert.Equal("--", drive.TemperatureLabel);
        Assert.Equal("--", drive.LifeLabel);
        Assert.Equal("--", drive.WrittenLabel);
    }

    [Fact]
    public void DetailLabels_FormatRealFigures()
    {
        var drive = Drive("A", 500, DriveHealthState.Healthy) with
        {
            TemperatureC = 41.6f,
            RemainingLifePercent = 97f,
            DataWrittenTb = 12.34f
        };

        Assert.Equal("42°C", drive.TemperatureLabel);
        Assert.Equal("97%", drive.LifeLabel);
        Assert.Equal("12.3 TB", drive.WrittenLabel);
    }

    [Fact]
    public void MissingFlags_OnlyFireWhenDetailWasExpected()
    {
        // Non-elevated: blank is normal, so nothing is flagged and the panel
        // explains it once instead of painting every cell amber.
        var nonElevated = Drive("A", 500, DriveHealthState.Healthy);
        Assert.False(nonElevated.TemperatureMissing);
        Assert.False(nonElevated.LifeMissing);

        // Elevated but this drive reported neither: that is a real gap.
        var elevated = nonElevated with { DetailExpected = true };
        Assert.True(elevated.TemperatureMissing);
        Assert.True(elevated.LifeMissing);

        // Elevated and reported: nothing missing.
        var populated = elevated with { TemperatureC = 40f, RemainingLifePercent = 99f };
        Assert.False(populated.TemperatureMissing);
        Assert.False(populated.LifeMissing);
    }

    [Fact]
    public void HealthLabel_ReadsWithoutColour()
    {
        Assert.Equal("OK", Drive("A", 1, DriveHealthState.Healthy).HealthLabel);
        Assert.Equal("WARN", Drive("A", 1, DriveHealthState.Warning).HealthLabel);
        Assert.Equal("FAIL", Drive("A", 1, DriveHealthState.Unhealthy).HealthLabel);
        Assert.Equal("--", Drive("A", 1, DriveHealthState.Unknown).HealthLabel);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static DriveHealth Drive(string name, double? gb, DriveHealthState health) =>
        new(name, "SSD", gb, health);

    private static DiskHealthSnapshot Snapshot(params DriveHealth[] drives) =>
        new(drives, false, DateTime.UtcNow, null);
}
