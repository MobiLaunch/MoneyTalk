namespace MoneyTalk.Core.Entities;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Base for entities that belong to a single company (this app supports one active
/// company at a time in the UI, but every record is tagged so multi-company support can be
/// added later without a data migration).</summary>
public abstract class CompanyOwnedEntity : EntityBase
{
    public Guid CompanyId { get; set; }
}
