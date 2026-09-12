using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using Seer.Controls;
using Seer.Services;
using Seer.Models;

namespace Seer;

/// <summary>
/// The main window's lifecycle and orchestration: constructing the
/// services, driving the one-second poll, restoring and persisting
/// geometry, and owning the tray icon and OSD.
///
/// Rendering lives in MainWindow.Panels.cs. Anything that doesn't need
/// the visual tree has moved out entirely — see TrayIconController,
/// HudBackground, ElevationService and WindowPlacement.
/// </summary>
public partial class MainWindow : Window
{
    private readonly HardwareMonitorService _monitor;
    private readonly ProcessMonitorService _processMonitor;
    private readonly DiskMonitorService _diskMonitor;
    private readonly NetworkMonitorService _networkMonitor;
    private readonly PingMonitorService _pingMonitor = new();
    private readonly DispatcherTimer _pollTimer;

    // History queues for trend charts
    private readonly Queue<float> _cpuHistory = new();
    private readonly Queue<float> _memHistory = new();
    private readonly Queue<float> _gpuHistory = new();
    private const int MaxHistory = 120;

    // Cached brushes from theme resources for elevation-aware display
    private readonly SolidColorBrush _normalBrush;
    private readonly SolidColorBrush _warningBrush;
    private readonly SolidColorBrush _dimBrush;
    private readonly SolidColorBrush _successBrush;
    private readonly SolidColorBrush _dangerBrush;

    // Badge backgrounds. Cached for the same reason as the brushes above:
    // UpdateStatusBadge runs every second, and resolving a resource plus
    // allocating a brush per tick is needless garbage on an always-on window.
    private readonly SolidColorBrush _successTintBrush;
    private readonly SolidColorBrush _warningTintBrush;
    private readonly SolidColorBrush _dangerTintBrush;

    // Peak-hold markers for the status strip. One per channel: a burst
    // between two glances would otherwise leave no trace on the bar.
    private readonly PeakHold _cpuPeak = new();

    // One per core, rebuilt if the core count ever changes.
    private PeakHold[] _corePeaks = Array.Empty<PeakHold>();
    private readonly PeakHold _memPeak = new();
    private readonly PeakHold _gpuPeak = new();

    // Panel edges hold a severity for a few polls after it clears, so a
    // reading sitting on its threshold doesn't flicker the whole panel.
    // Marks processes that climbed into or up the top list, held for a few
    // polls so a one-second arrow is readable.
    private readonly ProcessRankTracker _rankTracker = new();

    // Times the poll itself, and spots one that arrived late.
    private readonly PollClock _pollClock = new();

    // Which way the headline readings are heading. Temperatures get a wider
    // deadband than loads: a load swings by its nature, where a degree of
    // drift over ten seconds is a real thermal trend.
    private readonly SlopeTracker _cpuTempSlope = new(deadbandPerSecond: 0.03);
    private readonly SlopeTracker _cpuLoadSlope = new(deadbandPerSecond: 0.5);
    private readonly SlopeTracker _gpuTempSlope = new(deadbandPerSecond: 0.03);
    private readonly SlopeTracker _gpuLoadSlope = new(deadbandPerSecond: 0.5);

    // Session low/mean/high per chart. These outlive the 120-second plot, so
    // a peak that scrolled off an hour ago is still readable.
    private readonly SessionStats _cpuSession = new();
    private readonly SessionStats _memSession = new();
    private readonly SessionStats _gpuSession = new();

    // Disk has no cumulative counter to read, so its session totals are
    // integrated from the per-second rates and shown as estimates.
    private readonly RateIntegrator _diskReadTotal = new();
    private readonly RateIntegrator _diskWriteTotal = new();

    private readonly SeverityHold _cpuSeverity = new();
    private readonly SeverityHold _memSeverity = new();
    private readonly SeverityHold _gpuSeverity = new();

    private readonly ThresholdEvaluator _thresholdEvaluator = new();
    private AppSettings _appSettings = new();

    // Threshold crossings and app events share one session log: when a
    // reading jumps, the reason is often something the app itself did.
    private readonly SessionLog _events = new();

    // Processes already reported as heavy, so one busy process produces one
    // event rather than one per second for as long as it stays busy.
    private readonly HashSet<int> _heavyProcesses = new();
    
    private OsdWindow? _osdWindow;
    private TrayIconController? _trayIcon;
    private TrayMetricIcons? _trayMetrics;
    private bool _isExplicitShutdown = false;

