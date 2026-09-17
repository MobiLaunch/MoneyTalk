using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class TradeInEditPage : Page
{
    public TradeInEditViewModel ViewModel { get; }

    public TradeInEditPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TradeInEditViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var args = e.Parameter as TradeInEditNavigationArgs ?? new TradeInEditNavigationArgs(null);
        await ViewModel.LoadAsync(args);
    }
}
