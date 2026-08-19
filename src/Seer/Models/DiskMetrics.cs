namespace Seer.Models;

public record DiskMetrics
{
    public double ReadBytesPerSec { get; init; }
    public double WriteBytesPerSec { get; init; }

    // 100 MB/s max scale for the dense inline bar (arbitrary for visual density)
    private const double MaxBytesPerSec = 100.0 * 1024 * 1024;

    public string ReadBar => GetBar(ReadBytesPerSec);
    public string WriteBar => GetBar(WriteBytesPerSec);

    private static string GetBar(double bytesPerSec)
    {
        int bars = (int)((bytesPerSec / MaxBytesPerSec) * 10.0);
        if (bars > 10) bars = 10;
        if (bars < 0) bars = 0;
        return new string('|', bars).PadRight(10, ' ');
    }
}
