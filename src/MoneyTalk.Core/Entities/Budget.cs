namespace MoneyTalk.Core.Entities;

public class Budget : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public int FiscalYear { get; set; } = DateTime.UtcNow.Year;
    public List<BudgetLine> Lines { get; set; } = new();
}

public class BudgetLine : EntityBase
{
    public Guid BudgetId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>1-12.</summary>
    public int Month { get; set; }
    public decimal Amount { get; set; }
}
