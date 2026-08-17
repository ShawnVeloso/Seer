using System;
using System.Text;
using LibreHardwareMonitor.Hardware;

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
    /// Runs a one-time smoke test: opens the monitor, enumerates all
    /// detected hardware and their sensors, and returns a summary string.
    /// This is a temporary diagnostic — will be replaced by real polling.
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
