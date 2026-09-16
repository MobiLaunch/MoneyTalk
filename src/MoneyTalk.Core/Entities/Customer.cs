namespace MoneyTalk.Core.Entities;

public class Customer : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? BillingAddress { get; set; }
    public string? ShippingAddress { get; set; }
    public bool TaxExempt { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    /// <summary>Cached open accounts-receivable balance owed by this customer.</summary>
    public decimal Balance { get; set; }
}
