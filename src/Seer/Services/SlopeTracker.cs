using System;
using System.Collections.Generic;

namespace Seer.Services;

/// <summary>Which way a reading is heading.</summary>
public enum TrendDirection
{
    Flat,
    Rising,
    Falling
}

/// <summary>
/// Works out which way a reading is going over a short window.
///
/// For a temperature this matters more than the number itself: 76 degrees
/// climbing and 76 degrees falling are the same digits and completely
/// different situations.
///
/// The deadband exists so the arrow does not flutter. Sensor noise of a tenth
/// of a degree would otherwise flip the direction every poll, which is motion
/// carrying no information — the thing this whole pass exists to avoid.
/// </summary>
public sealed class SlopeTracker
{
    private readonly TimeSpan _window;
    private readonly double _deadbandPerSecond;
    private readonly Queue<(DateTime At, double Value)> _samples = new();

    /// <param name="window">How far back to measure. Ten seconds by default.</param>
    /// <param name="deadbandPerSecond">
    /// Minimum rate of change before a direction is called. The default of
    /// 0.05 means half a unit across the ten-second window.
    /// </param>
    public SlopeTracker(TimeSpan? window = null, double deadbandPerSecond = 0.05)
    {
        _window = window ?? TimeSpan.FromSeconds(10);
        _deadbandPerSecond = deadbandPerSecond;
    }

    /// <summary>Change per second across the window, or 0 with too little history.</summary>
    public double Slope { get; private set; }

    public TrendDirection Direction =>
        Slope > _deadbandPerSecond ? TrendDirection.Rising
        : Slope < -_deadbandPerSecond ? TrendDirection.Falling
        : TrendDirection.Flat;

    public void Push(double value, DateTime now)
    {
        _samples.Enqueue((now, value));

        while (_samples.Count > 1 && now - _samples.Peek().At > _window)
            _samples.Dequeue();

        if (_samples.Count < 2)
        {
            Slope = 0;
            return;
        }

        var oldest = _samples.Peek();
        var seconds = (now - oldest.At).TotalSeconds;

        Slope = seconds <= 0 ? 0 : (value - oldest.Value) / seconds;
    }

    public void Push(double value) => Push(value, DateTime.UtcNow);

    public void Reset()
    {
        _samples.Clear();
        Slope = 0;
    }
}
