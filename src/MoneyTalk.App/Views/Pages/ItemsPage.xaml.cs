using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class ItemsPage : Page
{
    public ItemsViewModel ViewModel { get; }

    public ItemsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ItemsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddItem_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var incomeAccountNames = ViewModel.IncomeAccounts.Select(a => $"{a.Code} · {a.Name}").ToList();
        if (incomeAccountNames.Count == 0) incomeAccountNames.Add("(no income accounts available)");

        var dialog = new SimpleFormDialog("Add Item", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "sku", Label = "SKU" },
            new TextFieldDescriptor { Key = "name", Label = "Name" },
            new ComboFieldDescriptor { Key = "type", Label = "Type", Options = ItemsViewModel.ItemTypeOptions },
            new NumberFieldDescriptor { Key = "price", Label = "Sales price", Minimum = 0 },
            new NumberFieldDescriptor { Key = "cost", Label = "Cost", Minimum = 0 },
            new ComboFieldDescriptor { Key = "incomeAccount", Label = "Income account", Options = incomeAccountNames }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var type = Enum.Parse<ItemType>(dialog.GetComboValue("type"));
        var incomeAccountIndex = dialog.GetComboIndex("incomeAccount");
        Guid? incomeAccountId = incomeAccountIndex >= 0 && incomeAccountIndex < ViewModel.IncomeAccounts.Count
            ? ViewModel.IncomeAccounts[incomeAccountIndex].Id
            : null;

        await ViewModel.AddItemAsync(
            dialog.GetText("sku"), dialog.GetText("name"), type,
            (decimal)dialog.GetNumber("price"), (decimal)dialog.GetNumber("cost"), incomeAccountId);
    }

    private async void ItemsGrid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var item = ViewModel.SelectedItem;
        if (item == null) return;

        var incomeAccounts = ViewModel.IncomeAccounts.ToList();
        var incomeAccountNames = incomeAccounts.Select(a => $"{a.Code} · {a.Name}").ToList();
        var currentIndex = incomeAccounts.FindIndex(a => a.Id == item.IncomeAccountId);
        if (incomeAccountNames.Count == 0) incomeAccountNames.Add("(no income accounts available)");

        var typeOptions = ItemsViewModel.ItemTypeOptions;
        var dialog = new SimpleFormDialog("Edit Item", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "sku", Label = "SKU", InitialValue = item.Sku },
            new TextFieldDescriptor { Key = "name", Label = "Name", InitialValue = item.Name },
            new ComboFieldDescriptor { Key = "type", Label = "Type", Options = typeOptions, InitialIndex = typeOptions.ToList().IndexOf(item.Type.ToString()) },
            new NumberFieldDescriptor { Key = "price", Label = "Sales price", InitialValue = (double)item.SalesPrice, Minimum = 0 },
            new NumberFieldDescriptor { Key = "cost", Label = "Cost", InitialValue = (double)item.Cost, Minimum = 0 },
            new ComboFieldDescriptor { Key = "incomeAccount", Label = "Income account", Options = incomeAccountNames, InitialIndex = Math.Max(currentIndex, 0) },
            new CheckboxFieldDescriptor { Key = "isActive", Label = "Active", InitialValue = item.IsActive }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var type = Enum.Parse<ItemType>(dialog.GetComboValue("type"));
        var incomeAccountIndex = dialog.GetComboIndex("incomeAccount");
        Guid? incomeAccountId = incomeAccountIndex >= 0 && incomeAccountIndex < incomeAccounts.Count
            ? incomeAccounts[incomeAccountIndex].Id
            : null;

        await ViewModel.EditItemAsync(
            item.Id, dialog.GetText("sku"), dialog.GetText("name"), type,
            (decimal)dialog.GetNumber("price"), (decimal)dialog.GetNumber("cost"), incomeAccountId, dialog.GetBool("isActive"));
    }
}
