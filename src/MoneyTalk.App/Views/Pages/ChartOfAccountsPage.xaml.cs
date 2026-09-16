using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class ChartOfAccountsPage : Page
{
    public ChartOfAccountsViewModel ViewModel { get; }

    public ChartOfAccountsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ChartOfAccountsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddAccount_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new SimpleFormDialog("Add Account", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "code", Label = "Account code", PlaceholderText = "e.g. 6200" },
            new TextFieldDescriptor { Key = "name", Label = "Account name" },
            new ComboFieldDescriptor { Key = "type", Label = "Account type", Options = ChartOfAccountsViewModel.AccountTypeOptions },
            new ComboFieldDescriptor { Key = "subtype", Label = "Sub-type", Options = ChartOfAccountsViewModel.AccountSubTypeOptions }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var type = Enum.Parse<AccountType>(dialog.GetComboValue("type"));
        var subType = Enum.Parse<AccountSubType>(dialog.GetComboValue("subtype"));
        await ViewModel.AddAccountAsync(dialog.GetText("code"), dialog.GetText("name"), type, subType);
    }
}
