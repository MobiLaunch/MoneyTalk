namespace MoneyTalk.Core.Entities;

/// <summary>A device repair job. <see cref="Status"/> is validated against the owning
/// <see cref="Company.TicketStatuses"/> list rather than a fixed enum, since shops customize
/// their workflow stages.</summary>
public class RepairTicket : CompanyOwnedEntity
{
    public Guid? CustomerId { get; set; }
    public string Device { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string? DeviceDescription { get; set; }
    public string Issue { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public decimal Price { get; set; }
    public string? SerialNumber { get; set; }
    public int WarrantyDays { get; set; }
    public DateTime? WarrantyStartDate { get; set; }
    public string? SignatureImagePath { get; set; }
    public string? TrackingInfo { get; set; }

    public List<RepairTicketNote> Notes { get; set; } = new();
    public List<RepairTicketLine> Lines { get; set; } = new();
    public List<RepairTicketPayment> Payments { get; set; } = new();

    /// <summary>Null until a warranty start date is recorded; never a stored column so it always
    /// reflects the current WarrantyDays/WarrantyStartDate values.</summary>
    public DateTime? WarrantyEndDate => WarrantyStartDate?.AddDays(WarrantyDays);

    public decimal AmountPaid => Payments.Sum(p => p.Amount);
    public decimal Balance => Math.Max(0, Price - AmountPaid);
}

public class RepairTicketNote : EntityBase
{
    public Guid RepairTicketId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

/// <summary>A part or labor line consumed by the repair — mirrors <see cref="InvoiceLine"/>'s
/// shape so it can be carried straight onto an invoice when the ticket is paid out.</summary>
public class RepairTicketLine : EntityBase
{
    public Guid RepairTicketId { get; set; }
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }

    public decimal Amount => Math.Round(Quantity * UnitPrice, 2);
}

public class RepairTicketPayment : EntityBase
{
    public Guid RepairTicketId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Set once this payment has been posted through the ledger as a real
    /// <see cref="Payment"/> (see PosViewModel's ticket-mode checkout in Phase 3).</summary>
    public Guid? PaymentId { get; set; }
}