    public MainWindow()
    {
        InitializeComponent();

        VersionLabel.Text = AppVersion.Display;

        _normalBrush = (SolidColorBrush)FindResource("SeerText");
        _warningBrush = (SolidColorBrush)FindResource("SeerWarning");
        _dimBrush = (SolidColorBrush)FindResource("SeerTextDim");
        _successBrush = (SolidColorBrush)FindResource("SeerSuccess");
        _dangerBrush = (SolidColorBrush)FindResource("SeerDanger");
        _successTintBrush = (SolidColorBrush)FindResource("SeerSuccessTint");
        _warningTintBrush = (SolidColorBrush)FindResource("SeerWarningTint");
        _dangerTintBrush = (SolidColorBrush)FindResource("SeerDangerTint");

        // Nothing to elevate to if we're already elevated.
        if (ElevationService.IsElevated)
            ElevateButton.Visibility = Visibility.Collapsed;

        _monitor = new HardwareMonitorService();
        _monitor.Open();

        _processMonitor = new ProcessMonitorService();
        _diskMonitor = new DiskMonitorService();
        _networkMonitor = new NetworkMonitorService();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pollTimer.Tick += PollTimer_Tick;
        _pollTimer.Start();

        // OsdWindow is now spawned dynamically based on settings in MainWindow_Loaded

        // Run an immediate first update so panels don't sit empty for 1s
        UpdatePanels();

        ApplyBackgroundGrid();

        // Fetch static system info once at startup — not on the polling timer.
        PopulateSystemInfo();

        AlertsList.ItemsSource = _events.Entries;

        RecordLaunchReport();

        // Windows can turn animations off while the app is running. This is a
        // static event, so it roots the window until OnClosed unhooks it.
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;

        // --- Settings persistence ---
        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// Writes what the app could reach at startup into the event log: sensor
    /// counts and whether Ring0 is available. Without this, a machine where
    /// half the CPU readings are unavailable looks the same as a healthy one
    /// except for some amber dashes with no explanation.
    /// </summary>
    private void RecordLaunchReport()
    {
        if (!HudConfig.EnableLaunchLog)
            return;

        foreach (var line in LaunchReport.Collect(_monitor.Computer))
        {
            _events.Add(
                line.Label,
                line.Detail,
                line.Ok ? AlertSeverity.Nominal : AlertSeverity.Warning);
        }
    }

    /// <summary>
    /// Applies persisted window geometry after the window has been shown
    /// and measured. Using Loaded (not the constructor) ensures that WPF
    /// has already applied XAML defaults, so we only override what the
    /// settings file explicitly provides.
    /// </summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _appSettings = SettingsService.Load();

        Width = _appSettings.WindowWidth;
        Height = _appSettings.WindowHeight;

        // Restore window state (Normal or Maximized; never Minimized).
        if (Enum.TryParse<WindowState>(_appSettings.WindowState, out var state)
            && state != WindowState.Minimized)
        {
            WindowState = state;
        }

        // Restore position only if the saved coordinates place the
        // window at least partially on a currently-connected monitor.
        if (!double.IsNaN(_appSettings.WindowLeft) && !double.IsNaN(_appSettings.WindowTop)
            && WindowPlacement.IsOnScreen(_appSettings.WindowLeft, _appSettings.WindowTop,
                                         _appSettings.WindowWidth, _appSettings.WindowHeight))
        {
            Left = _appSettings.WindowLeft;
            Top = _appSettings.WindowTop;
        }
        // else: leave WPF's default CenterScreen / system placement.
        
        ApplyOsdSettings();
        SetupTrayIcon();
        ApplyTrayReadoutSettings();

        PingHostBox.Text = _appSettings.PingHost;
        UpdatePingPanel();
    }

    /// <summary>
    /// Creates the tray icon and connects its menu to app state. The
    /// controller owns the Win32 icon; this method owns what the choices
    /// mean — persisting each toggle so it survives a restart.
    /// </summary>
    private void SetupTrayIcon()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        _trayIcon = new TrayIconController(
            exePath, _appSettings.ShowOsd, _appSettings.LockOsd, _appSettings.ShowTrayReadouts);

