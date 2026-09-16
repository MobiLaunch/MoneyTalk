using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;
using Windows.System;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class IntegrationsSettingsPage : Page
{
    public IntegrationsSettingsViewModel ViewModel { get; }

    public IntegrationsSettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<IntegrationsSettingsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void OpenSquareAuth_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ViewModel.SquareAuthorizationUrl))
            await Launcher.LaunchUriAsync(new Uri(ViewModel.SquareAuthorizationUrl));
    }

    private async void OpenQuickBooksAuth_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ViewModel.QuickBooksAuthorizationUrl))
            await Launcher.LaunchUriAsync(new Uri(ViewModel.QuickBooksAuthorizationUrl));
    }
}
