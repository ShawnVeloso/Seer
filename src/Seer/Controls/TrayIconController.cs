using System;
using System.Drawing;
using System.Windows.Forms;

namespace Seer.Controls;

/// <summary>
/// Owns the system tray icon and its context menu.
///
/// Seer is a tray-resident tool — closing the window hides it rather than
/// quitting — so the tray menu is a real control surface, not a shortcut.
/// It's kept out of the window code-behind so that adding a toggle here
/// doesn't mean editing the window.
///
/// This class raises events and holds no application state — the window
/// stays the owner of settings and decides what each toggle means.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _showOsdItem;
    private readonly ToolStripMenuItem _lockOsdItem;

    /// <summary>The user asked to bring the main window back.</summary>
    public event Action? ShowRequested;

    /// <summary>The user chose Exit — a real shutdown, not a hide.</summary>
    public event Action? ExitRequested;

    /// <summary>The user asked to open the settings dialog.</summary>
    public event Action? SettingsRequested;

    /// <summary>The desktop overlay was toggled on or off.</summary>
    public event Action<bool>? ShowOsdChanged;

    /// <summary>The overlay's locked (click-through) state was toggled.</summary>
    public event Action<bool>? LockOsdChanged;

    /// <summary>The taskbar metric readouts were toggled on or off.</summary>
    public event Action<bool>? ShowTrayReadoutsChanged;

    private readonly ToolStripMenuItem _trayReadoutsItem;

    public TrayIconController(string exePath, bool showOsd, bool lockOsd, bool showTrayReadouts)
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show Seer");
        showItem.Click += (_, _) => ShowRequested?.Invoke();

        _showOsdItem = new ToolStripMenuItem("Show OSD") { CheckOnClick = true, Checked = showOsd };
        _showOsdItem.Click += (_, _) => ShowOsdChanged?.Invoke(_showOsdItem.Checked);

        _lockOsdItem = new ToolStripMenuItem("Lock OSD Position") { CheckOnClick = true, Checked = lockOsd };
        _lockOsdItem.Click += (_, _) => LockOsdChanged?.Invoke(_lockOsdItem.Checked);

        _trayReadoutsItem = new ToolStripMenuItem("Show Temps in Taskbar")
        {
            CheckOnClick = true,
            Checked = showTrayReadouts
        };
        _trayReadoutsItem.Click += (_, _) => ShowTrayReadoutsChanged?.Invoke(_trayReadoutsItem.Checked);

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_showOsdItem);
        menu.Items.Add(_lockOsdItem);
        menu.Items.Add(_trayReadoutsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(exePath),
            Visible = true,
            Text = "Seer",
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke();
    }

    private static Icon LoadIcon(string exePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(exePath))
                return Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
        }
        catch
        {
            // Fall through — a missing tray icon must not stop startup.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        // Hide before disposing, or Windows leaves a ghost icon in the
        // tray until the user hovers over it.
        _icon.Visible = false;
        _icon.Dispose();
    }
}
