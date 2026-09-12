using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace Seer.Controls;

/// <summary>
/// The one place an animation starts.
///
/// Every living detail renders its resting state from the poll regardless;
/// this adds only the transient blink. Routing them all through here means
/// the Windows "Show animations" setting is honoured in a single place,
/// rather than checked at a dozen call sites and eventually forgotten at one.
///
/// Two rules keep the render thread idle between polls: nothing repeats
/// forever, and every animation releases its clock when it finishes. A clock
/// left attached keeps the element in the animated-properties set for the
/// life of the app, which on a window that updates every second for hours is
/// exactly the sort of cost that never shows up in a screenshot.
/// </summary>
public static class Pulse
{
    /// <summary>
    /// Fades an element from full to <paramref name="restingOpacity"/> once.
    /// With motion disabled it simply sits at the resting value: the detail
    /// still updates, it just does not move.
    /// </summary>
    public static void Blink(UIElement element, double restingOpacity = 0.35, int milliseconds = 600)
    {
        if (element == null)
            return;

        if (!HudConfig.MotionAllowed)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = restingOpacity;
            return;
        }

        var animation = new DoubleAnimation(1.0, restingOpacity, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) =>
        {
            // Release the clock, then pin the value it landed on.
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = restingOpacity;
        };

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
