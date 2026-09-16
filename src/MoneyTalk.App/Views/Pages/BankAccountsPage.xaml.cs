using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class BankAccountsPage : Page
{
    public BankAccountsViewModel ViewModel { get; }

    public BankAccountsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<BankAccountsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddBankAccount_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var accountNames = ViewModel.LinkableAccounts.Select(a => $"{a.Code} · {a.Name}").ToList();
        if (accountNames.Count == 0)
        {
            accountNames.Add("(no unlinked bank/credit-card accounts — add one on Chart of Accounts)");
        }

        var dialog = new SimpleFormDialog("Link Bank Account", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "account", Label = "Chart-of-accounts account", Options = accountNames },
            new TextFieldDescriptor { Key = "bankName", Label = "Display name", PlaceholderText = "e.g. Chase Business Checking" },
            new TextFieldDescriptor { Key = "accountNumber", Label = "Account number (last 4)" },
            new TextFieldDescriptor { Key = "routingNumber", Label = "Routing number" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        if (accountIndex < 0 || accountIndex >= ViewModel.LinkableAccounts.Count) return;

        await ViewModel.AddBankAccountAsync(
            ViewModel.LinkableAccounts[accountIndex], dialog.GetText("bankName"),
            dialog.GetText("accountNumber"), dialog.GetText("routingNumber"));
    }

    private async void ImportCsv_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ViewModel.SelectedBankAccount == null)
        {
            ViewModel.ErrorMessage = "Select a bank account in the list first, then import its statement.";
            return;
        }

        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        using var stream = await file.OpenStreamForReadAsync();
        using var reader = new StreamReader(stream);
        var imported = await ViewModel.ImportCsvAsync(ViewModel.SelectedBankAccount.Id, reader);

        var confirmDialog = new ContentDialog
        {
            Title = "Import complete",
            Content = $"Imported {imported} transaction(s). Head to Reconciliation to match them against the ledger.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await confirmDialog.ShowAsync();
    }
}
