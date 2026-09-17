namespace MoneyTalk.Core.Entities;

/// <summary>An on-site (customer's location) repair appointment.</summary>
public class HouseCall : CompanyOwnedEntity
{
    public Guid? CustomerId { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? ScheduledTime { get; set; }
    public HouseCallStatus Status { get; set; } = HouseCallStatus.Scheduled;
    public string? Notes { get; set; }
}
