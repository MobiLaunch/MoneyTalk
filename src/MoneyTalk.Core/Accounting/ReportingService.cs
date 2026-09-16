using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Read-only financial statements, all derived directly from posted journal entries so
/// they can never disagree with the ledger they're reporting on.</summary>
public class ReportingService
{
    public async Task<ProfitAndLossReport> GetProfitAndLossAsync(IUnitOfWork uow, Guid companyId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == companyId, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);
        var activity = await GetPeriodActivityByAccountAsync(uow, companyId, start, end, ct);

        var report = new ProfitAndLossReport { Start = start, End = end };
        foreach (var (accountId, netAmount) in activity)
        {
            if (!accountsById.TryGetValue(accountId, out var account)) continue;
            var line = new ReportLine(account.Id, account.Code, account.Name, netAmount);
            switch (account.Type)
            {
                case AccountType.Income: report.IncomeLines.Add(line); break;
                case AccountType.CostOfGoodsSold: report.CogsLines.Add(line); break;
                case AccountType.Expense: report.ExpenseLines.Add(line); break;
            }
        }

        report.IncomeLines = report.IncomeLines.OrderBy(l => l.AccountCode).ToList();
        report.CogsLines = report.CogsLines.OrderBy(l => l.AccountCode).ToList();
        report.ExpenseLines = report.ExpenseLines.OrderBy(l => l.AccountCode).ToList();
        return report;
    }

    public async Task<BalanceSheetReport> GetBalanceSheetAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct = default)
    {
        var accounts = (await uow.Accounts.FindAsync(a => a.CompanyId == companyId, ct)).OrderBy(a => a.Code).ToList();
        var report = new BalanceSheetReport { AsOf = asOf };

        foreach (var account in accounts)
        {
            var balance = await GetBalanceAsOfAsync(uow, account, asOf, ct);
            if (balance == 0) continue;
            var line = new ReportLine(account.Id, account.Code, account.Name, balance);
            switch (account.Type)
            {
                case AccountType.Asset: report.Assets.Add(line); break;
                case AccountType.Liability: report.Liabilities.Add(line); break;
                case AccountType.Equity: report.Equity.Add(line); break;
            }
        }

        // Retained earnings = all-time net income from Income/COGS/Expense accounts up to asOf,
        // computed on the fly rather than persisted, so it can never fall out of sync.
        var fiscalStart = DateTime.MinValue;
        var pnl = await GetProfitAndLossAsync(uow, companyId, fiscalStart, asOf, ct);
        report.RetainedEarnings = pnl.NetIncome;

        return report;
    }

    public async Task<TrialBalanceReport> GetTrialBalanceAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct = default)
    {
        var accounts = (await uow.Accounts.FindAsync(a => a.CompanyId == companyId, ct)).OrderBy(a => a.Code).ToList();
        var report = new TrialBalanceReport { AsOf = asOf };

        foreach (var account in accounts)
        {
            var balance = await GetBalanceAsOfAsync(uow, account, asOf, ct);
            if (balance == 0) continue;

            decimal debit = 0, credit = 0;
            if (account.NormalBalance == NormalBalance.Debit)
            {
                if (balance >= 0) debit = balance; else credit = -balance;
            }
            else
            {
                if (balance >= 0) credit = balance; else debit = -balance;
            }
            report.Lines.Add(new TrialBalanceLine(account.Id, account.Code, account.Name, debit, credit));
        }

        return report;
    }

    /// <summary>Simplified direct-method cash flow statement: every posted journal line that
    /// touches a Bank/CreditCard account within the period is bucketed into Operating,
    /// Investing, or Financing based on the *other* side of the entry.</summary>
    public async Task<CashFlowStatement> GetCashFlowStatementAsync(IUnitOfWork uow, Guid companyId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == companyId, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);
        var cashAccountIds = accounts.Where(a => a.SubType is AccountSubType.Bank or AccountSubType.CreditCard).Select(a => a.Id).ToHashSet();

        var statement = new CashFlowStatement { Start = start, End = end };

        decimal beginningCash = 0;
        foreach (var accountId in cashAccountIds)
            beginningCash += await GetBalanceAsOfAsync(uow, accountsById[accountId], start.AddDays(-1), ct);
        statement.BeginningCash = beginningCash;

        var entries = await uow.JournalEntries.FindAsync(
            e => e.CompanyId == companyId && e.Status == JournalEntryStatus.Posted && e.Date >= start && e.Date <= end, ct);

        var buckets = new Dictionary<Guid, decimal>();
        foreach (var entry in entries)
        {
            var cashLines = entry.Lines.Where(l => cashAccountIds.Contains(l.AccountId)).ToList();
            if (cashLines.Count == 0) continue;
            var cashDelta = cashLines.Sum(l => l.Debit - l.Credit);
            if (cashDelta == 0) continue;

            var counterpartAccountId = entry.Lines.FirstOrDefault(l => !cashAccountIds.Contains(l.AccountId))?.AccountId;
            if (counterpartAccountId == null || !accountsById.TryGetValue(counterpartAccountId.Value, out var counterpart))
            {
                buckets[Guid.Empty] = buckets.GetValueOrDefault(Guid.Empty) + cashDelta;
                continue;
            }

            var category = ClassifyCashFlowCategory(counterpart);
            var key = counterpart.Id;
            var bucketList = category switch
            {
                CashFlowCategory.Investing => statement.InvestingLines,
                CashFlowCategory.Financing => statement.FinancingLines,
                _ => statement.OperatingLines
            };

            var existingLine = bucketList.FirstOrDefault(l => l.AccountId == key);
            var newAmount = (existingLine?.Amount ?? 0) + cashDelta;
            bucketList.RemoveAll(l => l.AccountId == key);
            bucketList.Add(new ReportLine(counterpart.Id, counterpart.Code, counterpart.Name, newAmount));
        }

        return statement;
    }

    private enum CashFlowCategory { Operating, Investing, Financing }

    private static CashFlowCategory ClassifyCashFlowCategory(Account account) => account.SubType switch
    {
        AccountSubType.FixedAsset or AccountSubType.OtherAsset => CashFlowCategory.Investing,
        AccountSubType.LongTermLiability or AccountSubType.OwnersEquity or AccountSubType.RetainedEarnings => CashFlowCategory.Financing,
        _ => CashFlowCategory.Operating
    };

    public async Task<AgingReport> GetAccountsReceivableAgingAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct = default)
    {
        var invoices = await uow.Invoices.FindAsync(
            i => i.CompanyId == companyId && i.Balance > 0 && i.Status != InvoiceStatus.Void && i.Status != InvoiceStatus.Draft, ct);
        var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId, ct);
        var customersById = customers.ToDictionary(c => c.Id);

        var report = new AgingReport { AsOf = asOf };
        foreach (var group in invoices.GroupBy(i => i.CustomerId))
        {
            var line = new AgingBucketLine
            {
                PartyId = group.Key,
                PartyName = customersById.TryGetValue(group.Key, out var c) ? c.Name : "Unknown customer"
            };
            foreach (var invoice in group)
                BucketByAge(line, invoice.DueDate, invoice.Balance, asOf);
            report.Lines.Add(line);
        }

        return report;
    }

    public async Task<AgingReport> GetAccountsPayableAgingAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct = default)
    {
        var bills = await uow.Bills.FindAsync(
            b => b.CompanyId == companyId && b.Balance > 0 && b.Status != BillStatus.Void, ct);
        var vendors = await uow.Vendors.FindAsync(v => v.CompanyId == companyId, ct);
        var vendorsById = vendors.ToDictionary(v => v.Id);

        var report = new AgingReport { AsOf = asOf };
        foreach (var group in bills.GroupBy(b => b.VendorId))
        {
            var line = new AgingBucketLine
            {
                PartyId = group.Key,
                PartyName = vendorsById.TryGetValue(group.Key, out var v) ? v.Name : "Unknown vendor"
            };
            foreach (var bill in group)
                BucketByAge(line, bill.DueDate, bill.Balance, asOf);
            report.Lines.Add(line);
        }

        return report;
    }

    private static void BucketByAge(AgingBucketLine line, DateTime dueDate, decimal amount, DateTime asOf)
    {
        var daysPastDue = (asOf.Date - dueDate.Date).Days;
        if (daysPastDue <= 0) line.Current += amount;
        else if (daysPastDue <= 30) line.Days1To30 += amount;
        else if (daysPastDue <= 60) line.Days31To60 += amount;
        else if (daysPastDue <= 90) line.Days61To90 += amount;
        else line.Over90 += amount;
    }

    /// <summary>Net activity per account (credit-normal accounts reported as positive when they
    /// increase) for postings strictly within [start, end]. Used by the P&amp;L, which only
    /// cares about period movement, not a cumulative balance.</summary>
    private static async Task<Dictionary<Guid, decimal>> GetPeriodActivityByAccountAsync(
        IUnitOfWork uow, Guid companyId, DateTime start, DateTime end, CancellationToken ct)
    {
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == companyId, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);

        var entries = await uow.JournalEntries.FindAsync(
            e => e.CompanyId == companyId && e.Status == JournalEntryStatus.Posted && e.Date >= start && e.Date <= end, ct);

        var activity = new Dictionary<Guid, decimal>();
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines)
            {
                if (!accountsById.TryGetValue(line.AccountId, out var account)) continue;
                var delta = account.NormalBalance == NormalBalance.Debit
                    ? line.Debit - line.Credit
                    : line.Credit - line.Debit;
                activity[line.AccountId] = activity.GetValueOrDefault(line.AccountId) + delta;
            }
        }
        return activity;
    }

    private static async Task<decimal> GetBalanceAsOfAsync(IUnitOfWork uow, Account account, DateTime asOf, CancellationToken ct)
    {
        var entries = await uow.JournalEntries.FindAsync(
            e => e.CompanyId == account.CompanyId && e.Status == JournalEntryStatus.Posted && e.Date <= asOf, ct);

        decimal balance = 0;
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.Where(l => l.AccountId == account.Id))
            {
                balance += account.NormalBalance == NormalBalance.Debit
                    ? line.Debit - line.Credit
                    : line.Credit - line.Debit;
            }
        }
        return balance;
    }
}
