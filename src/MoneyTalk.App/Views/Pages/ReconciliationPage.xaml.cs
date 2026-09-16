using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class ReconciliationPage : Page
{
    public ReconciliationViewModel ViewModel { get; }

    public ReconciliationPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ReconciliationViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }
}
