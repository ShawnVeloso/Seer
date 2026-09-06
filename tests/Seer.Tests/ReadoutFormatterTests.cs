using System.Linq;
using Seer.Models;
using Seer.Services;
using Xunit;

namespace Seer.Tests;

/// <summary>
/// ReadoutFormatter is shared by the taskbar tray icons and the desktop
/// overlay, so a mistake here shows up in two places at once — and both
/// are the surfaces a user stares at while gaming, where a wrong or
/// mis-coloured number is worse than no number.
/// </summary>
public class ReadoutFormatterTests
{
    private static AppSettings Defaults() => new();

    private static CpuMetrics Cpu(float? load = null, float? temp = null)
        => new() { TotalLoad = load, Temperature = temp };

    private static MemoryMetrics Mem(float? load = null, float? used = null)
        => new() { Load = load, UsedGb = used };

    private static GpuMetrics Gpu(float? load = null, float? temp = null, float? vram = null)
        => new() { Load = load, Temperature = temp, VramUsedGb = vram };

    // ── Formatting ─────────────────────────────────────────────────────

    [Fact]
    public void Temperature_reads_as_whole_degrees()
    {
        var reading = ReadoutFormatter.Read(
            ReadoutMetric.CpuTemp, Cpu(temp: 66.4f), Mem(), Gpu(), Defaults());

        Assert.Equal("66", reading.Compact);
        Assert.Equal("CPU 66°C", reading.Full);
    }

    [Fact]
    public void Load_reads_as_whole_percent()
    {
        var reading = ReadoutFormatter.Read(
            ReadoutMetric.GpuLoad, Cpu(), Mem(), Gpu(load: 14.2f), Defaults());

        Assert.Equal("14", reading.Compact);
        Assert.Equal("GPU 14%", reading.Full);
    }

    [Fact]
    public void Memory_used_keeps_a_decimal_in_full_but_not_compact()
    {
        // "11.9" will not fit a 16px tray icon; the overlay has room.
        var reading = ReadoutFormatter.Read(
            ReadoutMetric.MemUsed, Cpu(), Mem(used: 11.94f), Gpu(), Defaults());

        Assert.Equal("RAM 11.9GB", reading.Full);
        Assert.Equal("12", reading.Compact);
    }

    // ── Missing sensors ────────────────────────────────────────────────

    [Fact]
    public void Unavailable_sensor_shows_placeholder_not_zero()
    {
        // The non-elevated case: CPU temperature is simply unreadable.
        var reading = ReadoutFormatter.Read(
            ReadoutMetric.CpuTemp, Cpu(temp: null), Mem(), Gpu(), Defaults());

        Assert.Equal("--", reading.Compact);
        Assert.Equal("CPU --°C", reading.Full);
        Assert.Equal(AlertSeverity.Nominal, reading.Severity);
    }

    // ── Severity, which drives the colour ──────────────────────────────

    [Theory]
    [InlineData(70f, AlertSeverity.Nominal)]
    [InlineData(75f, AlertSeverity.Warning)]
    [InlineData(90f, AlertSeverity.Critical)]
    public void Temperature_severity_uses_the_temperature_thresholds(float temp, AlertSeverity expected)
    {
        var reading = ReadoutFormatter.Read(
            ReadoutMetric.GpuTemp, Cpu(), Mem(), Gpu(temp: temp), Defaults());

        Assert.Equal(expected, reading.Severity);
    }

    [Fact]
    public void Load_severity_uses_the_load_thresholds_not_the_temperature_ones()
    {
        // 80 is above the temp warning (75) but below the load warning
        // (85). Reading it against the wrong pair would paint an idle
        // machine amber.
        var reading = ReadoutFormatter.Read(
            ReadoutMetric.CpuLoad, Cpu(load: 80f), Mem(), Gpu(), Defaults());

        Assert.Equal(AlertSeverity.Nominal, reading.Severity);
    }

    [Fact]
    public void Absolute_sizes_never_report_a_severity()
    {
        // 30 GB in use is not "critical", it's a machine being used.
        var reading = ReadoutFormatter.Read(
            ReadoutMetric.MemUsed, Cpu(), Mem(used: 30f), Gpu(), Defaults());

        Assert.Equal(AlertSeverity.Nominal, reading.Severity);
    }

    [Fact]
    public void Severity_follows_custom_thresholds()
    {
        var strict = new AppSettings { TempWarningThreshold = 50f, TempCriticalThreshold = 60f };

        var reading = ReadoutFormatter.Read(
            ReadoutMetric.CpuTemp, Cpu(temp: 55f), Mem(), Gpu(), strict);

        Assert.Equal(AlertSeverity.Warning, reading.Severity);
    }

    // ── Selection handling ─────────────────────────────────────────────

    [Fact]
    public void ReadAll_preserves_the_requested_order()
    {
        var readings = ReadoutFormatter.ReadAll(
            new[] { ReadoutMetric.GpuTemp, ReadoutMetric.CpuLoad },
            Cpu(load: 5f), Mem(), Gpu(temp: 40f), Defaults());

        Assert.Equal(
            new[] { ReadoutMetric.GpuTemp, ReadoutMetric.CpuLoad },
            readings.Select(r => r.Metric));
    }

    [Fact]
    public void ReadAll_drops_duplicates()
    {
        // A duplicate would mean two identical tray icons competing for
        // space in the notification area.
        var readings = ReadoutFormatter.ReadAll(
            new[] { ReadoutMetric.CpuTemp, ReadoutMetric.CpuTemp },
            Cpu(temp: 50f), Mem(), Gpu(), Defaults());

        Assert.Single(readings);
    }

    [Fact]
    public void ReadAll_of_nothing_is_empty_not_an_error()
    {
        var readings = ReadoutFormatter.ReadAll(
            new ReadoutMetric[0], Cpu(), Mem(), Gpu(), Defaults());

        Assert.Empty(readings);
    }

    [Fact]
    public void Every_metric_can_be_read_and_named()
    {
        // Guards the switch statements: a new enum member that nobody
        // wired up would fall through to the "?" placeholder.
        foreach (var metric in ReadoutFormatter.All)
        {
            var reading = ReadoutFormatter.Read(metric, Cpu(), Mem(), Gpu(), Defaults());

            Assert.NotEqual("?", reading.Label);
            Assert.False(string.IsNullOrWhiteSpace(ReadoutFormatter.DisplayName(metric)));
        }
    }

    [Fact]
    public void All_covers_every_declared_metric()
    {
        var declared = System.Enum.GetValues<ReadoutMetric>();
        Assert.Equal(declared.Length, ReadoutFormatter.All.Length);
    }
}
