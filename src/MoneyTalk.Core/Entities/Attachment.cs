namespace MoneyTalk.Core.Entities;

/// <summary>A receipt, statement, or supporting document attached to any record in the system
/// (invoice, bill, bank transaction, journal entry, etc.).</summary>
public class Attachment : CompanyOwnedEntity
{
    public string RelatedEntityType { get; set; } = string.Empty;
    public Guid RelatedEntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? ContentType { get; set; }
}
