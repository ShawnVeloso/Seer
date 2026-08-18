using System;
using System.Linq;
using System.Management;
using LibreHardwareMonitor.Hardware;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Collects static system hardware metadata once at startup.
/// Uses WMI (System.Management) for motherboard, BIOS, CPU, and RAM info.
/// Uses the existing LibreHardwareMonitorLib Computer instance for GPU model.
///
/// Data source per field:
///   Motherboard  → WMI Win32_BaseBoard (Manufacturer + Product)
///   BIOS         → WMI Win32_BIOS (SMBIOSBIOSVersion, ReleaseDate)
///   CPU          → WMI Win32_Processor (Name, NumberOfCores, NumberOfLogicalProcessors)
///   RAM          → WMI Win32_PhysicalMemory + Win32_PhysicalMemoryArray
///   GPU          → LibreHardwareMonitorLib IHardware.Name (GPU hardware type)
///
/// System.Management is already a transitive dependency of LibreHardwareMonitorLib,
/// so this adds zero new packages to the project.
/// </summary>
public static class SystemInfoService
{
    /// <summary>
    /// Collects all static system info. Call once at startup — not on the polling timer.
    /// </summary>
    /// <param name="computer">
    /// The already-opened LibreHardwareMonitorLib Computer instance, used to read the GPU model name.
    /// </param>
    public static SystemInfo Collect(Computer computer)
    {
        string moboName = "Unknown";
        string biosVersion = "Unknown";
        string biosDate = "Unknown";
        string cpuModel = "Unknown";
        int cpuCores = 0;
        int cpuThreads = 0;
        string gpuModel = "Unknown";

        // --- Motherboard (WMI) ---
        try
        {
            using var moboQuery = new ManagementObjectSearcher(
                "SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (ManagementObject obj in moboQuery.Get())
            {
                string mfr = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                string product = obj["Product"]?.ToString()?.Trim() ?? "";
                moboName = string.IsNullOrEmpty(mfr) ? product : $"{mfr} {product}";
                break;
            }
        }
        catch { /* WMI query failed — leave as "Unknown" */ }

        // --- BIOS (WMI) ---
        try
        {
            using var biosQuery = new ManagementObjectSearcher(
                "SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
            foreach (ManagementObject obj in biosQuery.Get())
            {
                biosVersion = obj["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "Unknown";

                string? rawDate = obj["ReleaseDate"]?.ToString();
                if (!string.IsNullOrEmpty(rawDate) && rawDate.Length >= 8)
                {
                    // WMI date format: "20240322000000.000000+000" → "2024-03-22"
                    biosDate = $"{rawDate[..4]}-{rawDate[4..6]}-{rawDate[6..8]}";
                }
                break;
            }
        }
        catch { /* WMI query failed — leave as "Unknown" */ }

        // --- CPU (WMI) ---
        try
        {
            using var cpuQuery = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject obj in cpuQuery.Get())
            {
                cpuModel = obj["Name"]?.ToString()?.Trim() ?? "Unknown";
                cpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                cpuThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                break;
            }
        }
        catch { /* WMI query failed — leave defaults */ }

        // --- RAM (WMI) ---
        string ramSummary = "Unknown";
        int ramSlotsUsed = 0;
        int ramSlotsTotal = 0;

        try
        {
            long totalCapacityBytes = 0;
            int configuredSpeed = 0;
            int smbiosMemoryType = 0;
            int dimmCount = 0;

            using var ramQuery = new ManagementObjectSearcher(
                "SELECT Capacity, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            foreach (ManagementObject obj in ramQuery.Get())
            {
                dimmCount++;
                totalCapacityBytes += Convert.ToInt64(obj["Capacity"] ?? 0);

                // Take the first DIMM's speed and type as representative
                if (configuredSpeed == 0)
                    configuredSpeed = Convert.ToInt32(obj["ConfiguredClockSpeed"] ?? 0);
                if (smbiosMemoryType == 0)
                    smbiosMemoryType = Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0);
            }

            ramSlotsUsed = dimmCount;

            int totalGb = (int)(totalCapacityBytes / (1024L * 1024L * 1024L));
            string memType = smbiosMemoryType switch
            {
                26 => "DDR4",
                34 => "DDR5",
                24 => "DDR3",
                22 => "DDR2",
                _ => $"Type {smbiosMemoryType}"
            };

            ramSummary = configuredSpeed > 0
                ? $"{totalGb} GB {memType} @ {configuredSpeed} MHz"
                : $"{totalGb} GB {memType}";

            // Total slot count
            using var slotQuery = new ManagementObjectSearcher(
                "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
            foreach (ManagementObject obj in slotQuery.Get())
            {
                ramSlotsTotal = Convert.ToInt32(obj["MemoryDevices"] ?? 0);
                break;
            }
        }
        catch { /* WMI query failed — leave defaults */ }

        // --- GPU model (from LibreHardwareMonitorLib) ---
        try
        {
            foreach (IHardware hw in computer.Hardware)
            {
                if (hw.HardwareType == HardwareType.GpuNvidia ||
                    hw.HardwareType == HardwareType.GpuAmd ||
                    hw.HardwareType == HardwareType.GpuIntel)
                {
                    gpuModel = hw.Name;
                    break;
                }
            }
        }
        catch { /* Leave as "Unknown" */ }

        return new SystemInfo
        {
            MotherboardName = moboName,
            BiosVersion = biosVersion,
            BiosDate = biosDate,
            CpuModel = cpuModel,
            CpuCores = cpuCores,
            CpuThreads = cpuThreads,
            RamSummary = ramSummary,
            RamSlotsUsed = ramSlotsUsed,
            RamSlotsTotal = ramSlotsTotal,
            GpuModel = gpuModel
        };
    }
}
