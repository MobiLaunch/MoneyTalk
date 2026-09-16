namespace MoneyTalk.Core.Entities;

/// <summary>A payment received from a customer, applied against one or more open invoices.</summary>
public class Payment : CompanyOwnedEntity
{
    public Guid CustomerId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Other;
    public Guid DepositToAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Memo { get; set; }

    /// <summary>Set when this payment originated from a synced Square transaction.</summary>
    public string? SquareTransactionId { get; set; }

    public Guid? JournalEntryId { get; set; }

    public List<PaymentApplication> Applications { get; set; } = new();

    public decimal UnappliedAmount => Amount - Applications.Sum(a => a.AmountApplied);
}

public class PaymentApplication : EntityBase
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal AmountApplied { get; set; }
}

/// <summary>A payment made to a vendor, applied against one or more open bills.</summary>
public class BillPayment : CompanyOwnedEntity
{
    public Guid VendorId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Other;
    public Guid PaidFromAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Memo { get; set; }

    public Guid? JournalEntryId { get; set; }

    public List<BillPaymentApplication> Applications { get; set; } = new();

    public decimal UnappliedAmount => Amount - Applications.Sum(a => a.AmountApplied);
}

public class BillPaymentApplication : EntityBase
{
    public Guid BillPaymentId { get; set; }
    public Guid BillId { get; set; }
    public decimal AmountApplied { get; set; }
}
