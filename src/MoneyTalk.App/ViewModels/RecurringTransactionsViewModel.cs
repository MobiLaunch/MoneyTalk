using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class RecurringTransactionsViewModel : ViewModelBase
{
    private readonly RecurringTransactionService _recurringTransactionService;

    public ObservableCollection<RecurringTransaction> RecurringTransactions { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<NewJournalLineRow> NewTemplateLines { get; } = new();

    [ObservableProperty] private string newTemplateName = string.Empty;
    [ObservableProperty] private DateTimeOffset newTemplateStartDate = DateTimeOffset.Now;

    public IReadOnlyList<string> FrequencyOptions { get; } = Enum.GetNames<RecurrenceFrequency>();
    [ObservableProperty] private int selectedFrequencyIndex = (int)RecurrenceFrequency.Monthly;

    public RecurringTransactionsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, RecurringTransactionService recurringTransactionService)
        : base(unitOfWorkFactory, settingsService)
    {
        _recurringTransactionService = recurringTransactionService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var recurring = await uow.RecurringTransactions.FindAsync(r => r.CompanyId == ActiveCompanyId);
            RecurringTransactions.Clear();
            foreach (var item in recurring.OrderBy(r => r.NextRunDate)) RecurringTransactions.Add(item);

            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == ActiveCompanyId && a.IsActive);
            Accounts.Clear();
            foreach (var account in accounts.OrderBy(a => a.Code)) Accounts.Add(account);
        });
    }

    public void AddTemplateLine(Account account, decimal debit, decimal credit)
    {
        NewTemplateLines.Add(new NewJournalLineRow { AccountId = account.Id, AccountName = $"{account.Code} · {account.Name}", Debit = debit, Credit = credit });
    }

    [RelayCommand]
    private void RemoveTemplateLine(NewJournalLineRow? row)
    {
        if (row != null) NewTemplateLines.Remove(row);
    }

    [RelayCommand]
    private async Task CreateRecurringJournalEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTemplateName)) { ErrorMessage = "Give the recurring entry a name."; return; }
        var debits = NewTemplateLines.Sum(l => l.Debit);
        var credits = NewTemplateLines.Sum(l => l.Credit);
        if (NewTemplateLines.Count < 2 || Math.Round(debits - credits, 2) != 0)
        {
            ErrorMessage = "Add at least two lines where debits equal credits.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var template = new RecurringJournalEntryTemplate
            {
                Memo = NewTemplateName,
                Lines = NewTemplateLines.Select(l => new RecurringJournalLineTemplate(l.AccountId, l.Debit, l.Credit, l.Memo)).ToList()
            };

            var recurring = new RecurringTransaction
            {
                CompanyId = ActiveCompanyId,
                Name = NewTemplateName.Trim(),
                TemplateType = RecurringTemplateType.JournalEntry,
                Frequency = (RecurrenceFrequency)SelectedFrequencyIndex,
                NextRunDate = NewTemplateStartDate.DateTime,
                TemplatePayloadJson = JsonSerializer.Serialize(template)
            };
            await uow.RecurringTransactions.AddAsync(recurring);
            await uow.SaveChangesAsync();

            RecurringTransactions.Add(recurring);
            NewTemplateLines.Clear();
            NewTemplateName = string.Empty;
        });
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(RecurringTransaction? item)
    {
        if (item == null) return;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var tracked = await uow.RecurringTransactions.GetByIdAsync(item.Id)
                ?? throw new InvalidOperationException("This recurring transaction no longer exists.");
            tracked.IsActive = !tracked.IsActive;
            uow.RecurringTransactions.Update(tracked);
            await uow.SaveChangesAsync();
            item.IsActive = tracked.IsActive;
        });
    }

    [RelayCommand]
    private async Task RunDueNowAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _recurringTransactionService.ProcessDueTransactionsAsync(uow, ActiveCompanyId, DateTime.UtcNow.Date);
        });
        await LoadAsync();
    }
}
