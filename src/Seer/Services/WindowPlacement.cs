using System.Drawing;
using System.Windows.Forms;

namespace Seer.Services;

/// <summary>
/// Validates persisted window coordinates against the monitors that are
/// actually connected right now, so unplugging a display can't leave
/// Seer restoring itself somewhere invisible.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// How much of the window must overlap a monitor's work area for the
    /// saved position to be considered usable.
    /// </summary>
    private const double VisibleMargin = 50;

    /// <summary>
    /// Returns true if the given rectangle overlaps at least one connected
    /// monitor's work area by <see cref="VisibleMargin"/> pixels.
    /// </summary>
    public static bool IsOnScreen(double left, double top, double width, double height)
    {
        var windowRect = new Rectangle((int)left, (int)top, (int)width, (int)height);

        foreach (var screen in Screen.AllScreens)
        {
            var workArea = screen.WorkingArea;
            if (windowRect.Right > workArea.Left + VisibleMargin
                && windowRect.Left < workArea.Right - VisibleMargin
                && windowRect.Bottom > workArea.Top + VisibleMargin
                && windowRect.Top < workArea.Bottom - VisibleMargin)
            {
                return true;
            }
        }

        return false;
    }
}
