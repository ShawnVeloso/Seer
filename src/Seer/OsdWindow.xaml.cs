using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Documents;
using System.Windows.Media;
using Seer.Models;
using Seer.Services;

namespace Seer;

public partial class OsdWindow : Window
{
    private readonly AppSettings _settings;

    public OsdWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        SourceInitialized += OsdWindow_SourceInitialized;

        if (double.IsNaN(_settings.OsdX) || double.IsNaN(_settings.OsdY))
        {
            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = 20;
        }
        else if (WindowPlacement.IsOnScreen(_settings.OsdX, _settings.OsdY, Width, Height))
        {
            Left = _settings.OsdX;
            Top = _settings.OsdY;
        }
        else
        {
            Left = SystemParameters.PrimaryScreenWidth - Width - 20;
            Top = 20;
        }
        
        LocationChanged += OsdWindow_LocationChanged;
    }

    // ── Win32 interop ──────────────────────────────────────────────────

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // ── Apply extended styles on first render ──────────────────────────

    private void OsdWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyLockState();
    }

    public void ApplyLockState()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (_settings.LockOsd)
        {
            exStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
            RootBorder.Background = Brushes.Transparent;
            RootBorder.BorderThickness = new Thickness(0);
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
            exStyle |= WS_EX_TOOLWINDOW;
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(0x4D, 0x0E, 0x0E, 0x11)); // 30% --panel
            RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4D, 0xD8, 0xFF)); // --border-active
            RootBorder.BorderThickness = new Thickness(1);
        }

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_settings.LockOsd)
        {
            DragMove();
        }
    }

    private void OsdWindow_LocationChanged(object? sender, EventArgs e)
    {
        _settings.OsdX = Left;
        _settings.OsdY = Top;
        SettingsService.Save(_settings);
    }

    /// <summary>
    /// Rebuilds the strip from whichever metrics the user selected.
    ///
    /// Previously this was a fixed CPU/GPU/RAM line. It now renders one
    /// run per metric so each can be coloured by its own severity — a
    /// single TextBlock could only ever be one colour, which meant a
    /// critical GPU temperature looked exactly like an idle one.
    /// </summary>
    public void UpdateStats(CpuMetrics cpu, GpuMetrics gpu, MemoryMetrics mem)
    {
        var readings = ReadoutFormatter.ReadAll(_settings.OsdMetrics, cpu, mem, gpu, _settings);

        OsdText.Inlines.Clear();

        if (readings.Count == 0)
        {
            OsdText.Inlines.Add(new Run("SEER") { Foreground = SeverityBrush(AlertSeverity.Nominal) });
            return;
        }

        for (var i = 0; i < readings.Count; i++)
        {
            if (i > 0)
                OsdText.Inlines.Add(new Run("  |  ") { Foreground = DimBrush });

            OsdText.Inlines.Add(new Run(readings[i].Full)
            {
                Foreground = SeverityBrush(readings[i].Severity)
            });
        }
    }

    // Frozen brushes: rebuilt every poll otherwise, for no benefit.
    private static readonly SolidColorBrush NominalBrush = Frozen(0xC9, 0xC9, 0xCE);  // --text
    private static readonly SolidColorBrush WarningBrush = Frozen(0xFF, 0xB0, 0x20);  // --warning
    private static readonly SolidColorBrush DangerBrush  = Frozen(0xFF, 0x5C, 0x5C);  // --danger
    private static readonly SolidColorBrush DimBrush     = Frozen(0x6A, 0x6A, 0x70);  // --text-dim

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush SeverityBrush(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => DangerBrush,
        AlertSeverity.Warning => WarningBrush,
        _ => NominalBrush
    };
}
