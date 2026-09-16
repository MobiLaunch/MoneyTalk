using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class RecurringTransactionsPage : Page
{
    public RecurringTransactionsViewModel ViewModel { get; }

    public RecurringTransactionsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<RecurringTransactionsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var accountNames = ViewModel.Accounts.Select(a => $"{a.Code} · {a.Name}").ToList();
        if (accountNames.Count == 0) return;

        var dialog = new SimpleFormDialog("Add Line", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "account", Label = "Account", Options = accountNames },
            new NumberFieldDescriptor { Key = "debit", Label = "Debit", Minimum = 0 },
            new NumberFieldDescriptor { Key = "credit", Label = "Credit", Minimum = 0 }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        if (accountIndex < 0 || accountIndex >= ViewModel.Accounts.Count) return;

        ViewModel.AddTemplateLine(ViewModel.Accounts[accountIndex], (decimal)dialog.GetNumber("debit"), (decimal)dialog.GetNumber("credit"));
    }

    private void ToggleActive_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: RecurringTransaction item })
            ViewModel.ToggleActiveCommand.Execute(item);
    }
}
