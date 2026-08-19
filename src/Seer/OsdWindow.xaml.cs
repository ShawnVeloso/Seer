using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
        else if (IsOnScreen(_settings.OsdX, _settings.OsdY, Width, Height))
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

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        const double margin = 50;
        var windowRect = new System.Drawing.Rectangle((int)left, (int)top, (int)width, (int)height);

        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var workArea = screen.WorkingArea;
            if (windowRect.Right > workArea.Left + margin && windowRect.Left < workArea.Right - margin &&
                windowRect.Bottom > workArea.Top + margin && windowRect.Top < workArea.Bottom - margin)
            {
                return true;
            }
        }
        return false;
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

    public void UpdateStats(CpuMetrics cpu, GpuMetrics gpu, MemoryMetrics mem)
    {
        string cpuLoad = cpu.TotalLoad.HasValue ? $"{cpu.TotalLoad.Value:F0}%" : "--%";
        string cpuTemp = cpu.Temperature.HasValue ? $"{cpu.Temperature.Value:F0}°C" : "--°C";
        
        string gpuLoad = gpu.Load.HasValue ? $"{gpu.Load.Value:F0}%" : "--%";
        string gpuTemp = gpu.Temperature.HasValue ? $"{gpu.Temperature.Value:F0}°C" : "--°C";
        
        string memUsed = mem.UsedGb.HasValue ? $"{mem.UsedGb.Value:F1}GB" : "--GB";
        string memTot = mem.TotalGb.HasValue ? $"{mem.TotalGb.Value:F1}GB" : "--GB";
        
        OsdText.Text = $"CPU: {cpuLoad} [{cpuTemp}] | GPU: {gpuLoad} [{gpuTemp}] | RAM: {memUsed}/{memTot}";
    }
}
