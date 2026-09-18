using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class ReceiptEditorPage : Page
{
    public ReceiptEditorViewModel ViewModel { get; }

    public ReceiptEditorPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ReceiptEditorViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Load();
    }
}
