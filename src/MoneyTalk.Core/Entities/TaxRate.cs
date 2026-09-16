namespace MoneyTalk.Core.Entities;

public class TaxRate : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? AgencyName { get; set; }

    /// <summary>Percentage, e.g. 8.25 for 8.25%.</summary>
    public decimal RatePercent { get; set; }
    public bool IsDefault { get; set; }
    public Guid? LiabilityAccountId { get; set; }
}
