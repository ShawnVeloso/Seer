using System;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace Seer.Services;

/// <summary>
/// What Seer could and could not reach at startup.
///
/// Every one of these lines is the result of a check the app already performs
/// on launch — the sensor enumeration, the elevation state, the disk counter
/// opening. They were simply never shown, so a machine where half the
/// readings are unavailable looked the same as one where everything worked,
/// except for some amber dashes.
/// </summary>
public sealed record LaunchLine(string Label, string Detail, bool Ok);

public static class LaunchReport
{
    /// <summary>
    /// Builds the report from the live monitor. Cheap: it walks hardware that
    /// has already been opened and enumerated.
    /// </summary>
    public static IReadOnlyList<LaunchLine> Collect(Computer computer)
    {
        var lines = new List<LaunchLine>();

        var devices = 0;
        var sensors = 0;

        try
        {
            foreach (var hardware in computer.Hardware)
            {
                devices++;
                sensors += CountSensors(hardware);
            }

            lines.Add(new LaunchLine(
                "LibreHardwareMonitor",
                $"{devices} devices - {sensors} sensors",
                sensors > 0));
        }
        catch (Exception ex)
        {
            lines.Add(new LaunchLine("LibreHardwareMonitor", ex.GetType().Name, false));
        }

        // The single most useful line on the whole list: without Ring0 the CPU
        // panel is mostly dashes, and this says why.
        lines.Add(ElevationService.IsElevated
            ? new LaunchLine("Ring0 driver", "elevated - CPU temp, clock and power readable", true)
            : new LaunchLine("Ring0 driver", "not elevated - CPU temp, clock and power unavailable", false));

        return lines;
    }

    private static int CountSensors(IHardware hardware)
    {
        var count = hardware.Sensors.Length;

        foreach (var sub in hardware.SubHardware)
            count += sub.Sensors.Length;

        return count;
    }
}
