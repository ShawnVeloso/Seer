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
    private readonly DispatcherTimer _pollTimer;

    // History queues for trend charts
    private readonly Queue<float> _cpuHistory = new();
    private readonly Queue<float> _memHistory = new();
    private readonly Queue<float> _gpuHistory = new();
    private const int MaxHistory = 120;

    // Cached brushes from theme resources for elevation-aware display
    private readonly SolidColorBrush _normalBrush;
    private readonly SolidColorBrush _warningBrush;

    private readonly ThresholdEvaluator _thresholdEvaluator = new();
    private AppSettings _appSettings = new();
    private readonly ObservableCollection<AlertEvent> _alerts = new();
    private const int MaxAlerts = 50;
    
    private OsdWindow? _osdWindow;
    private TrayIconController? _trayIcon;
    private bool _isExplicitShutdown = false;

    public MainWindow()
    {
        InitializeComponent();

        VersionLabel.Text = AppVersion.Display;

        _normalBrush = (SolidColorBrush)FindResource("SeerText");
        _warningBrush = (SolidColorBrush)FindResource("SeerWarning");

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

        AlertsList.ItemsSource = _alerts;

        // --- Settings persistence ---
        Loaded += MainWindow_Loaded;
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
    }

    /// <summary>
    /// Creates the tray icon and connects its menu to app state. The
    /// controller owns the Win32 icon; this method owns what the choices
    /// mean — persisting each toggle so it survives a restart.
    /// </summary>
    private void SetupTrayIcon()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        _trayIcon = new TrayIconController(exePath, _appSettings.ShowOsd, _appSettings.LockOsd);

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

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon?.Dispose();
        _pollTimer.Stop();
        _monitor.Dispose();
        _osdWindow?.Close();
        base.OnClosed(e);
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

    private void AlertsHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (AlertsContent.Visibility == Visibility.Collapsed)
        {
            AlertsContent.Visibility = Visibility.Visible;
            AlertsHeaderText.Text = "[7] ALERTS ▾";
        }
        else
        {
            AlertsContent.Visibility = Visibility.Collapsed;
            AlertsHeaderText.Text = "[7] ALERTS ▸";
        }
    }

    private void SystemInfoHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SystemInfoContent.Visibility == Visibility.Collapsed)
        {
            SystemInfoContent.Visibility = Visibility.Visible;
            SystemInfoHeaderText.Text = "[i] SYSTEM INFO ▾";
        }
        else
        {
            SystemInfoContent.Visibility = Visibility.Collapsed;
            SystemInfoHeaderText.Text = "[i] SYSTEM INFO ▸";
        }
    }
}
