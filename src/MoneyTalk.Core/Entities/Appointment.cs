namespace MoneyTalk.Core.Entities;

public class Appointment : CompanyOwnedEntity
{
    public Guid? CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? ScheduledTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public string? Notes { get; set; }
}
