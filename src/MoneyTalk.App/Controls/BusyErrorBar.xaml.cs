using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MoneyTalk.App.Controls;

/// <summary>Every page's standard "loading + error" strip: a ProgressRing that appears while a
/// ViewModel's <c>IsBusy</c> is true, and an InfoBar that appears when its <c>ErrorMessage</c>
/// is set. Centralized here instead of hand-copied per page so every page behaves identically
/// and none of them can silently drop the error/loading UI as new pages get added.</summary>
public sealed partial class BusyErrorBar : UserControl
{
    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy), typeof(bool), typeof(BusyErrorBar), new PropertyMetadata(false, OnIsBusyChanged));

    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(
        nameof(ErrorMessage), typeof(string), typeof(BusyErrorBar), new PropertyMetadata(null, OnErrorMessageChanged));

    public BusyErrorBar()
    {
        InitializeComponent();
    }

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public string? ErrorMessage
    {
        get => (string?)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    private static void OnIsBusyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BusyErrorBar)d;
        var isBusy = (bool)e.NewValue;
        control.BusyRing.IsActive = isBusy;
        control.BusyRing.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnErrorMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (BusyErrorBar)d;
        var message = (string?)e.NewValue;
        control.ErrorInfoBar.Message = message ?? string.Empty;
        control.ErrorInfoBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }
}
