using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class ChartOfAccountsViewModel : ViewModelBase
{
    public ObservableCollection<Account> Accounts { get; } = new();

    [ObservableProperty] private Account? selectedAccount;

    public static IReadOnlyList<string> AccountTypeOptions { get; } = Enum.GetNames<AccountType>();
    public static IReadOnlyList<string> AccountSubTypeOptions { get; } = Enum.GetNames<AccountSubType>();

    public ChartOfAccountsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
        : base(unitOfWorkFactory, settingsService)
    {
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == ActiveCompanyId);
            Accounts.Clear();
            foreach (var account in accounts.OrderBy(a => a.Code))
                Accounts.Add(account);
        });
    }

    public async Task<bool> AddAccountAsync(string code, string name, AccountType type, AccountSubType subType)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Both an account code and a name are required.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var account = new Account
            {
                CompanyId = ActiveCompanyId,
                Code = code.Trim(),
                Name = name.Trim(),
                Type = type,
                SubType = subType,
                IsActive = true
            };
            await uow.Accounts.AddAsync(account);
            await uow.SaveChangesAsync();
            Accounts.Add(account);
            success = true;
        });
        return success;
    }

    public async Task<bool> EditAccountAsync(Guid accountId, string code, string name, AccountType type, AccountSubType subType, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Both an account code and a name are required.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var account = await uow.Accounts.GetByIdAsync(accountId)
                ?? throw new InvalidOperationException("Account no longer exists.");
            account.Code = code.Trim();
            account.Name = name.Trim();
            account.Type = type;
            account.SubType = subType;
            account.IsActive = isActive;
            uow.Accounts.Update(account);
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }
}
