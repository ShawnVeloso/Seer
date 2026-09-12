using System;

namespace Seer.Services;

/// <summary>
/// Builds a running total out of periodic rate samples.
///
/// Disk throughput arrives as a rate from a Windows performance counter —
/// there is no cumulative byte count to read — so a session total has to be
/// integrated from one-second samples. That makes it an estimate: it assumes
/// the rate held steady between polls, and a burst that began and ended
/// between two samples is invisible to it.
///
/// Anything showing this number has to say so. The UI prefixes it with "~".
/// </summary>
public sealed class RateIntegrator
{
    /// <summary>
    /// Intervals longer than this are discarded rather than multiplied out:
    /// a stalled poll or a clock change would otherwise add one enormous
    /// phantom interval to the total.
    /// </summary>
    private const double MaxIntervalSeconds = 10;

    private DateTime? _lastSample;

    /// <summary>Accumulated total, in whatever unit the rate was per second.</summary>
    public double Total { get; private set; }

    /// <summary>
    /// Adds one sample and returns the new total. The first call only
    /// establishes a starting time, since there is no interval yet.
    /// </summary>
    public double Add(double ratePerSecond, DateTime now)
    {
        if (_lastSample is DateTime previous)
        {
            var seconds = (now - previous).TotalSeconds;

            if (seconds > 0 && seconds < MaxIntervalSeconds && ratePerSecond > 0)
                Total += ratePerSecond * seconds;
        }

        _lastSample = now;
        return Total;
    }

    public double Add(double ratePerSecond) => Add(ratePerSecond, DateTime.UtcNow);
}
