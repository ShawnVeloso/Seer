using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Seer.Controls;

/// <summary>
/// A decorative wrapper for UI panels that provides corner brackets and a hover glow.
/// These aesthetic effects can be toggled globally via <see cref="HudConfig"/>.
/// </summary>
public class HudPanel : ContentControl
{
    private Grid? _bracketGrid;
    private DropShadowEffect? _hoverGlow;
    private Border? _mainBorder;
    private Storyboard? _mouseEnterStoryboard;
    private Storyboard? _mouseLeaveStoryboard;

    static HudPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HudPanel), new FrameworkPropertyMetadata(typeof(HudPanel)));
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _bracketGrid = GetTemplateChild("BracketGrid") as Grid;
        _hoverGlow = GetTemplateChild("HoverGlow") as DropShadowEffect;
        _mainBorder = GetTemplateChild("MainBorder") as Border;

        if (_bracketGrid != null && !HudConfig.EnablePanelBrackets)
        {
            _bracketGrid.Visibility = Visibility.Collapsed;
        }

        if (HudConfig.EnableHoverGlow && _hoverGlow != null && _mainBorder != null)
        {
            // Build the storyboards programmatically since we need to resolve
            // dynamic resources for the active color and standard color, which
            // is tricky to do robustly inside pure XAML triggers across themes.

            var activeColor = ((System.Windows.Media.SolidColorBrush)FindResource("SeerBorderActive")).Color;
            var normalColor = ((System.Windows.Media.SolidColorBrush)FindResource("SeerBorder")).Color;

            _mouseEnterStoryboard = new Storyboard();
            var glowIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
            Storyboard.SetTarget(glowIn, _hoverGlow);
            Storyboard.SetTargetProperty(glowIn, new PropertyPath(DropShadowEffect.OpacityProperty));
            _mouseEnterStoryboard.Children.Add(glowIn);

            var borderActive = new ColorAnimation(activeColor, TimeSpan.FromMilliseconds(120));
            Storyboard.SetTarget(borderActive, _mainBorder);
            Storyboard.SetTargetProperty(borderActive, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));
            _mouseEnterStoryboard.Children.Add(borderActive);

            _mouseLeaveStoryboard = new Storyboard();
            var glowOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120));
            Storyboard.SetTarget(glowOut, _hoverGlow);
            Storyboard.SetTargetProperty(glowOut, new PropertyPath(DropShadowEffect.OpacityProperty));
            _mouseLeaveStoryboard.Children.Add(glowOut);

            var borderNormal = new ColorAnimation(normalColor, TimeSpan.FromMilliseconds(120));
            Storyboard.SetTarget(borderNormal, _mainBorder);
            Storyboard.SetTargetProperty(borderNormal, new PropertyPath("(Border.BorderBrush).(SolidColorBrush.Color)"));
            _mouseLeaveStoryboard.Children.Add(borderNormal);
        }
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (HudConfig.EnableHoverGlow)
        {
            _mouseEnterStoryboard?.Begin();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (HudConfig.EnableHoverGlow)
        {
            _mouseLeaveStoryboard?.Begin();
        }
    }
}
