using System;
using Seer.Models;
using Seer.Services;
using Xunit;

namespace Seer.Tests;

/// <summary>
/// The ping monitor is the only part of Seer that sends traffic and the
/// only one with a start/stop lifetime, so the things worth pinning down
/// are the lifecycle and the arithmetic — not whether packets arrive.
///
/// Nothing here asserts on a successful reply: a test that needs the
/// network to be up is a test that fails for reasons unrelated to the
/// code. The loop's own behaviour is exercised with a host that cannot
/// resolve, which is itself a case worth covering.
/// </summary>
public class PingMonitorTests
{
    // ── Snapshot arithmetic ────────────────────────────────────────────

    [Fact]
    public void Loss_is_zero_before_anything_is_sent()
    {
        // Not NaN: 0 sent, 0 received would divide by zero naively, and
        // the panel would print "NaN %" the moment it started.
        Assert.Equal(0, PingSnapshot.Idle("1.1.1.1").LossPercent);
    }

    [Theory]
    [InlineData(100, 100, 0d)]
    [InlineData(100, 90, 10d)]
    [InlineData(4, 1, 75d)]
    [InlineData(10, 0, 100d)]
    public void Loss_is_the_share_of_unanswered_requests(int sent, int received, double expected)
    {
        var snapshot = new PingSnapshot(
            true, "host", null, null, null, null, null, sent, received, null);

        Assert.Equal(expected, snapshot.LossPercent, precision: 6);
    }

    [Fact]
    public void An_idle_snapshot_reports_nothing_running_and_no_readings()
    {
        var snapshot = PingSnapshot.Idle("8.8.8.8");

        Assert.False(snapshot.IsRunning);
        Assert.Equal("8.8.8.8", snapshot.Host);
        Assert.Null(snapshot.LastMs);
        Assert.Null(snapshot.AverageMs);
        Assert.Null(snapshot.JitterMs);
        Assert.Null(snapshot.LastError);
    }

    // ── Lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public void A_fresh_monitor_is_not_running()
    {
        using var monitor = new PingMonitorService();

        Assert.False(monitor.IsRunning);
        Assert.False(monitor.GetSnapshot().IsRunning);
    }

    [Fact]
    public void Start_then_stop_flips_the_running_state()
    {
        using var monitor = new PingMonitorService();

        // Deliberately unresolvable: running state must not depend on
        // any packet getting through.
        monitor.Start("no-such-host.invalid", TimeSpan.FromSeconds(1));
        Assert.True(monitor.IsRunning);

        monitor.Stop();
        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void Starting_with_a_blank_host_does_nothing()
    {
        using var monitor = new PingMonitorService();

        monitor.Start("   ", TimeSpan.FromSeconds(1));

        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void Stopping_when_never_started_is_harmless()
    {
        using var monitor = new PingMonitorService();

        monitor.Stop();
        monitor.Stop();

        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void Restarting_clears_the_previous_hosts_statistics()
    {
        using var monitor = new PingMonitorService();

        monitor.Start("no-such-host.invalid", TimeSpan.FromMilliseconds(50));
        monitor.Start("another-host.invalid", TimeSpan.FromMilliseconds(50));

        var snapshot = monitor.GetSnapshot();

        // Statistics describe one host; carrying them across would report
        // loss for a target that was never asked.
        Assert.Equal("another-host.invalid", snapshot.Host);
        Assert.True(snapshot.Sent <= 1);
        Assert.Null(snapshot.LastMs);

        monitor.Stop();
    }

    [Fact]
    public void Disposing_stops_a_running_monitor()
    {
        var monitor = new PingMonitorService();
        monitor.Start("no-such-host.invalid", TimeSpan.FromMilliseconds(50));

        monitor.Dispose();

        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void Starting_after_dispose_is_ignored_rather_than_throwing()
    {
        var monitor = new PingMonitorService();
        monitor.Dispose();

        monitor.Start("1.1.1.1", TimeSpan.FromSeconds(1));

        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void The_host_is_trimmed_when_starting()
    {
        using var monitor = new PingMonitorService();

        monitor.Start("  1.1.1.1  ", TimeSpan.FromSeconds(30));

        Assert.Equal("1.1.1.1", monitor.GetSnapshot().Host);
        monitor.Stop();
    }
}
