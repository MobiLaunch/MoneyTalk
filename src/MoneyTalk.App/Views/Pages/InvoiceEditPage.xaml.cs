using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class InvoiceEditPage : Page
{
    public InvoiceEditViewModel ViewModel { get; }

    public InvoiceEditPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<InvoiceEditViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var args = e.Parameter as InvoiceEditNavigationArgs ?? new InvoiceEditNavigationArgs(null);
        await ViewModel.LoadAsync(args);
    }

    private async void AddLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var itemNames = ViewModel.Items.Select(i => i.Name).ToList();
        if (itemNames.Count == 0) itemNames.Add("(no items — add one on Products & Services)");

        var dialog = new SimpleFormDialog("Add Line Item", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "item", Label = "Item", Options = itemNames },
            new TextFieldDescriptor { Key = "description", Label = "Description (optional override)" },
            new NumberFieldDescriptor { Key = "quantity", Label = "Quantity", InitialValue = 1, Minimum = 0 },
            new NumberFieldDescriptor { Key = "price", Label = "Unit price", Minimum = 0 }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var itemIndex = dialog.GetComboIndex("item");
        var item = itemIndex >= 0 && itemIndex < ViewModel.Items.Count ? ViewModel.Items[itemIndex] : null;
        var unitPrice = (decimal)dialog.GetNumber("price");
        if (unitPrice == 0 && item != null) unitPrice = item.SalesPrice;

        ViewModel.AddLine(item, dialog.GetText("description"), (decimal)dialog.GetNumber("quantity"), unitPrice);
    }

    private void RemoveLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: InvoiceLineEditRow row })
            ViewModel.RemoveLineCommand.Execute(row);
    }

    private async void RecordPayment_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var accountNames = ViewModel.DepositAccounts.Select(a => a.BankName).ToList();
        if (accountNames.Count == 0) accountNames.Add("(no bank accounts — add one on Bank Accounts)");

        var dialog = new SimpleFormDialog("Record Payment", new FormFieldDescriptor[]
        {
            new NumberFieldDescriptor { Key = "amount", Label = "Amount", Minimum = 0 },
            new ComboFieldDescriptor { Key = "method", Label = "Method", Options = Enum.GetNames<PaymentMethod>() },
            new ComboFieldDescriptor { Key = "account", Label = "Deposit to", Options = accountNames }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        if (accountIndex < 0 || accountIndex >= ViewModel.DepositAccounts.Count) return;

        var method = Enum.Parse<PaymentMethod>(dialog.GetComboValue("method"));
        await ViewModel.RecordPaymentAsync(
            (decimal)dialog.GetNumber("amount"), DateTime.UtcNow.Date, method, ViewModel.DepositAccounts[accountIndex].Id);
    }
}
