using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class JournalPage : Page
{
    public JournalViewModel ViewModel { get; }

    public JournalPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<JournalViewModel>();
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

        var dialog = new SimpleFormDialog("Add Journal Line", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "account", Label = "Account", Options = accountNames },
            new NumberFieldDescriptor { Key = "debit", Label = "Debit", Minimum = 0 },
            new NumberFieldDescriptor { Key = "credit", Label = "Credit", Minimum = 0 },
            new TextFieldDescriptor { Key = "memo", Label = "Memo (optional)" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        if (accountIndex < 0 || accountIndex >= ViewModel.Accounts.Count) return;

        ViewModel.AddNewEntryLine(
            ViewModel.Accounts[accountIndex], (decimal)dialog.GetNumber("debit"), (decimal)dialog.GetNumber("credit"), dialog.GetText("memo"));
    }

    private void RemoveLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: NewJournalLineRow row })
            ViewModel.RemoveNewEntryLineCommand.Execute(row);
    }

    private async void VoidEntry_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: JournalEntryRow row }) return;

        var confirm = new ContentDialog
        {
            Title = $"Void journal entry #{row.EntryNumber}?",
            Content = "This posts an equal-and-opposite reversing entry — the original stays in the ledger for audit history, but its effect on your books is undone.",
            PrimaryButtonText = "Void Entry",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };
        var result = await confirm.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.VoidEntryAsync(row.Id, $"Manual void of JE #{row.EntryNumber}");
    }
}
