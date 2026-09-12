using System;
using System.Diagnostics;

namespace Seer.Services;

/// <summary>
/// Times the poll itself, and notices when one arrives late.
///
/// The cost of Seer is the one number a monitoring tool has no excuse for
/// hiding: if the window costs more than the thing it watches, that belongs
/// on screen. The lateness check is the honest version of a heartbeat — the
/// dispatcher can only report a delay after the fact, so a late poll is
/// detected on the one that follows it.
/// </summary>
public sealed class PollClock
{
    /// <summary>A gap beyond this means the previous poll ran late.</summary>
    private static readonly TimeSpan LateThreshold = TimeSpan.FromMilliseconds(1500);

    private readonly Stopwatch _current = new();
    private DateTime? _previousStart;

    /// <summary>How long the last completed poll took.</summary>
    public TimeSpan LastPollDuration { get; private set; }

    /// <summary>Wall time between the last two polls.</summary>
    public TimeSpan SincePreviousPoll { get; private set; }

    /// <summary>Whether the gap since the previous poll was long enough to notice.</summary>
    public bool WasLate => SincePreviousPoll > LateThreshold;

    /// <summary>How long this machine has been up.</summary>
    public static TimeSpan SystemUptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public void BeginPoll(DateTime now)
    {
        SincePreviousPoll = _previousStart is DateTime previous ? now - previous : TimeSpan.Zero;
        _previousStart = now;

        _current.Restart();
    }

    public void BeginPoll() => BeginPoll(DateTime.UtcNow);

    public void EndPoll()
    {
        _current.Stop();
        LastPollDuration = _current.Elapsed;
    }
}
