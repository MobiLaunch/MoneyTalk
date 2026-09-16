using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class ReconcileTransactionRow : ObservableObject
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }

    [ObservableProperty] private bool isCleared;
}

public partial class ReconciliationViewModel : ViewModelBase
{
    private readonly ReconciliationService _reconciliationService;

    public ObservableCollection<BankAccount> BankAccounts { get; } = new();
    public ObservableCollection<ReconcileTransactionRow> Transactions { get; } = new();

    [ObservableProperty] private BankAccount? selectedBankAccount;
    [ObservableProperty] private bool hasActiveSession;
    [ObservableProperty] private Guid activeSessionId;
    [ObservableProperty] private decimal statementBeginningBalance;
    [ObservableProperty] private decimal statementEndingBalance;
    [ObservableProperty] private DateTimeOffset statementDate = DateTimeOffset.Now;
    [ObservableProperty] private string clearedBalanceDisplay = "$0.00";
    [ObservableProperty] private string differenceDisplay = "$0.00";
    [ObservableProperty] private bool isBalanced;

    public ReconciliationViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, ReconciliationService reconciliationService)
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
            BankAccounts.Clear();
            foreach (var account in bankAccounts) BankAccounts.Add(account);
            SelectedBankAccount ??= BankAccounts.FirstOrDefault();
        });

        if (SelectedBankAccount != null)
            await RefreshForSelectedAccountAsync();
    }

    partial void OnSelectedBankAccountChanged(BankAccount? value) => _ = RefreshForSelectedAccountAsync();

    private async Task RefreshForSelectedAccountAsync()
    {
        if (SelectedBankAccount == null) return;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var session = await uow.ReconciliationSessions.FirstOrDefaultAsync(
                s => s.BankAccountId == SelectedBankAccount.Id && s.Status == ReconciliationStatus.InProgress);

            HasActiveSession = session != null;
            if (session != null)
            {
                ActiveSessionId = session.Id;
                StatementBeginningBalance = session.StatementBeginningBalance;
                StatementEndingBalance = session.StatementEndingBalance;
                StatementDate = session.StatementDate;
            }
            else
            {
                StatementBeginningBalance = SelectedBankAccount.LastReconciledBalance;
            }

            var transactions = await uow.BankTransactions.FindAsync(t =>
                t.BankAccountId == SelectedBankAccount.Id && t.Status != BankTransactionStatus.Reconciled && t.Status != BankTransactionStatus.Excluded);

            Transactions.Clear();
            foreach (var transaction in transactions.OrderBy(t => t.Date))
            {
                var row = new ReconcileTransactionRow
                {
                    Id = transaction.Id,
                    Date = transaction.Date,
                    Description = transaction.Description,
                    Amount = transaction.Amount,
                    IsCleared = session?.ClearedBankTransactionIds.Contains(transaction.Id) ?? false
                };
                row.PropertyChanged += async (_, args) =>
                {
                    if (args.PropertyName == nameof(ReconcileTransactionRow.IsCleared))
                        await ToggleClearedAsync(row);
                };
                Transactions.Add(row);
            }

            RecalculateDifference();
        });
    }

    [RelayCommand]
    private async Task StartSessionAsync()
    {
        if (SelectedBankAccount == null) return;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var session = await _reconciliationService.StartSessionAsync(
                uow, ActiveCompanyId, SelectedBankAccount.Id, StatementDate.DateTime, StatementBeginningBalance, StatementEndingBalance);
            ActiveSessionId = session.Id;
            HasActiveSession = true;
        });
    }

    private async Task ToggleClearedAsync(ReconcileTransactionRow row)
    {
        if (!HasActiveSession) return;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _reconciliationService.ToggleClearedAsync(uow, ActiveSessionId, row.Id, row.IsCleared);
            RecalculateDifference();
        });
    }

    [RelayCommand]
    private async Task AutoMatchAsync()
    {
        if (SelectedBankAccount == null) return;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _reconciliationService.AutoMatchAsync(uow, SelectedBankAccount.Id);
        });
        await RefreshForSelectedAccountAsync();
    }

    [RelayCommand]
    private async Task CompleteSessionAsync()
    {
        if (!HasActiveSession) return;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _reconciliationService.CompleteSessionAsync(uow, ActiveSessionId);
            HasActiveSession = false;
        });
        await RefreshForSelectedAccountAsync();
    }

    private void RecalculateDifference()
    {
        var clearedBalance = StatementBeginningBalance + Transactions.Where(t => t.IsCleared).Sum(t => t.Amount);
        ClearedBalanceDisplay = clearedBalance.ToString("C2");
        var difference = StatementEndingBalance - clearedBalance;
        DifferenceDisplay = difference.ToString("C2");
        IsBalanced = Math.Round(difference, 2) == 0;
    }
}
