using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class JournalEntryRow
{
    public int EntryNumber { get; init; }
    public DateTime Date { get; init; }
    public string Memo { get; init; } = string.Empty;
    public JournalSourceType SourceType { get; init; }
    public JournalEntryStatus Status { get; init; }
    public decimal Total { get; init; }
}

public partial class NewJournalLineRow : ObservableObject
{
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    [ObservableProperty] private decimal debit;
    [ObservableProperty] private decimal credit;
    [ObservableProperty] private string? memo;
}

public partial class JournalViewModel : ViewModelBase
{
    private readonly LedgerService _ledgerService;

    public ObservableCollection<JournalEntryRow> Entries { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<NewJournalLineRow> NewEntryLines { get; } = new();

    [ObservableProperty] private DateTimeOffset newEntryDate = DateTimeOffset.Now;
    [ObservableProperty] private string? newEntryMemo;
    [ObservableProperty] private string newEntryDebitTotalDisplay = "$0.00";
    [ObservableProperty] private string newEntryCreditTotalDisplay = "$0.00";
    [ObservableProperty] private bool isNewEntryBalanced;

    public JournalViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, LedgerService ledgerService)
        : base(unitOfWorkFactory, settingsService)
    {
        _ledgerService = ledgerService;
        NewEntryLines.CollectionChanged += (_, __) => RecalculateNewEntryTotals();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var entries = await uow.JournalEntries.FindAsync(e => e.CompanyId == ActiveCompanyId);
            Entries.Clear();
            foreach (var entry in entries.OrderByDescending(e => e.Date).ThenByDescending(e => e.EntryNumber))
            {
                Entries.Add(new JournalEntryRow
                {
                    EntryNumber = entry.EntryNumber,
                    Date = entry.Date,
                    Memo = entry.Memo ?? string.Empty,
                    SourceType = entry.SourceType,
                    Status = entry.Status,
                    Total = entry.TotalDebits
                });
            }

            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == ActiveCompanyId && a.IsActive);
            Accounts.Clear();
            foreach (var account in accounts.OrderBy(a => a.Code)) Accounts.Add(account);
        });
    }

    public void AddNewEntryLine(Account account, decimal debit, decimal credit, string? memo)
    {
        var row = new NewJournalLineRow { AccountId = account.Id, AccountName = $"{account.Code} · {account.Name}", Debit = debit, Credit = credit, Memo = memo };
        row.PropertyChanged += (_, __) => RecalculateNewEntryTotals();
        NewEntryLines.Add(row);
    }

    [RelayCommand]
    private void RemoveNewEntryLine(NewJournalLineRow? row)
    {
        if (row != null) NewEntryLines.Remove(row);
    }

    private void RecalculateNewEntryTotals()
    {
        var debits = NewEntryLines.Sum(l => l.Debit);
        var credits = NewEntryLines.Sum(l => l.Credit);
        NewEntryDebitTotalDisplay = debits.ToString("C2");
        NewEntryCreditTotalDisplay = credits.ToString("C2");
        IsNewEntryBalanced = NewEntryLines.Count >= 2 && Math.Round(debits - credits, 2) == 0;
    }

    [RelayCommand]
    private async Task PostNewEntryAsync()
    {
        if (!IsNewEntryBalanced)
        {
            ErrorMessage = "Debits must equal credits (and you need at least two lines) before posting.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var lines = NewEntryLines.Select(l => new JournalLineInput(l.AccountId, l.Debit, l.Credit, l.Memo));
            await _ledgerService.PostJournalEntryAsync(uow, ActiveCompanyId, NewEntryDate.DateTime, NewEntryMemo, JournalSourceType.Manual, null, lines);
            await uow.SaveChangesAsync();

            NewEntryLines.Clear();
            NewEntryMemo = null;
        });

        await LoadAsync();
    }
}
