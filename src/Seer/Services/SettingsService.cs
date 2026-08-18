using System;
using System.IO;
using System.Text.Json;
using Seer.Models;

namespace Seer.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> to a per-user JSON file.
/// All failure modes (missing file, corrupt JSON, IO errors) silently
/// return defaults — the app must never crash due to a settings problem.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Seer");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Allow trailing commas and comments so hand-edited files don't
        // blow up deserialization.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads settings from disk. Returns defaults on any failure.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            // Corrupt JSON, permission error, etc. — fall back silently.
            return new AppSettings();
        }
    }

    /// <summary>
    /// Persists settings to disk. Failures are swallowed — losing a
    /// window-position preference is not worth crashing the app.
    /// </summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Swallow — best-effort persistence.
        }
    }
}
