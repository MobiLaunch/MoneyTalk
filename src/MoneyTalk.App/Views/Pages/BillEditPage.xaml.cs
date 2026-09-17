using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class BillEditPage : Page
{
    public BillEditViewModel ViewModel { get; }

    public BillEditPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<BillEditViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var args = e.Parameter as BillEditNavigationArgs ?? new BillEditNavigationArgs(null);
        await ViewModel.LoadAsync(args);
    }

    private async void AddLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var accountNames = ViewModel.ExpenseAccounts.Select(a => $"{a.Code} · {a.Name}").ToList();
        if (accountNames.Count == 0) accountNames.Add("(no expense accounts available)");

        var dialog = new SimpleFormDialog("Add Line Item", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "account", Label = "Expense account", Options = accountNames },
            new TextFieldDescriptor { Key = "description", Label = "Description" },
            new NumberFieldDescriptor { Key = "quantity", Label = "Quantity", InitialValue = 1, Minimum = 0 },
            new NumberFieldDescriptor { Key = "price", Label = "Amount", Minimum = 0 }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        if (accountIndex < 0 || accountIndex >= ViewModel.ExpenseAccounts.Count) return;

        ViewModel.AddLine(
            ViewModel.ExpenseAccounts[accountIndex], dialog.GetText("description"),
            (decimal)dialog.GetNumber("quantity"), (decimal)dialog.GetNumber("price"));
    }

    private void RemoveLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: BillLineEditRow row })
            ViewModel.RemoveLineCommand.Execute(row);
    }

    private async void RecordPayment_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var accountNames = ViewModel.PaymentAccounts.Select(a => a.BankName).ToList();
        if (accountNames.Count == 0) accountNames.Add("(no bank accounts — add one on Bank Accounts)");

        var dialog = new SimpleFormDialog("Record Payment", new FormFieldDescriptor[]
        {
            new NumberFieldDescriptor { Key = "amount", Label = "Amount", Minimum = 0 },
            new ComboFieldDescriptor { Key = "method", Label = "Method", Options = Enum.GetNames<PaymentMethod>() },
            new ComboFieldDescriptor { Key = "account", Label = "Pay from", Options = accountNames }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        if (accountIndex < 0 || accountIndex >= ViewModel.PaymentAccounts.Count) return;

        var method = Enum.Parse<PaymentMethod>(dialog.GetComboValue("method"));
        await ViewModel.RecordPaymentAsync(
            (decimal)dialog.GetNumber("amount"), DateTime.UtcNow.Date, method, ViewModel.PaymentAccounts[accountIndex].Id);
    }

    private async void VoidBill_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var confirm = new ContentDialog
        {
            Title = "Void this bill?",
            Content = "This reverses the bill's posting on the ledger and removes it from the vendor's balance. The bill record itself stays for your history — this can't be undone from here.",
            PrimaryButtonText = "Void Bill",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };
        var result = await confirm.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.VoidBillAsync();
    }
}
