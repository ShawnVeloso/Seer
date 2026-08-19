using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Seer;

/// <summary>
/// OSD feasibility spike — tests topmost, transparent, click-through overlay.
/// P/Invoke applies WS_EX_TRANSPARENT + WS_EX_TOOLWINDOW so the mouse passes
/// through and the window stays off Alt-Tab.  All interop is self-contained;
/// nothing leaks into the production codebase.
/// </summary>
public partial class OsdTestWindow : Window
{
    public OsdTestWindow()
    {
        InitializeComponent();
        SourceInitialized += OsdTestWindow_SourceInitialized;
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

    private void OsdTestWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }
}
