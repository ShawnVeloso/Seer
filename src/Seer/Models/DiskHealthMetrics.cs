using System;
using System.Collections.Generic;

namespace Seer.Models;

/// <summary>
/// What Windows will say about a drive's condition.
///
/// Deliberately coarse. The underlying SMART attributes are a vendor-specific
/// mess — the same attribute number means different things on a Kingston and
/// a WD — so the only cross-drive answer worth showing is the one the storage
/// stack already reduces them to.
/// </summary>
public enum DriveHealthState
{
    /// <summary>No usable answer — the query failed, or the drive doesn't report.</summary>
    Unknown = 0,

    /// <summary>Windows considers the drive fine.</summary>
    Healthy = 1,

    /// <summary>Degraded but working. SMART has flagged something.</summary>
    Warning = 2,

    /// <summary>Failing or failed. Back it up now.</summary>
    Unhealthy = 3
}

/// <summary>
/// One physical drive's health.
///
/// Every figure past <see cref="Health"/> is <c>float?</c> and null when
/// unavailable, following the house rule: null means "no sensor, or not
/// elevated", never zero. Non-elevated that's all of them — see
/// <see cref="DiskHealthSnapshot.SmartAvailable"/>.
/// </summary>
/// <param name="Name">Model string as the storage stack reports it.</param>
/// <param name="MediaType">"SSD", "HDD", "SCM" or "Disk" when unspecified.</param>
/// <param name="CapacityGb">Reported capacity, decimal GB, as printed on the box.</param>
/// <param name="Health">Windows' own verdict. Available without elevation.</param>
/// <param name="TemperatureC">Drive temperature. SMART; needs elevation.</param>
/// <param name="RemainingLifePercent">
/// Wear remaining, 100 down to 0. SMART; needs elevation. On NVMe this is
/// 100 minus the "Percentage Used" the controller reports.
/// </param>
/// <param name="DataWrittenTb">Lifetime host writes. SMART; needs elevation.</param>
public record DriveHealth(
    string Name,
    string MediaType,
    double? CapacityGb,
    DriveHealthState Health,
    float? TemperatureC = null,
    float? RemainingLifePercent = null,
    float? DataWrittenTb = null)
{
    /// <summary>
    /// Capacity rendered the way a drive is sold — "500 GB", "1.0 TB" —
    /// rather than the binary figure, which would make a 1 TB disk read 931.
    /// </summary>
    public string CapacityLabel => CapacityGb switch
    {
        null => "--",
        >= 1000 => $"{CapacityGb.Value / 1000.0:0.#} TB",
        _ => $"{CapacityGb.Value:0} GB"
    };

    /// <summary>Short label for the health pip: OK / WARN / FAIL / --.</summary>
    public string HealthLabel => Health switch
    {
        DriveHealthState.Healthy => "OK",
        DriveHealthState.Warning => "WARN",
        DriveHealthState.Unhealthy => "FAIL",
        _ => "--"
    };

    /// <summary>
    /// Whether SMART detail was expected for this drive — i.e. the elevated
    /// read succeeded somewhere in this snapshot.
    ///
    /// This is what separates the two reasons a figure can be missing, and
    /// the panel colours them differently. Non-elevated every drive is blank
    /// and the panel says why once, in a single line, so painting two columns
    /// of amber dashes per drive would be alarm colour spent on the normal
    /// case. A blank when detail *did* come back is the real anomaly, and
    /// gets the amber the house null-render rule asks for.
    /// </summary>
    public bool DetailExpected { get; init; }

    /// <summary>Drive temperature, or "--".</summary>
    public string TemperatureLabel =>
        TemperatureC.HasValue ? $"{TemperatureC.Value:0}°C" : "--";

    /// <summary>Wear remaining, or "--".</summary>
    public string LifeLabel =>
        RemainingLifePercent.HasValue ? $"{RemainingLifePercent.Value:0}%" : "--";

    /// <summary>Lifetime host writes, or "--".</summary>
    public string WrittenLabel =>
        DataWrittenTb.HasValue ? $"{DataWrittenTb.Value:0.0} TB" : "--";

    /// <summary>A temperature that should have been there and wasn't.</summary>
    public bool TemperatureMissing => DetailExpected && TemperatureC == null;

    /// <summary>A wear figure that should have been there and wasn't.</summary>
    public bool LifeMissing => DetailExpected && RemainingLifePercent == null;
}

/// <summary>
/// A point-in-time view of every physical drive, safe to hand to the UI.
///
/// A snapshot for the same reason <see cref="PingSnapshot"/> is one: the
/// queries behind it are far too slow for the one-second poll (WMI's first
/// <c>MSFT_PhysicalDisk</c> call measured ~380ms, ~33ms warm, and a SMART
/// read is slower still), so a background loop refreshes on its own slow
/// cadence and the UI reads whatever is current on its tick.
/// </summary>
/// <param name="Drives">Every physical drive found, in enumeration order.</param>
/// <param name="SmartAvailable">
/// Whether the detailed SMART figures came back. False non-elevated, where
/// Windows still gives a health verdict but the drive-level sensors are
/// refused — so the UI can say "elevate for detail" rather than showing a
/// row of dashes with no explanation.
/// </param>
/// <param name="SampledAt">When this was taken, UTC. Null if never sampled.</param>
/// <param name="Error">Why the last refresh failed, if it did.</param>
public record DiskHealthSnapshot(
    IReadOnlyList<DriveHealth> Drives,
    bool SmartAvailable,
    DateTime? SampledAt,
    string? Error)
{
    /// <summary>Before the first refresh lands.</summary>
    public static DiskHealthSnapshot Empty { get; } =
        new(Array.Empty<DriveHealth>(), false, null, null);

    /// <summary>
    /// The worst verdict across all drives — what the panel reports, since
    /// one failing disk matters regardless of how healthy the others are.
    /// </summary>
    public DriveHealthState Worst
    {
        get
        {
            var worst = DriveHealthState.Unknown;

            foreach (var drive in Drives)
            {
                // Unknown sorts lowest in the enum but is not "better than
                // Healthy" — compare on severity rank, not raw value.
                if (Rank(drive.Health) > Rank(worst))
                    worst = drive.Health;
            }

            return worst;
        }
    }

    private static int Rank(DriveHealthState state) => state switch
    {
        DriveHealthState.Unhealthy => 3,
        DriveHealthState.Warning => 2,
        DriveHealthState.Healthy => 1,
        _ => 0
    };
}
