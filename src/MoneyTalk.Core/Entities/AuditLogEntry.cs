namespace MoneyTalk.Core.Entities;

/// <summary>Append-only trail of who changed what. Every accounting mutation (post, void,
/// reconcile, delete) should write one of these so books stay auditable.</summary>
public class AuditLogEntry : CompanyOwnedEntity
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string? Details { get; set; }
}
