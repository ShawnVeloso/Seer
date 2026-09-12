using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Seer.Models;

namespace Seer.Controls;

/// <summary>
/// The container every panel sits in: a chamfered outline, a title set into
/// the top border line, and corner brackets that appear only when they mean
/// something — hover, or a panel whose readings have left nominal.
///
/// State colours live in the template as StaticResources rather than being
/// resolved here by string. An earlier version called FindResource in
/// OnApplyTemplate and cast the result unconditionally, which turned a
/// renamed theme key into a runtime crash on first hover instead of a load
/// failure; the brush properties below are set by template triggers instead.
/// </summary>
[TemplatePart(Name = EdgePart, Type = typeof(ChamferShape))]
[TemplatePart(Name = HaloPart, Type = typeof(ChamferShape))]
[TemplatePart(Name = BracketPart, Type = typeof(Grid))]
[TemplatePart(Name = HeaderLinePart, Type = typeof(Grid))]
[TemplatePart(Name = IndexTabPart, Type = typeof(Border))]
public class HudPanel : ContentControl
{
    public const string EdgePart = "PART_Edge";
    public const string HaloPart = "PART_Halo";
    public const string BracketPart = "BracketGrid";
    public const string HeaderLinePart = "PART_HeaderLine";
    public const string IndexTabPart = "PART_IndexTab";

    private Border? _indexTab;

