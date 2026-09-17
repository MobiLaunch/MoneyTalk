using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class PosPage : Page
{
    public PosViewModel ViewModel { get; }

    public PosPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<PosViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private void CatalogItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Item item) ViewModel.AddToCart(item);
    }

    private void RemoveFromCart_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: PosCartRow row })
            ViewModel.RemoveFromCartCommand.Execute(row);
    }
}
