using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class ItemsViewModel : ViewModelBase
{
    public ObservableCollection<Item> Items { get; } = new();
    public ObservableCollection<Account> IncomeAccounts { get; } = new();

    [ObservableProperty] private Item? selectedItem;

    public static IReadOnlyList<string> ItemTypeOptions { get; } = Enum.GetNames<ItemType>();

    public ItemsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
        : base(unitOfWorkFactory, settingsService)
    {
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var items = await uow.Items.FindAsync(i => i.CompanyId == ActiveCompanyId);
            Items.Clear();
            foreach (var item in items.OrderBy(i => i.Name))
                Items.Add(item);

            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == ActiveCompanyId && a.Type == AccountType.Income && a.IsActive);
            IncomeAccounts.Clear();
            foreach (var account in accounts.OrderBy(a => a.Code))
                IncomeAccounts.Add(account);
        });
    }

    public async Task<bool> AddItemAsync(
        string sku, string name, ItemType type, decimal salesPrice, decimal cost, Guid? incomeAccountId,
        decimal quantityOnHand = 0, decimal reorderPoint = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Item name is required.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var item = new Item
            {
                CompanyId = ActiveCompanyId,
                Sku = sku.Trim(),
                Name = name.Trim(),
                Type = type,
                SalesPrice = salesPrice,
                Cost = cost,
                IncomeAccountId = incomeAccountId,
                QuantityOnHand = type == ItemType.Inventory ? quantityOnHand : 0,
                ReorderPoint = type == ItemType.Inventory ? reorderPoint : 0
            };
            await uow.Items.AddAsync(item);
            await uow.SaveChangesAsync();
            Items.Add(item);
            success = true;
        });
        return success;
    }

    public async Task<bool> EditItemAsync(
        Guid itemId, string sku, string name, ItemType type, decimal salesPrice, decimal cost, Guid? incomeAccountId, bool isActive,
        decimal quantityOnHand = 0, decimal reorderPoint = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Item name is required.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var item = await uow.Items.GetByIdAsync(itemId)
                ?? throw new InvalidOperationException("Item no longer exists.");
            item.Sku = sku.Trim();
            item.Name = name.Trim();
            item.Type = type;
            item.SalesPrice = salesPrice;
            item.Cost = cost;
            item.IncomeAccountId = incomeAccountId;
            item.IsActive = isActive;
            if (type == ItemType.Inventory)
            {
                item.QuantityOnHand = quantityOnHand;
                item.ReorderPoint = reorderPoint;
            }
            uow.Items.Update(item);
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    /// <summary>Encodes the item's SKU as a CODE128 barcode and sends a small label (name, SKU,
    /// price, barcode) straight to the configured printer — see <see cref="PrintService"/> for
    /// why this goes through classic GDI+ printing rather than the WinRT printing stack.</summary>
    public void PrintLabel(Item item)
    {
        if (string.IsNullOrWhiteSpace(item.Sku))
        {
            ErrorMessage = "This item has no SKU to encode as a barcode.";
            return;
        }

        try
        {
            var barcode = BarcodeService.Encode(item.Sku, LabelBarcodeFormat.Code128, 260, 80);
            var printerName = SettingsService.Load().ReceiptPrinterName;
            PrintService.PrintLabel(printerName, item.Name, $"{item.Sku} · {item.SalesPrice:C2}", barcode);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't print label: {ex.Message}";
        }
    }
}
