using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Seer.Controls;

public partial class TrendChart : UserControl
{
    private IEnumerable<float> _data = Enumerable.Empty<float>();

    public string Title
    {
        get => ChartTitle.Text;
        set => ChartTitle.Text = value;
    }

    public Brush Stroke
    {
        get => ChartLine.Stroke;
        set
        {
            ChartLine.Stroke = value;
            if (HudConfig.EnableChartGlow && value is SolidColorBrush solidBrush)
            {
                ChartLine.Effect = new DropShadowEffect
                {
                    Color = solidBrush.Color,
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
            }
        }
    }

    public TrendChart()
    {
        InitializeComponent();
    }

    public void UpdateData(IEnumerable<float> data)
    {
        _data = data;
        Redraw();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    private void Redraw()
    {
        var points = new PointCollection();
        var dataList = _data.ToList();

        if (dataList.Count == 0 || ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0)
        {
            ChartLine.Points = points;
            return;
        }

        // We plot right-to-left. Newest data is at the end of the list.
        // We have 120 max points (0 to 119). We space them out.
        int maxPoints = 120;
        double width = ChartCanvas.ActualWidth;
        double height = ChartCanvas.ActualHeight;

        double stepX = width / (maxPoints - 1);

        // Start drawing from right side for the newest point
        // Index 0 of the dataList is the oldest point
        // Index Count - 1 is the newest point
        
        int startIndex = maxPoints - dataList.Count;

        for (int i = 0; i < dataList.Count; i++)
        {
            // Position on X axis: right aligned
            double x = (startIndex + i) * stepX;
            
            // Value is 0-100 percentage. Y goes down, so 100% is Y=0, 0% is Y=height.
            float value = dataList[i];
            
            // Clamp value between 0 and 100 just in case
            if (value < 0) value = 0;
            if (value > 100) value = 100;

            double y = height - (value / 100.0 * height);

            points.Add(new Point(x, y));
        }

        ChartLine.Points = points;
    }
}
