namespace MoneyTalk.Core.Entities;

/// <summary>A product or service that can be sold (on invoices) or purchased (on bills).</summary>
public class Item : CompanyOwnedEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ItemType Type { get; set; } = ItemType.Service;
    public decimal SalesPrice { get; set; }
    public decimal Cost { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? IncomeAccountId { get; set; }
    public Guid? ExpenseAccountId { get; set; }
    public Guid? InventoryAssetAccountId { get; set; }

    /// <summary>Only meaningful when <see cref="Type"/> is <see cref="ItemType.Inventory"/>.</summary>
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderPoint { get; set; }
}
