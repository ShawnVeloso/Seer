using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Seer.Services;

/// <summary>
/// Launch-at-login, via the per-user Run key.
///
/// HKCU rather than HKLM on purpose: it needs no elevation to write, and
/// entries there start unprivileged. That matches how Seer is meant to
/// start — unelevated, with the user clicking RUN AS ADMIN only when they
/// want the gated CPU metrics. An HKLM entry or a scheduled task could
/// auto-start it elevated, but silently granting admin at every login is
/// not a trade this app should make for a temperature reading.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Seer";

    /// <summary>
    /// True when a Run entry for Seer exists. Doesn't check that the path
    /// still resolves — a stale entry is repaired by <see cref="Apply"/>.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Adds or removes the Run entry. Returns false if the registry
    /// rejected the change, so the caller can tell the user rather than
    /// leaving a checkbox that silently does nothing.
    /// </summary>
    public static bool Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null)
                return false;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                return false;

            // Quoted: the path may contain spaces, and an unquoted Run
            // value would be parsed as a command plus arguments.
            key.SetValue(ValueName, $"\"{exePath}\"");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
