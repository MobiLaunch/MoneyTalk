using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

public class BudgetService
{
    public async Task<Budget> SaveAsync(IUnitOfWork uow, Budget budget, CancellationToken ct = default)
    {
        var existing = await uow.Budgets.GetByIdAsync(budget.Id, ct);
        if (existing == null)
            await uow.Budgets.AddAsync(budget, ct);
        else
            uow.Budgets.Update(budget);

        await uow.SaveChangesAsync(ct);
        return budget;
    }

    /// <summary>Compares each budget line's planned amount for the month against the account's
    /// actual net activity for that same month.</summary>
    public async Task<List<BudgetVsActualLine>> GetBudgetVsActualAsync(IUnitOfWork uow, Guid budgetId, CancellationToken ct = default)
    {
        var budget = await uow.Budgets.GetByIdAsync(budgetId, ct)
            ?? throw new InvalidOperationException($"Budget {budgetId} was not found.");
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == budget.CompanyId, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);

        var reporting = new ReportingService();
        var results = new List<BudgetVsActualLine>();

        foreach (var monthGroup in budget.Lines.GroupBy(l => l.Month))
        {
            var monthStart = new DateTime(budget.FiscalYear, monthGroup.Key, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var pnl = await reporting.GetProfitAndLossAsync(uow, budget.CompanyId, monthStart, monthEnd, ct);
            var actualsByAccount = pnl.IncomeLines.Concat(pnl.CogsLines).Concat(pnl.ExpenseLines)
                .ToDictionary(l => l.AccountId, l => l.Amount);

            foreach (var line in monthGroup)
            {
                results.Add(new BudgetVsActualLine
                {
                    AccountId = line.AccountId,
                    AccountName = accountsById.TryGetValue(line.AccountId, out var acc) ? acc.Name : "Unknown account",
                    Month = line.Month,
                    BudgetAmount = line.Amount,
                    ActualAmount = actualsByAccount.GetValueOrDefault(line.AccountId)
                });
            }
        }

        return results.OrderBy(r => r.Month).ThenBy(r => r.AccountName).ToList();
    }
}
