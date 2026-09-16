using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class CompanySettingsPage : Page
{
    public CompanySettingsViewModel ViewModel { get; }

    public CompanySettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CompanySettingsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }
}
