using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Reads drive health — Windows' own verdict always, SMART detail when
/// elevated.
///
/// Two sources, because neither alone is enough:
///
/// <list type="bullet">
///   <item>
///     WMI's <c>MSFT_PhysicalDisk</c> gives a health verdict, media type and
///     capacity, and it answers <b>without elevation</b>. This is the floor:
///     Seer ships non-elevated, so a disk panel that showed nothing until the
///     user elevated would be empty for most of its life.
///   </item>
///   <item>
///     LibreHardwareMonitorLib gives temperature, wear and lifetime writes,
///     and needs elevation for all of it. Measured on this machine
///     non-elevated it enumerates <b>zero</b> storage devices — the SMART
///     IOCTLs need a physical-drive handle the user can't open. So this half
///     is a bonus layered on top, never a dependency.
///   </item>
/// </list>
///
/// Neither runs on the poll timer. The first <c>MSFT_PhysicalDisk</c> query
/// measured ~380ms cold and ~33ms warm, and a SMART read is slower again —
/// far past a one-second budget. Following <see cref="PingMonitorService"/>,
/// a background loop owns a slow cadence and publishes an immutable
/// <see cref="DiskHealthSnapshot"/> the UI reads on its own tick. Drive wear
/// moves over months, so refreshing once a minute is already generous.
/// </summary>
public sealed class DiskHealthService : IDisposable
{
    /// <summary>
    /// How often to re-read. Health changes on the order of weeks; this is
    /// really just "often enough to notice a drive being unplugged".
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();

    private DiskHealthSnapshot _snapshot = DiskHealthSnapshot.Empty;
    private Task? _loop;
    private bool _disposed;

    /// <summary>
    /// This service's own <see cref="Computer"/>, deliberately not the one
    /// <see cref="HardwareMonitorService"/> shares.
    ///
    /// A storage <c>Update()</c> issues blocking SMART reads, so it has to
    /// happen on this background loop — and driving the shared instance from
    /// two threads while the UI poll walks its CPU and GPU nodes is not
    /// something LibreHardwareMonitorLib promises to survive. A second
    /// instance scoped to storage keeps the threading trivially correct; the
    /// cost is one object that enumerates nothing at all when non-elevated.
    /// </summary>
    private Computer? _storage;

