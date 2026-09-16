using System.Globalization;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Bank/cash reconciliation: bringing the bank's statement balance and the ledger's
/// account balance into agreement by matching (or manually clearing) individual transactions.</summary>
public class ReconciliationService
{
    public async Task<ReconciliationSession> StartSessionAsync(
        IUnitOfWork uow, Guid companyId, Guid bankAccountId, DateTime statementDate,
        decimal statementBeginningBalance, decimal statementEndingBalance, CancellationToken ct = default)
    {
        var openSession = await uow.ReconciliationSessions.FirstOrDefaultAsync(
            s => s.BankAccountId == bankAccountId && s.Status == ReconciliationStatus.InProgress, ct);
        if (openSession != null)
            throw new InvalidOperationException("There is already a reconciliation in progress for this account.");

        var session = new ReconciliationSession
        {
            CompanyId = companyId,
            BankAccountId = bankAccountId,
            StatementDate = statementDate,
            StatementBeginningBalance = statementBeginningBalance,
            StatementEndingBalance = statementEndingBalance,
            Status = ReconciliationStatus.InProgress
        };
        await uow.ReconciliationSessions.AddAsync(session, ct);
        await uow.SaveChangesAsync(ct);
        return session;
    }

    public async Task ToggleClearedAsync(IUnitOfWork uow, Guid sessionId, Guid bankTransactionId, bool cleared, CancellationToken ct = default)
    {
        var session = await uow.ReconciliationSessions.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Reconciliation session {sessionId} was not found.");
        var transaction = await uow.BankTransactions.GetByIdAsync(bankTransactionId, ct)
            ?? throw new InvalidOperationException($"Bank transaction {bankTransactionId} was not found.");

        if (cleared && !session.ClearedBankTransactionIds.Contains(bankTransactionId))
        {
            session.ClearedBankTransactionIds.Add(bankTransactionId);
            session.ClearedBalance += transaction.Amount;
        }
        else if (!cleared && session.ClearedBankTransactionIds.Contains(bankTransactionId))
        {
            session.ClearedBankTransactionIds.Remove(bankTransactionId);
            session.ClearedBalance -= transaction.Amount;
        }

        uow.ReconciliationSessions.Update(session);
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Best-effort auto-matching: pairs unmatched bank transactions with unreconciled
    /// journal lines on the same underlying account that share an exact amount within a small
    /// date window. Anything left over needs a human to clear it by hand.</summary>
    public async Task<int> AutoMatchAsync(IUnitOfWork uow, Guid bankAccountId, int dateWindowDays = 4, CancellationToken ct = default)
    {
        var bankAccount = await uow.BankAccounts.GetByIdAsync(bankAccountId, ct)
            ?? throw new InvalidOperationException($"Bank account {bankAccountId} was not found.");

        var unmatchedTransactions = await uow.BankTransactions.FindAsync(
            t => t.BankAccountId == bankAccountId && t.Status == BankTransactionStatus.Unmatched, ct);

        var journalEntries = await uow.JournalEntries.FindAsync(
            e => e.CompanyId == bankAccount.CompanyId && e.Status == JournalEntryStatus.Posted, ct);

        var candidateLines = journalEntries
            .SelectMany(e => e.Lines.Select(l => (Entry: e, Line: l)))
            .Where(x => x.Line.AccountId == bankAccount.AccountId && !x.Line.Reconciled && x.Line.BankTransactionId == null)
            .ToList();

        int matched = 0;
        foreach (var transaction in unmatchedTransactions)
        {
            var match = candidateLines.FirstOrDefault(x =>
                Math.Abs((x.Entry.Date - transaction.Date).TotalDays) <= dateWindowDays &&
                AmountForBankAccount(x.Line) == transaction.Amount);

            if (match.Line == null) continue;

            match.Line.BankTransactionId = transaction.Id;
            transaction.Status = BankTransactionStatus.Matched;
            transaction.MatchedJournalEntryId = match.Entry.Id;

            uow.BankTransactions.Update(transaction);
            uow.JournalEntries.Update(match.Entry);
            candidateLines.Remove(match);
            matched++;
        }

        await uow.SaveChangesAsync(ct);
        return matched;

        static decimal AmountForBankAccount(JournalLine line) => line.Debit > 0 ? line.Debit : -line.Credit;
    }

    public async Task CompleteSessionAsync(IUnitOfWork uow, Guid sessionId, CancellationToken ct = default)
    {
        var session = await uow.ReconciliationSessions.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Reconciliation session {sessionId} was not found.");
        if (Math.Round(session.Difference, 2) != 0)
            throw new InvalidOperationException(
                $"Cannot complete: cleared balance is off by {session.Difference:0.00}. Every transaction must be matched or excluded first.");

        foreach (var transactionId in session.ClearedBankTransactionIds)
        {
            var transaction = await uow.BankTransactions.GetByIdAsync(transactionId, ct);
            if (transaction == null) continue;
            transaction.Status = BankTransactionStatus.Reconciled;
            uow.BankTransactions.Update(transaction);

            if (transaction.MatchedJournalEntryId.HasValue)
            {
                var entry = await uow.JournalEntries.GetByIdAsync(transaction.MatchedJournalEntryId.Value, ct);
                var line = entry?.Lines.FirstOrDefault(l => l.BankTransactionId == transactionId);
                if (line != null) line.Reconciled = true;
                if (entry != null) uow.JournalEntries.Update(entry);
            }
        }

        session.Status = ReconciliationStatus.Completed;
        session.CompletedAtUtc = DateTime.UtcNow;
        uow.ReconciliationSessions.Update(session);

        var bankAccount = await uow.BankAccounts.GetByIdAsync(session.BankAccountId, ct);
        if (bankAccount != null)
        {
            bankAccount.LastReconciledDate = session.StatementDate;
            bankAccount.LastReconciledBalance = session.StatementEndingBalance;
            uow.BankAccounts.Update(bankAccount);
        }

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Parses a generic bank CSV export (Date, Description, Amount columns — the
    /// common denominator across most banks' "download transactions" feature) into unmatched
    /// <see cref="BankTransaction"/> rows.</summary>
    public async Task<int> ImportCsvAsync(IUnitOfWork uow, Guid companyId, Guid bankAccountId, TextReader csvReader, CancellationToken ct = default)
    {
        string? headerLine = await csvReader.ReadLineAsync(ct);
        if (headerLine == null) return 0;

        var headers = SplitCsvLine(headerLine).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int dateIdx = headers.FindIndex(h => h.Contains("date"));
        int descIdx = headers.FindIndex(h => h.Contains("description") || h.Contains("memo") || h.Contains("payee"));
        int amountIdx = headers.FindIndex(h => h.Contains("amount"));
        if (dateIdx < 0 || amountIdx < 0)
            throw new InvalidOperationException("CSV must contain at least a Date column and an Amount column.");

        int imported = 0;
        string? line;
        while ((line = await csvReader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = SplitCsvLine(line);
            if (fields.Count <= amountIdx || fields.Count <= dateIdx) continue;

            if (!DateTime.TryParse(fields[dateIdx], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!decimal.TryParse(fields[amountIdx].Replace("$", "").Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                continue;

            var transaction = new BankTransaction
            {
                CompanyId = companyId,
                BankAccountId = bankAccountId,
                Date = date,
                Description = descIdx >= 0 && fields.Count > descIdx ? fields[descIdx] : string.Empty,
                Amount = amount,
                Source = BankTransactionSource.CsvImport,
                Status = BankTransactionStatus.Unmatched
            };
            await uow.BankTransactions.AddAsync(transaction, ct);
            imported++;
        }

        await uow.SaveChangesAsync(ct);
        return imported;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        fields.Add(current.ToString());
        return fields;
    }
}
