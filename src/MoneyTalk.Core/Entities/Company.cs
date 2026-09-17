namespace MoneyTalk.Core.Entities;

public class Company : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? Ein { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = "US";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? LogoPath { get; set; }

    /// <summary>1 = January ... 12 = December.</summary>
    public int FiscalYearStartMonth { get; set; } = 1;
    public string BaseCurrency { get; set; } = "USD";

    /// <summary>Threshold used by the dashboard / AI advisor to flag a low cash-runway warning.</summary>
    public decimal LowCashWarningThreshold { get; set; } = 5000m;

    // Default posting accounts. Set once during onboarding (DataSeeder wires these up against
    // the standard chart of accounts template) so services never have to guess which account
    // to hit for A/R, A/P, sales tax, etc.
    public Guid? DefaultArAccountId { get; set; }
    public Guid? DefaultApAccountId { get; set; }
    public Guid? DefaultUndepositedFundsAccountId { get; set; }
    public Guid? DefaultSalesTaxLiabilityAccountId { get; set; }
    public Guid? DefaultRetainedEarningsAccountId { get; set; }
    public Guid? DefaultOpeningBalanceEquityAccountId { get; set; }

    /// <summary>Comma-separated, shop-editable list of valid <see cref="RepairTicket.Status"/>
    /// values, in workflow order (e.g. "Open, In Progress, Waiting for Parts, Completed,
    /// Delivered"). Kept as free text rather than an enum since repair shops vary this list.</summary>
    public string TicketStatuses { get; set; } = "Open, In Progress, Waiting for Parts, Completed, Delivered";
}