    /// <summary>Starts the background refresh. Safe to call more than once.</summary>
    public void Start()
    {
        if (_disposed || _loop != null)
            return;

        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// The most recent reading. Cheap enough to call every poll — it hands
    /// back an already-built immutable record rather than querying anything.
    /// </summary>
    public DiskHealthSnapshot GetSnapshot()
    {
        lock (_gate)
            return _snapshot;
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var next = Refresh();

            lock (_gate)
                _snapshot = next;

            try
            {
                await Task.Delay(RefreshInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Builds one snapshot. Never throws: a machine with an exotic storage
    /// driver, a disabled WMI service or a locked-down policy is a degraded
    /// reading, not a crash.
    /// </summary>
    private DiskHealthSnapshot Refresh()
    {
        List<DriveHealth> drives;
        string? error = null;

        try
        {
            drives = QueryWindowsHealth();
        }
        catch (Exception ex)
        {
            return new DiskHealthSnapshot(
                Array.Empty<DriveHealth>(), false, DateTime.UtcNow,
                $"{ex.GetType().Name}: {ex.Message}");
        }

        var smartAvailable = false;

        try
        {
            smartAvailable = MergeSmartDetail(drives);
        }
        catch (Exception ex)
        {
            // The Windows verdict already collected is still worth showing,
            // so a SMART failure downgrades the reading rather than losing it.
            error = $"{ex.GetType().Name}: {ex.Message}";
        }

        // Stamp the whole set, not the drives that happened to answer: the
        // flag means "the elevated read worked on this machine", which is
        // what tells a blank cell apart from a blank panel.
        if (smartAvailable)
        {
            for (var i = 0; i < drives.Count; i++)
                drives[i] = drives[i] with { DetailExpected = true };
        }

        return new DiskHealthSnapshot(drives, smartAvailable, DateTime.UtcNow, error);
    }

    /// <summary>
    /// The non-elevated floor: every physical disk with Windows' own health
    /// verdict, via the Storage Spaces provider.
    /// </summary>
    private static List<DriveHealth> QueryWindowsHealth()
    {
        var drives = new List<DriveHealth>();

        using var searcher = new ManagementObjectSearcher(
            @"root\microsoft\windows\storage",
            "SELECT FriendlyName,MediaType,HealthStatus,Size FROM MSFT_PhysicalDisk");

        foreach (ManagementBaseObject row in searcher.Get())
        {
            using (row)
            {
                var name = row["FriendlyName"] as string;

                drives.Add(new DriveHealth(
                    Name: string.IsNullOrWhiteSpace(name) ? "Unknown drive" : Collapse(name),
                    MediaType: MediaTypeLabel(row["MediaType"]),
                    CapacityGb: DecimalGb(row["Size"]),
                    Health: HealthFrom(row["HealthStatus"])));
            }
        }

        return drives;
    }

    /// <summary>
    /// Layers SMART figures onto the drives already found. Returns whether
    /// any came back — false non-elevated, which is the normal case.
    /// </summary>
    private bool MergeSmartDetail(List<DriveHealth> drives)
    {
        if (drives.Count == 0)
            return false;

        _storage ??= new Computer { IsStorageEnabled = true };

        // Open is idempotent in practice but throws if the driver can't be
        // reached; the caller treats that as "no SMART", which is correct.
        _storage.Open();

        var any = false;

        foreach (IHardware hardware in _storage.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Storage)
                continue;

            hardware.Update();

            var index = IndexOfDrive(drives, hardware.Name);
            if (index < 0)
                continue;

            float? temperature = null;
            float? remainingLife = null;
            float? writtenGb = null;

            foreach (ISensor sensor in hardware.Sensors)
            {
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature when temperature == null:
                        temperature = Valid(sensor.Value);
                        break;

                    case SensorType.Level when Mentions(sensor.Name, "Remaining Life"):
                        remainingLife = Valid(sensor.Value, allowZero: true);
                        break;

                    // Some controllers report wear the other way up.
                    case SensorType.Level when remainingLife == null && Mentions(sensor.Name, "Percentage Used"):
                        var used = Valid(sensor.Value, allowZero: true);
                        if (used.HasValue)
                            remainingLife = Math.Clamp(100f - used.Value, 0f, 100f);
                        break;

                    case SensorType.Data when Mentions(sensor.Name, "Data Written"):
                        writtenGb = Valid(sensor.Value, allowZero: true);
                        break;
                }
            }

            if (temperature == null && remainingLife == null && writtenGb == null)
                continue;

            any = true;

            drives[index] = drives[index] with
            {
                TemperatureC = temperature,
                RemainingLifePercent = remainingLife,
                DataWrittenTb = writtenGb.HasValue ? writtenGb.Value / 1024f : null
            };
        }

        return any;
    }

    /// <summary>
    /// Matches a LibreHardwareMonitorLib drive to a WMI one by model string.
    /// The two spell the same drive differently often enough — doubled spaces,
    /// trailing padding — that an exact compare misses, so both sides are
    /// collapsed first, then tried as prefixes either way round.
    /// </summary>
    private static int IndexOfDrive(List<DriveHealth> drives, string hardwareName)
    {
        var candidate = Collapse(hardwareName);

        for (var i = 0; i < drives.Count; i++)
        {
            if (string.Equals(drives[i].Name, candidate, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        for (var i = 0; i < drives.Count; i++)
        {
            var known = drives[i].Name;

            if (known.Length == 0 || candidate.Length == 0)
                continue;

            if (known.StartsWith(candidate, StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(known, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    /// <summary>Squeezes runs of whitespace and trims — "WDC  WDS100T2B0A" has a double space.</summary>
    public static string Collapse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new System.Text.StringBuilder(value.Length);
        var lastWasSpace = false;

        foreach (var ch in value.Trim())
        {
            var isSpace = char.IsWhiteSpace(ch);

            if (isSpace && lastWasSpace)
                continue;

            builder.Append(isSpace ? ' ' : ch);
            lastWasSpace = isSpace;
        }

        return builder.ToString();
    }

    /// <summary>
    /// MSFT_PhysicalDisk MediaType, per the Storage Spaces provider:
    /// 3 spinning, 4 solid state, 5 storage-class memory.
    /// </summary>
    public static string MediaTypeLabel(object? raw) => ToUInt16(raw) switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => "Disk"
    };

    /// <summary>MSFT_PhysicalDisk HealthStatus: 0 healthy, 1 warning, 2 unhealthy.</summary>
    public static DriveHealthState HealthFrom(object? raw) => ToUInt16(raw) switch
    {
        0 => DriveHealthState.Healthy,
        1 => DriveHealthState.Warning,
        2 => DriveHealthState.Unhealthy,
        _ => DriveHealthState.Unknown
    };

    /// <summary>
    /// Bytes to the decimal GB a drive is sold as, so a 1 TB disk reads 1000
    /// rather than 931.
    /// </summary>
    public static double? DecimalGb(object? raw)
    {
        if (raw == null)
            return null;

        try
        {
            var bytes = Convert.ToUInt64(raw, CultureInfo.InvariantCulture);
            return bytes == 0 ? null : bytes / 1_000_000_000.0;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static int ToUInt16(object? raw)
    {
        if (raw == null)
            return -1;

        try
        {
            return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            return -1;
        }
    }

    private static bool Mentions(string name, string fragment) =>
        name.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    /// <summary>Same "null means unavailable" rule the sensor services use.</summary>
    private static float? Valid(float? value, bool allowZero = false)
    {
        if (value == null || float.IsNaN(value.Value))
            return null;
        if (value.Value < 0)
            return null;
        if (!allowZero && value.Value == 0)
            return null;

        return value;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already torn down.
        }

        try
        {
            _loop?.Wait(TimeSpan.FromMilliseconds(1500));
        }
        catch
        {
            // A faulted or cancelled loop is fine; we're shutting down.
        }

        try
        {
            _storage?.Close();
        }
        catch
        {
            // Closing a monitor that never opened is not worth a crash on exit.
        }

        _cts.Dispose();
    }
}
