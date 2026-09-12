using System;

namespace Seer.Services;

/// <summary>
/// Remembers the highest value of the last few seconds, then lets it fall —
/// the behaviour of a peak-hold marker on an audio meter.
///
/// It exists because a one-second poll shown as a bar loses exactly the thing
/// worth seeing: a burst to 97% between two glances is gone by the time you
/// look. The marker keeps it on screen long enough to be read.
///
/// Pure arithmetic over a time series, deliberately free of WPF, so the decay
/// rule can be tested rather than eyeballed.
/// </summary>
public sealed class PeakHold
{
    private readonly TimeSpan _hold;
    private double _peak = double.NaN;
    private DateTime _setAt;

    /// <param name="hold">How long a peak stays before it drops. Ten seconds by default.</param>
    public PeakHold(TimeSpan? hold = null)
        => _hold = hold ?? TimeSpan.FromSeconds(10);

    /// <summary>The held peak, or NaN until the first value arrives.</summary>
    public double Peak => _peak;

    /// <summary>
    /// Feeds a reading and returns the peak to draw. A new high replaces the
    /// old one and restarts the hold; otherwise the peak stands until it
    /// expires, at which point it falls to the current reading rather than to
    /// zero — the meter should never claim a quieter moment than is happening.
    /// </summary>
    public double Push(double value, DateTime now)
    {
        if (double.IsNaN(_peak) || value >= _peak || now - _setAt >= _hold)
        {
            _peak = value;
            _setAt = now;
        }

        return _peak;
    }

    public double Push(double value) => Push(value, DateTime.UtcNow);

    /// <summary>Forgets the held peak. For when a source goes away entirely.</summary>
    public void Reset() => _peak = double.NaN;
}
