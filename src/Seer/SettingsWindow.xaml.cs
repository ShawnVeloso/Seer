using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Seer.Models;
using Seer.Services;

namespace Seer;

/// <summary>
/// Edits the settings that aren't window geometry: alert thresholds and
/// launch-at-login.
///
/// Edits are applied to the live <see cref="AppSettings"/> instance only
/// on Save, so Cancel genuinely discards. The instance is shared with
/// MainWindow and OsdWindow, so a saved threshold takes effect on the
/// very next poll without a restart.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// One row of the readout picker. Plain settable properties are
    /// enough: the checkboxes are two-way bound and read back on Save,
    /// and nothing outside the dialog changes them while it's open.
    /// </summary>
    private sealed class ReadoutRow
    {
        public required ReadoutMetric Metric { get; init; }
        public required string Name { get; init; }
        public bool InTray { get; set; }
        public bool InOsd { get; set; }
    }

    private readonly AppSettings _settings;
    private readonly List<ReadoutRow> _readoutRows = new();

    /// <summary>Bounds chosen to reject nonsense, not to be clever.</summary>
    private const float MinLoad = 1f, MaxLoad = 100f;
    private const float MinTemp = 1f, MaxTemp = 150f;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        LoadWarningBox.Text  = Format(settings.LoadWarningThreshold);
        LoadCriticalBox.Text = Format(settings.LoadCriticalThreshold);
        TempWarningBox.Text  = Format(settings.TempWarningThreshold);
        TempCriticalBox.Text = Format(settings.TempCriticalThreshold);

        // The registry is the source of truth for startup, not the
        // settings file — the user may have removed the entry by hand.
        StartWithWindowsBox.IsChecked = StartupService.IsEnabled();

        foreach (var metric in ReadoutFormatter.All)
        {
            _readoutRows.Add(new ReadoutRow
            {
                Metric = metric,
                Name = ReadoutFormatter.DisplayName(metric),
                InTray = settings.TrayMetrics.Contains(metric),
                InOsd = settings.OsdMetrics.Contains(metric)
            });
        }

        ReadoutList.ItemsSource = _readoutRows;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadThresholds(out var loadWarn, out var loadCrit,
                               out var tempWarn, out var tempCrit, out var error))
        {
            ShowError(error);
            return;
        }

        _settings.LoadWarningThreshold  = loadWarn;
        _settings.LoadCriticalThreshold = loadCrit;
        _settings.TempWarningThreshold  = tempWarn;
        _settings.TempCriticalThreshold = tempCrit;

        var wantsStartup = StartWithWindowsBox.IsChecked == true;
        if (wantsStartup != StartupService.IsEnabled() && !StartupService.Apply(wantsStartup))
        {
            // Registry write failed. Say so rather than leaving a
            // checkbox that claims something untrue.
            ShowError("Couldn't update the Windows startup entry. Your other settings were not saved.");
            return;
        }
        _settings.StartWithWindows = wantsStartup;

        // Rebuilt in ReadoutFormatter.All order, so the overlay reads in
        // a stable order rather than the order boxes were ticked.
        _settings.TrayMetrics = _readoutRows.Where(r => r.InTray).Select(r => r.Metric).ToList();
        _settings.OsdMetrics = _readoutRows.Where(r => r.InOsd).Select(r => r.Metric).ToList();

        SettingsService.Save(_settings);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings();

        foreach (var row in _readoutRows)
        {
            row.InTray = defaults.TrayMetrics.Contains(row.Metric);
            row.InOsd = defaults.OsdMetrics.Contains(row.Metric);
        }
        ReadoutList.Items.Refresh();

        LoadWarningBox.Text  = Format(defaults.LoadWarningThreshold);
        LoadCriticalBox.Text = Format(defaults.LoadCriticalThreshold);
        TempWarningBox.Text  = Format(defaults.TempWarningThreshold);
        TempCriticalBox.Text = Format(defaults.TempCriticalThreshold);
        ErrorText.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Parses and sanity-checks all four fields. Warning must sit below
    /// critical, or the evaluator could never report a warning state.
    /// </summary>
    private bool TryReadThresholds(out float loadWarn, out float loadCrit,
                                   out float tempWarn, out float tempCrit,
                                   out string error)
    {
        loadWarn = loadCrit = tempWarn = tempCrit = 0f;

        if (!TryParse(LoadWarningBox.Text, MinLoad, MaxLoad, out loadWarn))
        {
            error = $"Load warning must be a number between {MinLoad:F0} and {MaxLoad:F0}.";
            return false;
        }
        if (!TryParse(LoadCriticalBox.Text, MinLoad, MaxLoad, out loadCrit))
        {
            error = $"Load critical must be a number between {MinLoad:F0} and {MaxLoad:F0}.";
            return false;
        }
        if (!TryParse(TempWarningBox.Text, MinTemp, MaxTemp, out tempWarn))
        {
            error = $"Temperature warning must be a number between {MinTemp:F0} and {MaxTemp:F0}.";
            return false;
        }
        if (!TryParse(TempCriticalBox.Text, MinTemp, MaxTemp, out tempCrit))
        {
            error = $"Temperature critical must be a number between {MinTemp:F0} and {MaxTemp:F0}.";
            return false;
        }

        if (loadWarn >= loadCrit)
        {
            error = "Load warning must be lower than load critical.";
            return false;
        }
        if (tempWarn >= tempCrit)
        {
            error = "Temperature warning must be lower than temperature critical.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParse(string text, float min, float max, out float value) =>
        float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        && value >= min && value <= max;

    private static string Format(float value) => value.ToString("0.#", CultureInfo.CurrentCulture);

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
