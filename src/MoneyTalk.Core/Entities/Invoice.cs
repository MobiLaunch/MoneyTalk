namespace MoneyTalk.Core.Entities;

public class Invoice : CompanyOwnedEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? Memo { get; set; }
    public string? Terms { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();

    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Journal entry created when the invoice was posted (Debit A/R, Credit Income/Tax).</summary>
    public Guid? JournalEntryId { get; set; }

    public bool IsOverdue(DateTime asOf) =>
        Status is InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue
        && Balance > 0
        && DueDate.Date < asOf.Date;
}

public class InvoiceLine : EntityBase
{
    public Guid InvoiceId { get; set; }
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public Guid? TaxRateId { get; set; }
    public Guid? IncomeAccountId { get; set; }

    public decimal Amount => Math.Round(Quantity * UnitPrice, 2);
}
