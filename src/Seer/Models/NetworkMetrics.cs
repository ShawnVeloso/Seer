namespace Seer.Models;

public record NetworkMetrics
{
    public double DownloadMbps { get; init; }
    public double UploadMbps { get; init; }

    // 1000 Mbps max scale for the dense inline bar (Gigabit)
    private const double MaxMbps = 1000.0;

    public string DownloadBar => GetBar(DownloadMbps);
    public string UploadBar => GetBar(UploadMbps);

    private static string GetBar(double mbps)
    {
        int bars = (int)((mbps / MaxMbps) * 10.0);
        if (bars > 10) bars = 10;
        if (bars < 0) bars = 0;
        return new string('|', bars).PadRight(10, ' ');
    }
}