        _trayIcon.ShowRequested += () =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };

        _trayIcon.ExitRequested += () =>
        {
            _isExplicitShutdown = true;
            Application.Current.Shutdown();
        };

        _trayIcon.SettingsRequested += OpenSettings;

        _trayIcon.ShowTrayReadoutsChanged += show =>
        {
            _appSettings.ShowTrayReadouts = show;
            SettingsService.Save(_appSettings);
            ApplyTrayReadoutSettings();
        };

        _trayIcon.ShowOsdChanged += showOsd =>
        {
            _appSettings.ShowOsd = showOsd;
            SettingsService.Save(_appSettings);
            ApplyOsdSettings();
        };

        _trayIcon.LockOsdChanged += lockOsd =>
        {
            _appSettings.LockOsd = lockOsd;
            SettingsService.Save(_appSettings);
            ApplyOsdSettings();
        };
    }

    /// <summary>
    /// Creates or tears down the taskbar metric icons to match settings.
    /// Safe to call repeatedly — it rebuilds the icon set from scratch,
    /// which is why it's called on settings changes and not per poll.
    /// </summary>
    private void ApplyTrayReadoutSettings()
    {
        if (_appSettings.ShowTrayReadouts && _appSettings.TrayMetrics.Count > 0)
        {
            if (_trayMetrics == null)
            {
                _trayMetrics = new TrayMetricIcons();
                _trayMetrics.ShowRequested += () =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                };
            }

            _trayMetrics.SetMetrics(_appSettings.TrayMetrics);
        }
        else
        {
            _trayMetrics?.Dispose();
            _trayMetrics = null;
        }
    }

    /// <summary>
    /// Opens the settings dialog. Edits land on the shared AppSettings
    /// instance, so new thresholds apply on the next poll — no restart.
    /// </summary>
    private void OpenSettings()
    {
        // Reachable from the tray while the window is hidden; show it
        // first so the dialog has something to centre on.
        if (!IsVisible)
        {
            Show();
            WindowState = WindowState.Normal;
        }
        Activate();

        if (new SettingsWindow(_appSettings) { Owner = this }.ShowDialog() == true)
        {
            // Metric selection may have changed; rebuild the icons and
            // let the overlay pick up its new list on the next poll.
            ApplyTrayReadoutSettings();

            // Thresholds may have moved, which changes what counts as an
            // alert from here on — worth a line in the log.
            _events.Add("SETTINGS", "saved");
        }
    }

    private void ApplyOsdSettings()
    {
        if (_appSettings.ShowOsd)
        {
            if (_osdWindow == null)
            {
                _osdWindow = new OsdWindow(_appSettings);
                _osdWindow.Show();
                // Send immediate stats
                _osdWindow.UpdateStats(_monitor.GetCpuMetrics(), _monitor.GetGpuMetrics(), _monitor.GetMemoryMetrics());
            }
            else
            {
                _osdWindow.ApplyLockState();
            }
        }
        else
        {
            if (_osdWindow != null)
            {
                _osdWindow.Close();
                _osdWindow = null;
            }
        }
    }

    /// <summary>
    /// Minimizes to tray if not explicitly shutting down.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitShutdown)
        {
            e.Cancel = true;
            SaveWindowSettings(); // Save layout before hiding
            Hide();
            return;
        }

        SaveWindowSettings();
        _diskMonitor?.Dispose();
        _processMonitor?.Dispose();
        base.OnClosing(e);
    }

    private void SaveWindowSettings()
    {
        var settings = SettingsService.Load();

        if (WindowState == WindowState.Maximized)
        {
            // RestoreBounds captures the pre-maximize geometry.
            settings.WindowWidth = RestoreBounds.Width;
            settings.WindowHeight = RestoreBounds.Height;
            settings.WindowLeft = RestoreBounds.Left;
            settings.WindowTop = RestoreBounds.Top;
            settings.WindowState = nameof(System.Windows.WindowState.Maximized);
        }
        else
        {
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            settings.WindowState = nameof(System.Windows.WindowState.Normal);
        }

        SettingsService.Save(settings);
    }

    /// <summary>Applies the HUD background grid, if the effect is enabled.</summary>
    private void ApplyBackgroundGrid()
    {
        var brush = HudBackground.CreateGridBrush();
        if (brush != null)
            RootBorder.Background = brush;
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        UpdatePanels();
    }

    private void ElevateButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Only shut down once the elevated instance has actually started —
        // a dismissed UAC prompt leaves us running unprivileged.
        if (ElevationService.TryRelaunchElevated())
        {
            _isExplicitShutdown = true;
            Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// Keeps the motion gate in step with the Windows animation setting, so
    /// turning animations off takes effect without a restart.
    /// </summary>
    private static void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            HudConfig.RefreshSystemAnimationSetting();
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        _trayIcon?.Dispose();
        _trayMetrics?.Dispose();
        _pollTimer.Stop();
        _pingMonitor.Dispose();
        _monitor.Dispose();
        _osdWindow?.Close();
        base.OnClosed(e);
    }


    /// <summary>
    /// Starts or stops the latency monitor. This is the only control in
    /// Seer that causes network traffic, so it is always an explicit
    /// action — never resumed on launch.
    /// </summary>
    private void PingToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pingMonitor.IsRunning)
        {
            _pingMonitor.Stop();
            _events.Add("PING", "stopped");
        }
        else
        {
            var host = PingHostBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(host))
                return;

            // Remember the host, but not that it was running.
            _appSettings.PingHost = host;
            SettingsService.Save(_appSettings);

            _pingMonitor.Start(host, TimeSpan.FromSeconds(_appSettings.PingIntervalSeconds));

            // The one monitor that sends traffic, so starting it is worth
            // recording next to the readings it will change.
            _events.Add("PING", $"started - {host}");
        }

        UpdatePingPanel();
    }

    private void SettingsButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenSettings();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Fetches static system info via WMI + LHM once and populates the UI.
    /// Called from the constructor — never from the polling timer.
    /// </summary>
    private void PopulateSystemInfo()
    {
        var info = SystemInfoService.Collect(_monitor.Computer);

        SysMotherboard.Text = info.MotherboardName;
        SysBios.Text = $"{info.BiosVersion} ({info.BiosDate})";
        SysCpu.Text = info.CpuModel;
        SysCores.Text = $"{info.CpuCores}C / {info.CpuThreads}T";
        SysRam.Text = info.RamSummary;
        SysDimmSlots.Text = $"{info.RamSlotsUsed} / {info.RamSlotsTotal}";
        SysGpu.Text = info.GpuModel;
    }

}
