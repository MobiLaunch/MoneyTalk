using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MoneyTalk.App.Controls;

/// <summary>A single dashboard stat tile: a label, a big value, and an optional muted subtext
/// line (e.g. a trend or a count). Used for every "Total Cash / Total AR / Net Income" style
/// number on the Dashboard page.</summary>
public sealed partial class KpiCard : UserControl
{
    public static readonly DependencyProperty LabelTextProperty = DependencyProperty.Register(
        nameof(LabelText), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(
        nameof(ValueText), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty, OnValueChanged));

    public static readonly DependencyProperty SubTextProperty = DependencyProperty.Register(
        nameof(SubText), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty, OnSubTextChanged));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(KpiCard), new PropertyMetadata(null));

    public KpiCard()
    {
        InitializeComponent();
    }

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public string SubText
    {
        get => (string)GetValue(SubTextProperty);
        set => SetValue(SubTextProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((KpiCard)d).LabelBlock.Text = (string)e.NewValue;

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((KpiCard)d).ValueBlock.Text = (string)e.NewValue;

    private static void OnSubTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (KpiCard)d;
        var text = (string)e.NewValue;
        card.SubTextBlock.Text = text;
        card.SubTextBlock.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
