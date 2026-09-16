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

    public async Task<bool> AddItemAsync(string sku, string name, ItemType type, decimal salesPrice, decimal cost, Guid? incomeAccountId)
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
                IncomeAccountId = incomeAccountId
            };
            await uow.Items.AddAsync(item);
            await uow.SaveChangesAsync();
            Items.Add(item);
            success = true;
        });
        return success;
    }
}
