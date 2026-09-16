using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class BankAccountRow
{
    public Guid Id { get; init; }
    public string BankName { get; init; } = string.Empty;
    public string LinkedAccountName { get; init; } = string.Empty;
    public decimal CurrentBalance { get; init; }
    public DateTime? LastReconciledDate { get; init; }
}

public partial class BankAccountsViewModel : ViewModelBase
{
    private readonly ReconciliationService _reconciliationService;

    public ObservableCollection<BankAccountRow> BankAccounts { get; } = new();
    public ObservableCollection<Account> LinkableAccounts { get; } = new();

    [ObservableProperty] private BankAccountRow? selectedBankAccount;

    public BankAccountsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, ReconciliationService reconciliationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _reconciliationService = reconciliationService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == ActiveCompanyId && b.IsActive);
            var linkedAccountIds = bankAccounts.Select(b => b.AccountId).ToHashSet();
            var accounts = (await uow.Accounts.FindAsync(a => a.CompanyId == ActiveCompanyId)).ToDictionary(a => a.Id);

            BankAccounts.Clear();
            foreach (var bankAccount in bankAccounts)
            {
                BankAccounts.Add(new BankAccountRow
                {
                    Id = bankAccount.Id,
                    BankName = bankAccount.BankName,
                    LinkedAccountName = accounts.TryGetValue(bankAccount.AccountId, out var a) ? $"{a.Code} · {a.Name}" : "Unlinked",
                    CurrentBalance = accounts.TryGetValue(bankAccount.AccountId, out var acct) ? acct.CurrentBalance : 0,
                    LastReconciledDate = bankAccount.LastReconciledDate
                });
            }

            LinkableAccounts.Clear();
            foreach (var account in accounts.Values.Where(a =>
                (a.SubType == AccountSubType.Bank || a.SubType == AccountSubType.CreditCard) && !linkedAccountIds.Contains(a.Id)))
            {
                LinkableAccounts.Add(account);
            }
        });
    }

    public async Task<bool> AddBankAccountAsync(Account linkedAccount, string bankName, string accountNumberMasked, string routingNumber)
    {
        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var bankAccount = new BankAccount
            {
                CompanyId = ActiveCompanyId,
                AccountId = linkedAccount.Id,
                BankName = bankName.Trim(),
                AccountNumberMasked = string.IsNullOrWhiteSpace(accountNumberMasked) ? null : accountNumberMasked.Trim(),
                RoutingNumber = string.IsNullOrWhiteSpace(routingNumber) ? null : routingNumber.Trim(),
                IsActive = true
            };
            await uow.BankAccounts.AddAsync(bankAccount);
            await uow.SaveChangesAsync();
            success = true;
        });
        if (success) await LoadAsync();
        return success;
    }

    public async Task<int> ImportCsvAsync(Guid bankAccountId, TextReader reader)
    {
        var imported = 0;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            imported = await _reconciliationService.ImportCsvAsync(uow, ActiveCompanyId, bankAccountId, reader);
        });
        return imported;
    }
}
