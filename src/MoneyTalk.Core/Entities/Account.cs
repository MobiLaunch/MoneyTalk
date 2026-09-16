namespace MoneyTalk.Core.Entities;

/// <summary>A single line in the chart of accounts. This is the backbone of the double-entry
/// ledger: every journal line debits or credits exactly one <see cref="Account"/>.</summary>
public class Account : CompanyOwnedEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AccountType Type { get; set; }
    public AccountSubType SubType { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemAccount { get; set; }

    /// <summary>Cached running balance, maintained by <c>LedgerService</c> every time a journal
    /// entry touching this account is posted or voided. Always recomputable from
    /// <see cref="JournalLine"/> history, so it is safe to rebuild if it ever drifts.</summary>
    public decimal CurrentBalance { get; set; }

    public NormalBalance NormalBalance => Type switch
    {
        AccountType.Asset => Entities.NormalBalance.Debit,
        AccountType.Expense => Entities.NormalBalance.Debit,
        AccountType.CostOfGoodsSold => Entities.NormalBalance.Debit,
        AccountType.Liability => Entities.NormalBalance.Credit,
        AccountType.Equity => Entities.NormalBalance.Credit,
        AccountType.Income => Entities.NormalBalance.Credit,
        _ => Entities.NormalBalance.Debit
    };
}
