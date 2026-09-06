using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Seer.Models;
using Seer.Services;

namespace Seer.Controls;

/// <summary>
/// Draws selected metrics as numbers in the notification area — one tray
/// icon per metric, the way MSI Afterburner does.
///
/// The appeal over the desktop overlay is that the tray is always there:
/// it doesn't cover anything, and it stays readable when a fullscreen
/// game owns the display.
///
/// Each refresh builds a fresh bitmap and turns it into an HICON. Windows
/// copies the icon when it's assigned, so the handle must be destroyed
/// afterwards — <see cref="Icon.FromHandle"/> does not own it, and
/// leaking one per metric per second would exhaust the GDI handle quota
/// within an hour.
/// </summary>
public sealed class TrayMetricIcons : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private sealed class Slot
    {
        public required ReadoutMetric Metric { get; init; }
        public required NotifyIcon Icon { get; init; }

        /// <summary>
        /// The icon currently installed, and the HICON behind it. Both
        /// are owned by this slot and must outlive the assignment —
        /// NotifyIcon keeps the reference and re-reads the handle when
        /// the shell asks it to redraw.
        /// </summary>
        public Icon? Current;
        public IntPtr CurrentHandle = IntPtr.Zero;

        /// <summary>Last text drawn, so an unchanged value costs nothing.</summary>
        public string LastText = string.Empty;
        public AlertSeverity LastSeverity = AlertSeverity.Nominal;
    }

    private readonly List<Slot> _slots = new();
    private readonly int _size;
    private bool _disposed;

    /// <summary>Raised when any of the metric icons is double-clicked.</summary>
    public event Action? ShowRequested;

    public TrayMetricIcons()
    {
        // Matches what the shell asks for, so the number isn't resampled.
        _size = Math.Max(16, SystemInformation.SmallIconSize.Width);
    }

    /// <summary>
    /// Rebuilds the set of icons to match <paramref name="metrics"/>.
    /// Called when the selection changes; cheap enough to call with an
    /// unchanged list, but not on the poll timer.
    /// </summary>
    public void SetMetrics(IEnumerable<ReadoutMetric> metrics)
    {
        Clear();

        foreach (var metric in metrics)
        {
            var notifyIcon = new NotifyIcon
            {
                Visible = true,
                Text = ReadoutFormatter.DisplayName(metric)
            };
            notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke();
            _slots.Add(new Slot { Metric = metric, Icon = notifyIcon });
        }
    }

    /// <summary>
    /// Redraws each icon from the current snapshot. Icons whose text and
    /// colour are unchanged are left alone — the common case, since these
    /// values only move every few seconds.
    /// </summary>
    public void Update(CpuMetrics cpu, MemoryMetrics mem, GpuMetrics gpu, AppSettings settings)
    {
        if (_disposed)
            return;

        foreach (var slot in _slots)
        {
            var reading = ReadoutFormatter.Read(slot.Metric, cpu, mem, gpu, settings);

            if (reading.Compact == slot.LastText && reading.Severity == slot.LastSeverity)
                continue;

            slot.LastText = reading.Compact;
            slot.LastSeverity = reading.Severity;

            slot.Icon.Text = Truncate($"{ReadoutFormatter.DisplayName(slot.Metric)}: {reading.Full}");
            ApplyIcon(slot, reading);
        }
    }

    /// <summary>
    /// Installs a freshly drawn icon and retires the one it replaces.
    ///
    /// Order matters. NotifyIcon does not copy what it is given: it keeps
    /// the Icon and reads its handle again whenever the shell needs a
    /// redraw — after an Explorer restart, a DPI change, or the taskbar
    /// being rebuilt. Destroying the handle straight after assigning it
    /// leaves that reference dangling, which faults in native code with
    /// no managed exception and so no crash log. The previous handle is
    /// therefore only released once its replacement is in place.
    /// </summary>
    private void ApplyIcon(Slot slot, ReadoutValue reading)
    {
        using var bitmap = Render(reading.Compact, ColorFor(reading.Severity));

        var handle = bitmap.GetHicon();
        // Icon.FromHandle does not take ownership, so the raw handle is
        // tracked alongside and destroyed by hand below.
        var icon = Icon.FromHandle(handle);

        var previousIcon = slot.Current;
        var previousHandle = slot.CurrentHandle;

        slot.Icon.Icon = icon;
        slot.Current = icon;
        slot.CurrentHandle = handle;

        Release(previousIcon, previousHandle);
    }

    /// <summary>Disposes an icon wrapper and frees the HICON behind it.</summary>
    private static void Release(Icon? icon, IntPtr handle)
    {
        icon?.Dispose();
        if (handle != IntPtr.Zero)
            DestroyIcon(handle);
    }

    /// <summary>
    /// Draws the value to fill the icon.
    ///
    /// Height is chosen first and width is condensed if needed, rather
    /// than scaling both down: three digits ("100" — reached whenever
    /// load peaks) shrunk uniformly ends up about eight pixels tall and
    /// unreadable, whereas condensed digits stay full height and legible.
    /// </summary>
    private Bitmap Render(string text, Color color)
    {
        var bitmap = new Bitmap(_size, _size);

        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        // Typographic formatting drops the padding GDI+ adds around a
        // string, which at 16px is a large fraction of the space.
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.NoClip | StringFormatFlags.NoWrap
        };

        // Largest size whose height still fits the icon.
        Font? font = null;
        SizeF measured = SizeF.Empty;
        for (var pt = (float)_size; pt >= MinFontPx; pt -= 0.5f)
        {
            font?.Dispose();
            font = new Font(FontFamilyName, pt, FontStyle.Bold, GraphicsUnit.Pixel);
            measured = g.MeasureString(text, font, PointF.Empty, format);

            if (measured.Height <= _size)
                break;
        }

        if (font == null)
            return bitmap;

        try
        {
            // Condense horizontally only when the digits are too wide.
            var scaleX = measured.Width > _size && measured.Width > 0
                ? _size / measured.Width
                : 1f;

            using var brush = new SolidBrush(color);
            var state = g.Save();
            g.ScaleTransform(scaleX, 1f);
            g.DrawString(text, font, brush,
                ((_size / scaleX) - measured.Width) / 2f,
                (_size - measured.Height) / 2f,
                format);
            g.Restore(state);
        }
        finally
        {
            font.Dispose();
        }

        return bitmap;
    }

    private const string FontFamilyName = "Segoe UI";
    private const float MinFontPx = 8f;

    /// <summary>Matches the design system's status colours.</summary>
    private static Color ColorFor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => Color.FromArgb(0xFF, 0x5C, 0x5C),  // --danger
        AlertSeverity.Warning => Color.FromArgb(0xFF, 0xB0, 0x20),   // --warning
        _ => Color.FromArgb(0x4D, 0xD8, 0xFF)                        // --accent
    };

    /// <summary>NotifyIcon.Text throws above 63 characters.</summary>
    private static string Truncate(string text) =>
        text.Length <= 63 ? text : text.Substring(0, 63);

    /// <summary>Removes every icon from the tray.</summary>
    public void Clear()
    {
        foreach (var slot in _slots)
        {
            // Hide before disposing, or the shell leaves a ghost icon
            // behind until the user hovers over it. Dispose the
            // NotifyIcon before the icon it points at, so nothing can
            // read a freed handle on the way out.
            slot.Icon.Visible = false;
            slot.Icon.Dispose();
            Release(slot.Current, slot.CurrentHandle);
            slot.Current = null;
            slot.CurrentHandle = IntPtr.Zero;
        }

        _slots.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
    }
}
