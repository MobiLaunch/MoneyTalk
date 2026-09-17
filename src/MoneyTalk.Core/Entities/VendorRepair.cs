namespace MoneyTalk.Core.Entities;

/// <summary>Tracks a device sent out to a third-party repair vendor. <see cref="Status"/> is a
/// free-text workflow stage (e.g. "Preparing to Ship", "Shipped", "At Vendor", "Returned") rather
/// than a fixed enum, since vendor workflows vary.</summary>
public class VendorRepair : CompanyOwnedEntity
{
    public Guid? CustomerId { get; set; }
    public string? Device { get; set; }
    public string? Issue { get; set; }
    public string? VendorName { get; set; }

    /// <summary>Free-text reference back to the originating <see cref="RepairTicket"/>, matching
    /// how the source system tracks this (not a hard foreign key).</summary>
    public string? TicketRef { get; set; }
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = "Preparing to Ship";
    public DateTime? SentDate { get; set; }
    public DateTime? EstimatedReturnDate { get; set; }
    public string? Notes { get; set; }

    public bool IsOverdue(DateTime asOf) =>
        EstimatedReturnDate.HasValue
        && EstimatedReturnDate.Value.Date < asOf.Date
        && Status is not ("Returned" or "Completed");
}
