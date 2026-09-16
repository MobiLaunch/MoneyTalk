namespace MoneyTalk.Core.Dtos;

public record RecurringInvoiceLineTemplate(Guid? ItemId, string Description, decimal Quantity, decimal UnitPrice, Guid? TaxRateId, Guid? IncomeAccountId);

public class RecurringInvoiceTemplate
{
    public Guid CustomerId { get; set; }
    public int DueDays { get; set; } = 30;
    public string? Memo { get; set; }
    public string? Terms { get; set; }
    public List<RecurringInvoiceLineTemplate> Lines { get; set; } = new();
}

public record RecurringBillLineTemplate(Guid? ItemId, Guid ExpenseAccountId, string Description, decimal Quantity, decimal UnitPrice, Guid? TaxRateId);

public class RecurringBillTemplate
{
    public Guid VendorId { get; set; }
    public int DueDays { get; set; } = 30;
    public string? Memo { get; set; }
    public List<RecurringBillLineTemplate> Lines { get; set; } = new();
}

public record RecurringJournalLineTemplate(Guid AccountId, decimal Debit, decimal Credit, string? Memo);

public class RecurringJournalEntryTemplate
{
    public string? Memo { get; set; }
    public List<RecurringJournalLineTemplate> Lines { get; set; } = new();
}
