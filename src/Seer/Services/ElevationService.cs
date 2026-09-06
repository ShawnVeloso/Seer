using System;
using System.Diagnostics;
using System.Security.Principal;

namespace Seer.Services;

/// <summary>
/// Everything to do with running (or not running) as administrator.
///
/// Seer starts unprivileged on purpose and only asks for elevation when
/// the user wants the gated CPU metrics — temperature, clock and package
/// power, which need LibreHardwareMonitorLib's Ring0 driver.
/// </summary>
public static class ElevationService
{
    /// <summary>True when the current process is running elevated.</summary>
    public static bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity)
                    .IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                // If we can't tell, assume not elevated — the UI degrades
                // to "--" placeholders, which is the safe direction.
                return false;
            }
        }
    }

    /// <summary>
    /// Relaunches Seer elevated via the UAC prompt.
    /// Returns true if the new process started, meaning the caller should
    /// shut this one down. Returns false when the user dismisses the
    /// prompt — the caller keeps running unprivileged.
    /// </summary>
    public static bool TryRelaunchElevated()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled the UAC prompt. Not an error.
            return false;
        }
    }
}
