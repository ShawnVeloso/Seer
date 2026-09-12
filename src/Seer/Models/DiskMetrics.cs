namespace Seer.Models;

public record DiskMetrics
{
    public double ReadBytesPerSec { get; init; }
    public double WriteBytesPerSec { get; init; }

    // The bar text that used to live here is gone: SegmentMeter draws the
    // row now, on a log scale. The old one was linear to 100 MB/s, so
    // everyday disk activity never lit a single character.
}
