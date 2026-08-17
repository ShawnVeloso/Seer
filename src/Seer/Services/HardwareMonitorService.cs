using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using LibreHardwareMonitor.Hardware;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Wraps LibreHardwareMonitorLib's <see cref="Computer"/> for hardware
/// sensor access. Separated from UI per AGENTS.md §4 — sensor logic
/// and display rendering must not share a file.
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private bool _isOpen;

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
    }

    /// <summary>
    /// Opens the hardware monitor connection. Requires admin privileges
    /// to access most sensor data (CPU temps, GPU metrics, etc.).
    /// </summary>
    public void Open()
    {
        if (!_isOpen)
        {
            _computer.Open();
            _isOpen = true;
        }
    }

    /// <summary>
    /// Closes the hardware monitor connection and releases resources.
    /// </summary>
    public void Close()
    {
        if (_isOpen)
        {
            _computer.Close();
            _isOpen = false;
        }
    }

    /// <summary>
    /// Returns a snapshot of current CPU sensor readings.
    /// Fields that require admin elevation (Temperature, Clock, Power)
    /// will be null when running non-elevated — callers should handle
    /// this gracefully rather than displaying raw NaN/0 values.
    /// </summary>
    public CpuMetrics GetCpuMetrics()
    {
        Open();

        float? temperature = null;
        float? totalLoad = null;
        float? clock = null;
        float? power = null;
        var coreLoadsList = new List<(string Name, float Load)>();

        foreach (IHardware hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu)
                continue;

            hardware.Update();

            foreach (ISensor sensor in hardware.Sensors)
            {
                // CPU Total Load — works without admin
                if (sensor.SensorType == SensorType.Load)
                {
                    if (sensor.Name == "CPU Total")
                    {
                        totalLoad = sensor.Value;
                    }
                    else if (System.Text.RegularExpressions.Regex.IsMatch(sensor.Name, @"Core #\d+") && sensor.Value.HasValue)
                    {
                        coreLoadsList.Add((sensor.Name, sensor.Value.Value));
                    }
                }
                // Package power — requires admin (returns 0 without)
                else if (sensor.SensorType == SensorType.Power && sensor.Name == "Package")
                {
                    power = NullIfInvalid(sensor.Value);
                }
                // First core clock as representative — requires admin (returns NaN without)
                else if (sensor.SensorType == SensorType.Clock && sensor.Name == "Core #1" && clock == null)
                {
                    clock = NullIfInvalid(sensor.Value);
                }
            }

            // Temperature lives in sub-hardware on some AMD CPUs,
            // but on Ryzen it's typically on the main hardware node
            // as "Core (Tctl/Tdie)"
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature &&
                    sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase))
                {
                    temperature = NullIfInvalid(sensor.Value);
                    break;
                }
            }

            // Fallback: try any Temperature sensor if Tctl not found
            if (temperature == null)
            {
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        temperature = NullIfInvalid(sensor.Value);
                        break;
                    }
                }
            }

            break; // Only process first CPU
        }

        var sortedCoreLoads = coreLoadsList
            .OrderBy(c => c.Name.Length)
            .ThenBy(c => c.Name)
            .ToArray();

        return new CpuMetrics
        {
            Temperature = temperature,
            TotalLoad = totalLoad,
            Clock = clock,
            Power = power,
            CoreLoads = sortedCoreLoads
        };
    }

    /// <summary>
    /// Returns a snapshot of current memory sensor readings.
    /// All fields work without admin elevation.
    /// </summary>
    public MemoryMetrics GetMemoryMetrics()
    {
        Open();

        float? usedGb = null;
        float? availableGb = null;
        float? load = null;

        foreach (IHardware hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Memory)
                continue;

            hardware.Update();

            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Load && sensor.Name == "Memory")
                {
                    load = sensor.Value;
                }
                else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Used")
                {
                    usedGb = sensor.Value;
                }
                else if (sensor.SensorType == SensorType.Data && sensor.Name == "Memory Available")
                {
                    availableGb = sensor.Value;
                }
            }

            break; // Only one memory device
        }

        return new MemoryMetrics
        {
            UsedGb = usedGb,
            AvailableGb = availableGb,
            Load = load
        };
    }

    /// <summary>
    /// Returns a snapshot of current GPU sensor readings.
    /// Most GPU sensors work without admin elevation.
    /// </summary>
    public GpuMetrics GetGpuMetrics()
    {
        Open();

        float? temp = null;
        float? hotSpot = null;
        float? fanRpm = null;
        float? fanPercent = null;
        float? load = null;
        float? clock = null;
        float? vramUsed = null;
        float? vramTotal = null;

        foreach (IHardware hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.GpuNvidia && 
                hardware.HardwareType != HardwareType.GpuAmd &&
                hardware.HardwareType != HardwareType.GpuIntel)
                continue;

            hardware.Update();

            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature)
                {
                    if (sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase))
                        hotSpot = NullIfInvalid(sensor.Value);
                    else if (sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) || temp == null)
                        temp = NullIfInvalid(sensor.Value);
                }
                else if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                {
                    load = NullIfInvalid(sensor.Value, allowZero: true);
                }
                else if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                {
                    clock = NullIfInvalid(sensor.Value);
                }
                else if (sensor.SensorType == SensorType.Fan)
                {
                    fanRpm = NullIfInvalid(sensor.Value, allowZero: true);
                }
                else if (sensor.SensorType == SensorType.Control)
                {
                    fanPercent = NullIfInvalid(sensor.Value, allowZero: true);
                }
                else if (sensor.SensorType == SensorType.SmallData || sensor.SensorType == SensorType.Data)
                {
                    if (sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase))
                        vramUsed = NullIfInvalid(sensor.Value, allowZero: true);
                    else if (sensor.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase))
                        vramTotal = NullIfInvalid(sensor.Value);
                }
            }

            break; // Only process first GPU
        }

        // LibreHardwareMonitorLib returns GPU memory in MB for SmallData.
        // Convert to GB for consistency with MemoryMetrics.
        if (vramUsed.HasValue) vramUsed = vramUsed.Value / 1024f;
        if (vramTotal.HasValue) vramTotal = vramTotal.Value / 1024f;

        return new GpuMetrics
        {
            Temperature = temp,
            HotSpotTemperature = hotSpot,
            FanRpm = fanRpm,
            FanPercent = fanPercent,
            Load = load,
            Clock = clock,
            VramUsedGb = vramUsed,
            VramTotalGb = vramTotal
        };
    }

    /// <summary>
    /// Returns null for sensor values that indicate "not available" —
    /// NaN, negative, or (optionally) 0. These are the values LibreHardwareMonitorLib
    /// returns for elevation-gated sensors when running non-elevated.
    /// </summary>
    private static float? NullIfInvalid(float? value, bool allowZero = false)
    {
        if (value == null || float.IsNaN(value.Value))
            return null;
        if (!allowZero && value.Value <= 0)
            return null;
        if (allowZero && value.Value < 0)
            return null;
        return value;
    }

    /// <summary>
    /// Runs a one-time smoke test: opens the monitor, enumerates all
    /// detected hardware and their sensors, and returns a summary string.
    /// Kept for future debugging — not used by the live polling path.
    /// </summary>
    public string RunSmokeTest()
    {
        var sb = new StringBuilder();

        try
        {
            Open();

            int hardwareCount = 0;
            int sensorCount = 0;

            foreach (IHardware hardware in _computer.Hardware)
            {
                hardwareCount++;
                hardware.Update();

                sb.AppendLine($"  Hardware: {hardware.Name} ({hardware.HardwareType})");

                foreach (ISensor sensor in hardware.Sensors)
                {
                    sensorCount++;
                    sb.AppendLine($"    Sensor: {sensor.Name} | {sensor.SensorType} = {sensor.Value}");
                }

                // Also check sub-hardware (e.g. individual CPU cores)
                foreach (IHardware subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    sb.AppendLine($"  Sub-hardware: {subHardware.Name} ({subHardware.HardwareType})");

                    foreach (ISensor sensor in subHardware.Sensors)
                    {
                        sensorCount++;
                        sb.AppendLine($"    Sensor: {sensor.Name} | {sensor.SensorType} = {sensor.Value}");
                    }
                }
            }

            sb.Insert(0, $"Smoke test result: {hardwareCount} hardware device(s), {sensorCount} sensor(s) detected.\n");

            if (sensorCount == 0)
            {
                sb.AppendLine();
                sb.AppendLine("WARNING: No sensors detected. This usually means the app");
                sb.AppendLine("is not running with administrator privileges. LibreHardwareMonitorLib");
                sb.AppendLine("requires elevation to access hardware sensors.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Smoke test FAILED with exception: {ex.GetType().Name}");
            sb.AppendLine($"  Message: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("This may indicate missing admin privileges or unsupported hardware.");
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        Close();
    }
}
