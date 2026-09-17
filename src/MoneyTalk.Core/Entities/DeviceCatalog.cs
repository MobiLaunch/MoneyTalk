namespace MoneyTalk.Core.Entities;

/// <summary>Per-company device brand/category/model lists used by the new-ticket and trade-in
/// wizards. Seeded with a common starter set on company creation, editable/extensible afterward
/// — matches the source system's per-shop override lists layered on top of a shared default set.</summary>
public class DeviceBrand : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional icon reference (a bundled asset name or a simpleicons.org slug) —
    /// resolved by the UI, not fetched live over the network at query time.</summary>
    public string? IconRef { get; set; }
}

public class DeviceCategory : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = "📦";
}

public class DeviceModel : CompanyOwnedEntity
{
    public Guid DeviceBrandId { get; set; }
    public Guid DeviceCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}
