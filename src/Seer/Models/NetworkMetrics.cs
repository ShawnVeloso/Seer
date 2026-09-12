namespace Seer.Models;

public record NetworkMetrics
{
    public double DownloadMbps { get; init; }
    public double UploadMbps { get; init; }

    /// <summary>
    /// Bytes received since the app started. Exact, not estimated: the
    /// adapters report cumulative counters and the monitor sums the same
    /// deltas it already uses for the rate.
    /// </summary>
    public double SessionReceivedBytes { get; init; }

    /// <summary>Bytes sent since the app started. Exact, as above.</summary>
    public double SessionSentBytes { get; init; }

    // The bar text that used to live here is gone: SegmentMeter draws the
    // row now, on a log scale. The old one was linear to gigabit, so a
    // normal download never lit a single character.
}
