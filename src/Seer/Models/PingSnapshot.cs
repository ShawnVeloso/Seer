namespace Seer.Models;

/// <summary>
/// A point-in-time view of the ping monitor, safe to hand to the UI.
///
/// A snapshot rather than live fields: the ping loop runs on a background
/// task at its own cadence while the UI reads on the one-second timer, so
/// handing over an immutable copy avoids the UI ever seeing a half-updated
/// set of statistics.
/// </summary>
/// <param name="IsRunning">Whether the monitor is currently pinging.</param>
/// <param name="Host">Target being pinged.</param>
/// <param name="LastMs">Most recent successful round trip, in milliseconds.</param>
/// <param name="AverageMs">Mean of the recent window of successful replies.</param>
/// <param name="MinMs">Fastest reply in the recent window.</param>
/// <param name="MaxMs">Slowest reply in the recent window.</param>
/// <param name="JitterMs">
/// Mean absolute difference between consecutive replies. More telling than
/// average latency for calls and games — a steady 60ms beats an average of
/// 40ms that swings between 10 and 200.
/// </param>
/// <param name="Sent">Requests attempted this session.</param>
/// <param name="Received">Replies received this session.</param>
/// <param name="LastError">Why the last attempt failed, if it did.</param>
public record PingSnapshot(
    bool IsRunning,
    string Host,
    long? LastMs,
    double? AverageMs,
    long? MinMs,
    long? MaxMs,
    double? JitterMs,
    int Sent,
    int Received,
    string? LastError)
{
    /// <summary>Packet loss over the session, 0–100.</summary>
    public double LossPercent => Sent == 0 ? 0 : (Sent - Received) * 100.0 / Sent;

    /// <summary>An idle monitor that has never run.</summary>
    public static PingSnapshot Idle(string host) =>
        new(false, host, null, null, null, null, null, 0, 0, null);
}
