namespace MoneyTalk.Core.Entities;

public class BankAccount : CompanyOwnedEntity
{
    /// <summary>The Chart-of-Accounts account this bank/credit-card feed is tied to. Must be
    /// <see cref="AccountSubType.Bank"/> or <see cref="AccountSubType.CreditCard"/>.</summary>
    public Guid AccountId { get; set; }

    public string BankName { get; set; } = string.Empty;
    public string? AccountNumberMasked { get; set; }
    public string? RoutingNumber { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime? LastReconciledDate { get; set; }
    public decimal LastReconciledBalance { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A raw bank-feed line: manually entered, CSV-imported, or synced from Square deposits.
/// These get matched against <see cref="JournalLine"/>s during reconciliation.</summary>
public class BankTransaction : CompanyOwnedEntity
{
    public Guid BankAccountId { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Positive for money in, negative for money out.</summary>
    public decimal Amount { get; set; }

    public BankTransactionStatus Status { get; set; } = BankTransactionStatus.Unmatched;
    public BankTransactionSource Source { get; set; } = BankTransactionSource.Manual;
    public string? ExternalId { get; set; }
    public Guid? MatchedJournalEntryId { get; set; }
}
