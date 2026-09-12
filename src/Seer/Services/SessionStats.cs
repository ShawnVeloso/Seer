namespace Seer.Services;

/// <summary>
/// Running low, mean and high for one reading, for as long as the app has
/// been open.
///
/// Kept as a running total rather than a list of samples: the charts already
/// hold the last two minutes, and this is the part that outlives them. A peak
/// that scrolled off the plot ten minutes ago is exactly what you want to be
/// able to see.
/// </summary>
public sealed class SessionStats
{
    private double _sum;
    private long _count;

    public double? Min { get; private set; }

    public double? Max { get; private set; }

    public double? Average => _count == 0 ? null : _sum / _count;

    public long Count => _count;

    public void Push(double value)
    {
        _sum += value;
        _count++;

        if (Min is null || value < Min) Min = value;
        if (Max is null || value > Max) Max = value;
    }

    public void Reset()
    {
        _sum = 0;
        _count = 0;
        Min = null;
        Max = null;
    }
}
