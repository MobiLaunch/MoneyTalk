namespace MoneyTalk.Core.Entities;

public class Vendor : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public bool Is1099Vendor { get; set; }
    public string? TaxId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    /// <summary>Cached open accounts-payable balance owed to this vendor.</summary>
    public decimal Balance { get; set; }

    /// <summary>Running total of payments made to this vendor in the current calendar year,
    /// used to flag which vendors cross the 1099-NEC reporting threshold.</summary>
    public decimal YtdPayments { get; set; }
}
