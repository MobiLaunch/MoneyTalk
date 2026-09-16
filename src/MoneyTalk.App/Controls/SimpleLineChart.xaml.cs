using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace MoneyTalk.App.Controls;

/// <summary>A minimal, dependency-free line chart: draws <see cref="Values"/> as a polyline
/// scaled to fill the control, with a dashed zero-line when the series crosses zero. Built
/// in-house (rather than pulling in a charting package) so the cash-trend/forecast sparklines on
/// the Dashboard and Reports pages have no external rendering dependency to go stale.</summary>
public sealed partial class SimpleLineChart : UserControl
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(SimpleLineChart),
        new PropertyMetadata(null, OnValuesChanged));

    public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(
        nameof(LineColor), typeof(Color), typeof(SimpleLineChart),
        new PropertyMetadata(Colors.SeaGreen, OnValuesChanged));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public SimpleLineChart()
    {
        InitializeComponent();
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SimpleLineChart)d).Redraw();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        DrawingCanvas.Children.Clear();

        var values = Values;
        var width = ActualWidth;
        var height = ActualHeight;
        if (values == null || values.Count < 2 || width <= 0 || height <= 0)
            return;

        var min = values.Min();
        var max = values.Max();
        if (min == max) { min -= 1; max += 1; }
        var range = max - min;

        double XFor(int index) => values.Count <= 1 ? 0 : index / (double)(values.Count - 1) * width;
        double YFor(double value) => height - (value - min) / range * height;

        if (min < 0 && max > 0)
        {
            var zeroLine = new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = YFor(0),
                Y2 = YFor(0),
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };
            DrawingCanvas.Children.Add(zeroLine);
        }

        var points = new PointCollection();
        for (int i = 0; i < values.Count; i++)
            points.Add(new Point(XFor(i), YFor(values[i])));

        var polyline = new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(LineColor),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        DrawingCanvas.Children.Add(polyline);

        var lastPoint = points[^1];
        var dot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = new SolidColorBrush(LineColor)
        };
        Canvas.SetLeft(dot, lastPoint.X - 4);
        Canvas.SetTop(dot, lastPoint.Y - 4);
        DrawingCanvas.Children.Add(dot);
    }
}
