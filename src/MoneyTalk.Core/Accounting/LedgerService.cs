using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

public record JournalLineInput(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Memo = null,
    Guid? CustomerId = null,
    Guid? VendorId = null);

/// <summary>The single choke point through which every dollar enters or leaves the books.
/// Every other service (invoices, bills, payments, reconciliation, recurring transactions)
/// builds a set of <see cref="JournalLineInput"/>s and calls
/// <see cref="PostJournalEntryAsync"/> rather than touching <see cref="Account.CurrentBalance"/>
/// directly, so the "debits always equal credits" invariant only has to be enforced in one
/// place.</summary>
public class LedgerService
{
    public async Task<JournalEntry> PostJournalEntryAsync(
        IUnitOfWork uow,
        Guid companyId,
        DateTime date,
        string? memo,
        JournalSourceType sourceType,
        Guid? sourceId,
        IEnumerable<JournalLineInput> lines,
        string? createdByUserName = null,
        CancellationToken ct = default)
    {
        var lineList = lines.ToList();
        if (lineList.Count < 2)
            throw new InvalidOperationException("A journal entry needs at least two lines.");

        foreach (var line in lineList)
        {
            if (line.Debit < 0 || line.Credit < 0)
                throw new InvalidOperationException("Debit and credit amounts must not be negative.");
            if (line.Debit > 0 && line.Credit > 0)
                throw new InvalidOperationException("A single journal line cannot have both a debit and a credit.");
            if (line.Debit == 0 && line.Credit == 0)
                throw new InvalidOperationException("A journal line must have either a debit or a credit amount.");
        }

        var totalDebits = lineList.Sum(l => l.Debit);
        var totalCredits = lineList.Sum(l => l.Credit);
        if (Math.Round(totalDebits - totalCredits, 2) != 0)
            throw new InvalidOperationException(
                $"Journal entry is not balanced: debits {totalDebits:0.00} vs credits {totalCredits:0.00}.");

        var entryNumber = await GetNextEntryNumberAsync(uow, companyId, ct);
        var entry = new JournalEntry
        {
            CompanyId = companyId,
            EntryNumber = entryNumber,
            Date = date,
            Memo = memo,
            Status = JournalEntryStatus.Posted,
            SourceType = sourceType,
            SourceId = sourceId,
            CreatedByUserName = createdByUserName
        };

        foreach (var line in lineList)
        {
            var account = await uow.Accounts.GetByIdAsync(line.AccountId, ct)
                ?? throw new InvalidOperationException($"Account {line.AccountId} was not found.");
            if (!account.IsActive)
                throw new InvalidOperationException($"Account '{account.Name}' is inactive and cannot be posted to.");

            entry.Lines.Add(new JournalLine
            {
                JournalEntryId = entry.Id,
                AccountId = line.AccountId,
                Debit = line.Debit,
                Credit = line.Credit,
                Memo = line.Memo ?? memo,
                CustomerId = line.CustomerId,
                VendorId = line.VendorId
            });

            var delta = account.NormalBalance == NormalBalance.Debit
                ? line.Debit - line.Credit
                : line.Credit - line.Debit;
            account.CurrentBalance += delta;
            account.ModifiedAtUtc = DateTime.UtcNow;
            uow.Accounts.Update(account);
        }

        await uow.JournalEntries.AddAsync(entry, ct);
        return entry;
    }

    /// <summary>Reverses a posted entry by creating an equal-and-opposite entry, rather than
    /// deleting history — keeping the audit trail intact for anything that has ever hit the
    /// books.</summary>
    public async Task<JournalEntry> VoidJournalEntryAsync(
        IUnitOfWork uow, Guid journalEntryId, string? reason, string? voidedByUserName = null, CancellationToken ct = default)
    {
        var entry = await uow.JournalEntries.GetByIdAsync(journalEntryId, ct)
            ?? throw new InvalidOperationException($"Journal entry {journalEntryId} was not found.");
        if (entry.Status != JournalEntryStatus.Posted)
            throw new InvalidOperationException("Only posted journal entries can be voided.");

        var reversalLines = entry.Lines.Select(l => new JournalLineInput(
            l.AccountId, l.Credit, l.Debit, $"Reversal of JE #{entry.EntryNumber}", l.CustomerId, l.VendorId));

        var reversal = await PostJournalEntryAsync(
            uow, entry.CompanyId, DateTime.UtcNow.Date,
            $"Reversal of JE #{entry.EntryNumber}" + (string.IsNullOrWhiteSpace(reason) ? "" : $": {reason}"),
            JournalSourceType.Adjustment, entry.Id, reversalLines, voidedByUserName, ct);
        reversal.ReversalOfJournalEntryId = entry.Id;

        entry.Status = JournalEntryStatus.Void;
        entry.ModifiedAtUtc = DateTime.UtcNow;
        uow.JournalEntries.Update(entry);

        return reversal;
    }

    public async Task<decimal> GetAccountBalanceAsOfAsync(IUnitOfWork uow, Guid accountId, DateTime asOfDate, CancellationToken ct = default)
    {
        var account = await uow.Accounts.GetByIdAsync(accountId, ct)
            ?? throw new InvalidOperationException($"Account {accountId} was not found.");

        var entries = await uow.JournalEntries.FindAsync(
            e => e.CompanyId == account.CompanyId && e.Status == JournalEntryStatus.Posted && e.Date <= asOfDate, ct);

        decimal balance = 0;
        foreach (var entry in entries)
        {
            foreach (var line in entry.Lines.Where(l => l.AccountId == accountId))
            {
                balance += account.NormalBalance == NormalBalance.Debit
                    ? line.Debit - line.Credit
                    : line.Credit - line.Debit;
            }
        }
        return balance;
    }

    /// <summary>Rebuilds every account's cached <see cref="Account.CurrentBalance"/> from
    /// scratch by replaying all posted journal entries. A safety valve if balances ever drift
    /// (e.g. after a manual data fix or a bug), never used on the normal posting path.</summary>
    public async Task RebuildAccountBalancesAsync(IUnitOfWork uow, Guid companyId, CancellationToken ct = default)
    {
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == companyId, ct);
        var postedEntries = await uow.JournalEntries.FindAsync(
            e => e.CompanyId == companyId && e.Status == JournalEntryStatus.Posted, ct);

        var balances = accounts.ToDictionary(a => a.Id, _ => 0m);
        foreach (var entry in postedEntries)
        {
            foreach (var line in entry.Lines)
            {
                if (!balances.ContainsKey(line.AccountId)) continue;
                var account = accounts.First(a => a.Id == line.AccountId);
                balances[line.AccountId] += account.NormalBalance == NormalBalance.Debit
                    ? line.Debit - line.Credit
                    : line.Credit - line.Debit;
            }
        }

        foreach (var account in accounts)
        {
            account.CurrentBalance = balances[account.Id];
            uow.Accounts.Update(account);
        }
    }

    private static async Task<int> GetNextEntryNumberAsync(IUnitOfWork uow, Guid companyId, CancellationToken ct)
    {
        var existing = await uow.JournalEntries.FindAsync(e => e.CompanyId == companyId, ct);
        return existing.Count == 0 ? 1001 : existing.Max(e => e.EntryNumber) + 1;
    }
}
