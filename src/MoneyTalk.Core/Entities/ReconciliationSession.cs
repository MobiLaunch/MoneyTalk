namespace MoneyTalk.Core.Entities;

public class ReconciliationSession : CompanyOwnedEntity
{
    public Guid BankAccountId { get; set; }
    public DateTime StatementDate { get; set; }
    public decimal StatementBeginningBalance { get; set; }
    public decimal StatementEndingBalance { get; set; }
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.InProgress;

    /// <summary>Ids of BankTransactions marked "cleared" as part of this session.</summary>
    public List<Guid> ClearedBankTransactionIds { get; set; } = new();

    public decimal ClearedBalance { get; set; }
    public decimal Difference => StatementEndingBalance - ClearedBalance;

    public DateTime? CompletedAtUtc { get; set; }
}
