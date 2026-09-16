namespace MoneyTalk.Core.Entities;

public class Bill : CompanyOwnedEntity
{
    public string BillNumber { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public DateTime BillDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    public BillStatus Status { get; set; } = BillStatus.Open;
    public string? Memo { get; set; }
    public string? VendorReferenceNumber { get; set; }

    public List<BillLine> Lines { get; set; } = new();

    public decimal Subtotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Journal entry created when the bill was posted (Debit Expense/COGS, Credit A/P).</summary>
    public Guid? JournalEntryId { get; set; }

    public bool IsOverdue(DateTime asOf) =>
        Status is BillStatus.Open or BillStatus.PartiallyPaid
        && Balance > 0
        && DueDate.Date < asOf.Date;
}

public class BillLine : EntityBase
{
    public Guid BillId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid ExpenseAccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public Guid? TaxRateId { get; set; }

    public decimal Amount => Math.Round(Quantity * UnitPrice, 2);
}
