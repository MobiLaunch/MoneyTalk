namespace MoneyTalk.Core.Entities;

/// <summary>A balanced double-entry transaction. Every posted entry must have
/// sum(debits) == sum(credits) across its <see cref="Lines"/>; this invariant is enforced by
/// <c>LedgerService.PostJournalEntry</c>, never by the UI.</summary>
public class JournalEntry : CompanyOwnedEntity
{
    public int EntryNumber { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
    public string? Memo { get; set; }
    public string? Reference { get; set; }
    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;
    public JournalSourceType SourceType { get; set; } = JournalSourceType.Manual;

    /// <summary>Id of the Invoice/Bill/Payment/etc. that generated this entry, if any.</summary>
    public Guid? SourceId { get; set; }

    /// <summary>If this entry reverses another one (e.g. voiding), points at the original.</summary>
    public Guid? ReversalOfJournalEntryId { get; set; }

    public string? CreatedByUserName { get; set; }

    public List<JournalLine> Lines { get; set; } = new();

    public decimal TotalDebits => Lines.Sum(l => l.Debit);
    public decimal TotalCredits => Lines.Sum(l => l.Credit);
    public bool IsBalanced => TotalDebits == TotalCredits;
}

public class JournalLine : EntityBase
{
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Memo { get; set; }

    /// <summary>Optional subledger tags so AR/AP and customer/vendor activity can be traced
    /// back to individual ledger lines for statements and aging reports.</summary>
    public Guid? CustomerId { get; set; }
    public Guid? VendorId { get; set; }

    public bool Reconciled { get; set; }
    public Guid? BankTransactionId { get; set; }
}