    static HudPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(HudPanel), new FrameworkPropertyMetadata(typeof(HudPanel)));
    }

    // ── Header line ───────────────────────────────────────────────────

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(HudPanel),
            new PropertyMetadata(string.Empty));

    /// <summary>Panel name, drawn on the top border line.</summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty IndexTagProperty =
        DependencyProperty.Register(nameof(IndexTag), typeof(string), typeof(HudPanel),
            new PropertyMetadata(string.Empty, OnIndexTagChanged));

    /// <summary>
    /// The panel's number, including its brackets — "[1]", "[i]". Rendered as
    /// a tinted tab. The design system's numbered-header convention; panels
    /// without a number (the trend charts) simply leave it empty.
    /// </summary>
    public string IndexTag
    {
        get => (string)GetValue(IndexTagProperty);
        set => SetValue(IndexTagProperty, value);
    }

    public static readonly DependencyProperty MetaProperty =
        DependencyProperty.Register(nameof(Meta), typeof(string), typeof(HudPanel),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// A real fact about the panel, right-aligned on the border line: thread
    /// count, VRAM size, ping target. Pad it to a fixed width when it updates
    /// per poll, or the header re-measures every second.
    /// </summary>
    public string Meta
    {
        get => (string)GetValue(MetaProperty);
        set => SetValue(MetaProperty, value);
    }

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(HudPanel),
            new PropertyMetadata(null));

    /// <summary>A panel-specific control on the header line — the ping start/stop button, the elevate link.</summary>
    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    // ── Chrome ────────────────────────────────────────────────────────

    public static readonly DependencyProperty ChamferProperty =
        DependencyProperty.Register(nameof(Chamfer), typeof(double), typeof(HudPanel),
            new PropertyMetadata(10.0));

    /// <summary>Length of the 45° cut on the top-right corner.</summary>
    public double Chamfer
    {
        get => (double)GetValue(ChamferProperty);
        set => SetValue(ChamferProperty, value);
    }

    public static readonly DependencyProperty EdgeBrushProperty =
        DependencyProperty.Register(nameof(EdgeBrush), typeof(Brush), typeof(HudPanel),
            new PropertyMetadata(null));

    /// <summary>The outline colour. Set by template triggers, not by hand.</summary>
    public Brush? EdgeBrush
    {
        get => (Brush?)GetValue(EdgeBrushProperty);
        set => SetValue(EdgeBrushProperty, value);
    }

    public static readonly DependencyProperty AccentBrushProperty =
        DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(HudPanel),
            new PropertyMetadata(null));

    /// <summary>Bracket colour. Set by template triggers.</summary>
    public Brush? AccentBrush
    {
        get => (Brush?)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public static readonly DependencyProperty HaloBrushProperty =
        DependencyProperty.Register(nameof(HaloBrush), typeof(Brush), typeof(HudPanel),
            new PropertyMetadata(null));

    /// <summary>The soft outer light. Transparent at rest; set by template triggers.</summary>
    public Brush? HaloBrush
    {
        get => (Brush?)GetValue(HaloBrushProperty);
        set => SetValue(HaloBrushProperty, value);
    }

    // ── State ─────────────────────────────────────────────────────────

    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(AlertSeverity), typeof(HudPanel),
            new PropertyMetadata(AlertSeverity.Nominal, null, CoerceSeverity));

    /// <summary>
    /// The worst severity among this panel's own readings. Drives the edge and
    /// bracket colour, so the panel behind a WARNING badge identifies itself.
    /// </summary>
    public AlertSeverity Severity
    {
        get => (AlertSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    // Honours the feature flag at the source: with severity edges switched
    // off, the value never leaves Nominal and no trigger ever fires.
    private static object CoerceSeverity(DependencyObject d, object baseValue)
        => HudConfig.EnableSeverityEdges ? baseValue : AlertSeverity.Nominal;

    public static readonly DependencyProperty IsExpandableProperty =
        DependencyProperty.Register(nameof(IsExpandable), typeof(bool), typeof(HudPanel),
            new PropertyMetadata(false));

    /// <summary>Whether clicking the header line collapses the panel body.</summary>
    public bool IsExpandable
    {
        get => (bool)GetValue(IsExpandableProperty);
        set => SetValue(IsExpandableProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(HudPanel),
            new PropertyMetadata(true));

    /// <summary>
    /// Collapsed panels keep their header line and drop their body, so the row
    /// shrinks to a titled rule rather than disappearing.
    /// </summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    // ── Template ──────────────────────────────────────────────────────

    private static void OnIndexTagChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HudPanel)d).UpdateIndexTab();

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        var edge = GetTemplateChild(EdgePart) as ChamferShape;
        var halo = GetTemplateChild(HaloPart) as ChamferShape;
        var brackets = GetTemplateChild(BracketPart) as Grid;
        var headerLine = GetTemplateChild(HeaderLinePart) as Grid;
        _indexTab = GetTemplateChild(IndexTabPart) as Border;

        // GetTemplateChild returns null for a renamed part and the feature
        // silently vanishes, so fail loudly in Debug instead.
        Debug.Assert(edge != null, EdgePart + " is missing from the HudPanel template.");
        Debug.Assert(halo != null, HaloPart + " is missing from the HudPanel template.");
        Debug.Assert(brackets != null, BracketPart + " is missing from the HudPanel template.");
        Debug.Assert(headerLine != null, HeaderLinePart + " is missing from the HudPanel template.");
        Debug.Assert(_indexTab != null, IndexTabPart + " is missing from the HudPanel template.");

        // Feature flags set Visibility, never Opacity: the triggers own
        // Opacity, and a local value would win permanently over them.
        if (brackets != null && !HudConfig.EnablePanelBrackets)
            brackets.Visibility = Visibility.Collapsed;

        if (halo != null && !HudConfig.EnableHoverGlow)
            halo.Visibility = Visibility.Collapsed;

        if (!HudConfig.EnableChamferedCorner)
        {
            if (edge != null) edge.Chamfer = 0;
            if (halo != null) halo.Chamfer = 0;
        }

        if (headerLine != null)
        {
            headerLine.Cursor = IsExpandable ? Cursors.Hand : null;
            headerLine.MouseLeftButtonUp += HeaderLine_MouseLeftButtonUp;
        }

        UpdateIndexTab();
    }

    private void HeaderLine_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsExpandable)
            IsExpanded = !IsExpanded;
    }

    /// <summary>
    /// Hides the tab for panels that have no number — an empty tinted box
    /// reads as a rendering fault — and for the whole app when the flag is off.
    /// </summary>
    private void UpdateIndexTab()
    {
        if (_indexTab == null)
            return;

        _indexTab.Visibility = HudConfig.EnableIndexTabs && !string.IsNullOrEmpty(IndexTag)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
