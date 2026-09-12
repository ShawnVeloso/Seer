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

    /// <summary>
    /// Stand-in extent for the on-screen check. The real one depends on which
    /// metrics are selected and isn't known until the first layout pass; this
    /// only has to be close enough to decide whether a saved corner still
    /// lands on a monitor that is plugged in.
    /// </summary>
    private const double NominalWidth = 500;
    private const double NominalHeight = 36;


    // Cached from the theme once, not re-resolved per poll: UpdateStats runs
    // every second and builds a Run per selected metric.
    private readonly SolidColorBrush _nominalBrush;
    private readonly SolidColorBrush _warningBrush;
    private readonly SolidColorBrush _dangerBrush;
    private readonly SolidColorBrush _dimBrush;
    private readonly SolidColorBrush _edgeBrush;
    private readonly SolidColorBrush _groundBrush;

    public OsdWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        _nominalBrush = ThemeBrush("SeerText");
        _warningBrush = ThemeBrush("SeerWarning");
        _dangerBrush = ThemeBrush("SeerDanger");
        _dimBrush = ThemeBrush("SeerTextDim");
        _edgeBrush = ThemeBrush("SeerBorderActive");

        // The panel ground at 30%, so the desktop still reads through an
        // unlocked overlay. Derived from the token rather than written out
        // again, which is how the old literal drifted from the theme.
        var panel = ((SolidColorBrush)FindResource("SeerPanel")).Color;
        _groundBrush = Freeze(Color.FromArgb(0x4D, panel.R, panel.G, panel.B));

        ApplyChrome();
        SourceInitialized += OsdWindow_SourceInitialized;

        if (!double.IsNaN(_settings.OsdX) && !double.IsNaN(_settings.OsdY)
            && WindowPlacement.IsOnScreen(_settings.OsdX, _settings.OsdY, NominalWidth, NominalHeight))
        {
            Left = _settings.OsdX;
            Top = _settings.OsdY;
        }
        else
        {
            // The strip sizes itself to the metrics the user picked, so its
            // width isn't known until it has been laid out. Park it off-screen
            // and place it properly from Loaded, rather than guessing a width
            // here and leaving the overlay hanging off the right edge.
            Left = -NominalWidth;
            Top = -NominalHeight;
            Loaded += PlaceTopRight;
        }

        LocationChanged += OsdWindow_LocationChanged;
    }

    /// <summary>
    /// The default corner: top-right of the work area, inset 20px. Runs once,
    /// after the first layout, when the strip's real width is known.
    /// </summary>
    private void PlaceTopRight(object sender, RoutedEventArgs e)
    {
        Loaded -= PlaceTopRight;

        var work = SystemParameters.WorkArea;
        Left = work.Right - ActualWidth - 20;
        Top = work.Top + 20;
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

    /// <summary>
    /// Locked: click-through, and no chrome at all — just the readings over
    /// the desktop. Unlocked: the panel chrome, so there is something to grab.
    ///
    /// The chrome is applied whether or not the window has a handle yet. Only
    /// the extended style needs one, and folding both into a single
    /// handle-guarded block meant an early call left the overlay with no
    /// outline and no ground.
    /// </summary>
    public void ApplyLockState()
    {
        ApplyChrome();

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

        if (_settings.LockOsd)
        {
            exStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
        }
        else
        {
            exStyle &= ~WS_EX_TRANSPARENT;
            exStyle |= WS_EX_TOOLWINDOW;
        }

        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private void ApplyChrome()
    {
        Chrome.Fill = _settings.LockOsd ? null : _groundBrush;
        Chrome.Stroke = _settings.LockOsd ? null : _edgeBrush;
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
                OsdText.Inlines.Add(new Run("  |  ") { Foreground = _dimBrush });

            OsdText.Inlines.Add(new Run(readings[i].Full)
            {
                Foreground = SeverityBrush(readings[i].Severity)
            });
        }
    }

    /// <summary>
    /// A theme brush, frozen. The dictionary's own instances are shared with
    /// the main window, so a clone is taken before freezing rather than
    /// freezing a brush the rest of the app is still using.
    /// </summary>
    private SolidColorBrush ThemeBrush(string key)
        => Freeze(((SolidColorBrush)FindResource(key)).Color);

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private SolidColorBrush SeverityBrush(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => _dangerBrush,
        AlertSeverity.Warning => _warningBrush,
        _ => _nominalBrush
    };
}
