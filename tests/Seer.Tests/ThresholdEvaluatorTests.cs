using Seer.Models;
using Seer.Services;
using Xunit;

namespace Seer.Tests;

/// <summary>
/// ThresholdEvaluator decides what the status badge says and what lands
/// in the alert log. It is pure logic — no WPF, no hardware — so it is
/// the one part of Seer that can be verified without heating the machine
/// up and watching.
///
/// Two behaviours matter most and are easy to break: thresholds are
/// inclusive (>=), and an alert fires on *escalation* only, so a metric
/// parked at 90% doesn't append a new alert every single second.
/// </summary>
public class ThresholdEvaluatorTests
{
    /// <summary>Defaults: load warns at 85 / crits at 95, temp warns at 75 / crits at 85.</summary>
    private static AppSettings Defaults() => new();

    private static CpuMetrics Cpu(float? load = null, float? temp = null)
        => new() { TotalLoad = load, Temperature = temp };

    private static MemoryMetrics Mem(float? load = null)
        => new() { Load = load };

    private static GpuMetrics Gpu(float? load = null, float? temp = null)
        => new() { Load = load, Temperature = temp };

    // ── Severity boundaries ────────────────────────────────────────────

    [Theory]
    [InlineData(0f, AlertSeverity.Nominal)]
    [InlineData(84.9f, AlertSeverity.Nominal)]
    [InlineData(85f, AlertSeverity.Warning)]    // inclusive lower bound
    [InlineData(94.9f, AlertSeverity.Warning)]
    [InlineData(95f, AlertSeverity.Critical)]   // inclusive lower bound
    [InlineData(100f, AlertSeverity.Critical)]
    public void Load_severity_is_inclusive_at_each_threshold(float load, AlertSeverity expected)
    {
        var (overall, _) = new ThresholdEvaluator()
            .Evaluate(Cpu(load: load), Mem(), Gpu(), Defaults());

        Assert.Equal(expected, overall);
    }

    [Theory]
    [InlineData(74.9f, AlertSeverity.Nominal)]
    [InlineData(75f, AlertSeverity.Warning)]
    [InlineData(85f, AlertSeverity.Critical)]
    public void Temperature_severity_is_inclusive_at_each_threshold(float temp, AlertSeverity expected)
    {
        var (overall, _) = new ThresholdEvaluator()
            .Evaluate(Cpu(temp: temp), Mem(), Gpu(), Defaults());

        Assert.Equal(expected, overall);
    }

    [Fact]
    public void Overall_severity_is_the_worst_across_all_metrics()
    {
        // CPU nominal, memory warning, GPU critical — the worst must win,
        // so one hot component cannot be masked by healthy neighbours.
        var (overall, _) = new ThresholdEvaluator()
            .Evaluate(Cpu(load: 10f), Mem(load: 90f), Gpu(load: 99f), Defaults());

        Assert.Equal(AlertSeverity.Critical, overall);
    }

    // ── Escalation behaviour ───────────────────────────────────────────

    [Fact]
    public void Sustained_warning_logs_one_alert_not_one_per_poll()
    {
        var evaluator = new ThresholdEvaluator();
        var settings = Defaults();

        var (_, first) = evaluator.Evaluate(Cpu(load: 90f), Mem(), Gpu(), settings);
        var (_, second) = evaluator.Evaluate(Cpu(load: 91f), Mem(), Gpu(), settings);
        var (_, third) = evaluator.Evaluate(Cpu(load: 92f), Mem(), Gpu(), settings);

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Empty(third);
    }

    [Fact]
    public void Warning_escalating_to_critical_logs_again()
    {
        var evaluator = new ThresholdEvaluator();
        var settings = Defaults();

        var (_, warned) = evaluator.Evaluate(Cpu(load: 90f), Mem(), Gpu(), settings);
        var (_, crossed) = evaluator.Evaluate(Cpu(load: 99f), Mem(), Gpu(), settings);

        Assert.Equal(AlertSeverity.Warning, Assert.Single(warned).Severity);
        Assert.Equal(AlertSeverity.Critical, Assert.Single(crossed).Severity);
    }

    [Fact]
    public void Recovering_then_spiking_again_logs_a_second_alert()
    {
        var evaluator = new ThresholdEvaluator();
        var settings = Defaults();

        evaluator.Evaluate(Cpu(load: 90f), Mem(), Gpu(), settings);   // warn
        evaluator.Evaluate(Cpu(load: 10f), Mem(), Gpu(), settings);   // recover
        var (_, again) = evaluator.Evaluate(Cpu(load: 90f), Mem(), Gpu(), settings);

        Assert.Single(again);
    }

    [Fact]
    public void Dropping_from_critical_to_warning_does_not_log()
    {
        var evaluator = new ThresholdEvaluator();
        var settings = Defaults();

        evaluator.Evaluate(Cpu(load: 99f), Mem(), Gpu(), settings);
        var (overall, alerts) = evaluator.Evaluate(Cpu(load: 90f), Mem(), Gpu(), settings);

        // Still worth showing as WARNING, but easing off is not news.
        Assert.Equal(AlertSeverity.Warning, overall);
        Assert.Empty(alerts);
    }

    // ── Missing sensors ────────────────────────────────────────────────

    [Fact]
    public void Null_readings_are_skipped_rather_than_treated_as_zero()
    {
        // Everything unavailable: the non-elevated case for CPU temp, or
        // a machine with no discrete GPU.
        var (overall, alerts) = new ThresholdEvaluator()
            .Evaluate(Cpu(), Mem(), Gpu(), Defaults());

        Assert.Equal(AlertSeverity.Nominal, overall);
        Assert.Empty(alerts);
    }

    [Fact]
    public void A_missing_reading_does_not_mask_a_present_one()
    {
        var (overall, alerts) = new ThresholdEvaluator()
            .Evaluate(Cpu(load: null, temp: 99f), Mem(), Gpu(), Defaults());

        Assert.Equal(AlertSeverity.Critical, overall);
        Assert.Single(alerts);
    }

    // ── Alert contents ─────────────────────────────────────────────────

    [Fact]
    public void Alert_carries_the_metric_name_value_and_unit()
    {
        var (_, alerts) = new ThresholdEvaluator()
            .Evaluate(Cpu(temp: 90f), Mem(), Gpu(), Defaults());

        var alert = Assert.Single(alerts);
        Assert.Equal("CPU Temp", alert.MetricName);
        Assert.Equal(90f, alert.Value);
        Assert.Equal(" °C", alert.Unit);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void Each_metric_escalates_independently()
    {
        var evaluator = new ThresholdEvaluator();
        var settings = Defaults();

        // CPU load warns first; on the next poll GPU load warns too. The
        // GPU alert must not be suppressed by the CPU's existing state.
        evaluator.Evaluate(Cpu(load: 90f), Mem(), Gpu(), settings);
        var (_, alerts) = evaluator.Evaluate(Cpu(load: 90f), Mem(), Gpu(load: 90f), settings);

        var alert = Assert.Single(alerts);
        Assert.Equal("GPU Load", alert.MetricName);
    }

    // ── Custom thresholds (what the Settings window writes) ────────────

    [Fact]
    public void Custom_thresholds_from_settings_are_honoured()
    {
        var strict = new AppSettings { LoadWarningThreshold = 50f, LoadCriticalThreshold = 60f };

        var (overall, _) = new ThresholdEvaluator()
            .Evaluate(Cpu(load: 55f), Mem(), Gpu(), strict);

        Assert.Equal(AlertSeverity.Warning, overall);
    }
}
