using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class BudgetsViewModel : ViewModelBase
{
    private readonly BudgetService _budgetService;

    public ObservableCollection<Budget> Budgets { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<BudgetVsActualLine> BudgetLines { get; } = new();

    [ObservableProperty] private Budget? selectedBudget;

    public static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    public BudgetsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, BudgetService budgetService)
        : base(unitOfWorkFactory, settingsService)
    {
        _budgetService = budgetService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var budgets = await uow.Budgets.FindAsync(b => b.CompanyId == ActiveCompanyId);
            Budgets.Clear();
            foreach (var budget in budgets.OrderByDescending(b => b.FiscalYear)) Budgets.Add(budget);

            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == ActiveCompanyId &&
                (a.Type == AccountType.Income || a.Type == AccountType.Expense || a.Type == AccountType.CostOfGoodsSold) && a.IsActive);
            Accounts.Clear();
            foreach (var account in accounts.OrderBy(a => a.Code)) Accounts.Add(account);

            SelectedBudget ??= Budgets.FirstOrDefault();
        });

        if (SelectedBudget != null) await RefreshBudgetVsActualAsync();
    }

    partial void OnSelectedBudgetChanged(Budget? value) => _ = RefreshBudgetVsActualAsync();

    private async Task RefreshBudgetVsActualAsync()
    {
        if (SelectedBudget == null) { BudgetLines.Clear(); return; }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var lines = await _budgetService.GetBudgetVsActualAsync(uow, SelectedBudget.Id);
            BudgetLines.Clear();
            foreach (var line in lines) BudgetLines.Add(line);
        });
    }

    public async Task<bool> CreateBudgetAsync(string name, int fiscalYear)
    {
        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var budget = new Budget { CompanyId = ActiveCompanyId, Name = name.Trim(), FiscalYear = fiscalYear };
            await _budgetService.SaveAsync(uow, budget);
            Budgets.Add(budget);
            SelectedBudget = budget;
            success = true;
        });
        return success;
    }

    public async Task<bool> AddBudgetLineAsync(Account account, int month, decimal amount)
    {
        if (SelectedBudget == null) return false;

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var budget = await uow.Budgets.GetByIdAsync(SelectedBudget.Id)
                ?? throw new InvalidOperationException("Budget no longer exists.");
            budget.Lines.Add(new BudgetLine { BudgetId = budget.Id, AccountId = account.Id, Month = month, Amount = amount });
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await RefreshBudgetVsActualAsync();
        return success;
    }
}
