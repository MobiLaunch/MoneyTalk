using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class BudgetsPage : Page
{
    public BudgetsViewModel ViewModel { get; }

    public BudgetsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<BudgetsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void NewBudget_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new SimpleFormDialog("New Budget", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Budget name", PlaceholderText = $"e.g. FY{DateTime.Now.Year} Operating Budget" },
            new NumberFieldDescriptor { Key = "year", Label = "Fiscal year", InitialValue = DateTime.Now.Year, Minimum = 2000, Maximum = 2100 }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.CreateBudgetAsync(dialog.GetText("name"), (int)dialog.GetNumber("year"));
    }

    private async void AddBudgetLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedBudget == null)
        {
            ViewModel.ErrorMessage = "Create or select a budget first.";
            return;
        }

        var accountNames = ViewModel.Accounts.Select(a => $"{a.Code} · {a.Name}").ToList();

        var dialog = new SimpleFormDialog("Add Budget Line", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "account", Label = "Account", Options = accountNames },
            new ComboFieldDescriptor { Key = "month", Label = "Month", Options = BudgetsViewModel.MonthNames },
            new NumberFieldDescriptor { Key = "amount", Label = "Budgeted amount", Minimum = 0 }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        var monthIndex = dialog.GetComboIndex("month");
        if (accountIndex < 0 || accountIndex >= ViewModel.Accounts.Count || monthIndex < 0) return;

        await ViewModel.AddBudgetLineAsync(ViewModel.Accounts[accountIndex], monthIndex + 1, (decimal)dialog.GetNumber("amount"));
    }
}
