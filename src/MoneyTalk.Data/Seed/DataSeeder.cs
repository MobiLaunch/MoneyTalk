using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Data.Seed;

/// <summary>Sets up a brand-new company with a standard U.S. small-business chart of accounts
/// and wires up the default posting accounts (A/R, A/P, sales tax, undeposited funds) that the
/// accounting services rely on. Run once, during first-launch onboarding.</summary>
public static class DataSeeder
{
    public static async Task<Company> CreateCompanyWithDefaultsAsync(IUnitOfWork uow, string companyName, CancellationToken ct = default)
    {
        var company = new Company { Name = companyName };
        await uow.Companies.AddAsync(company, ct);

        var accounts = BuildDefaultChartOfAccounts(company.Id);
        foreach (var account in accounts)
            await uow.Accounts.AddAsync(account, ct);

        Guid Find(string code) => accounts.First(a => a.Code == code).Id;

        company.DefaultArAccountId = Find("1100");
        company.DefaultApAccountId = Find("2000");
        company.DefaultUndepositedFundsAccountId = Find("1050");
        company.DefaultSalesTaxLiabilityAccountId = Find("2200");
        company.DefaultOpeningBalanceEquityAccountId = Find("3050");
        company.DefaultRetainedEarningsAccountId = Find("3900");

        var bankAccount = new BankAccount
        {
            CompanyId = company.Id,
            AccountId = Find("1000"),
            BankName = "Primary Checking",
            IsActive = true
        };
        await uow.BankAccounts.AddAsync(bankAccount, ct);

        var defaultTaxRate = new TaxRate
        {
            CompanyId = company.Id,
            Name = "No Tax",
            RatePercent = 0,
            IsDefault = true
        };
        await uow.TaxRates.AddAsync(defaultTaxRate, ct);

        var owner = new User
        {
            CompanyId = company.Id,
            Name = "Owner",
            Email = "owner@example.com",
            Role = UserRole.Owner
        };
        await uow.Users.AddAsync(owner, ct);

        await uow.SaveChangesAsync(ct);
        return company;
    }

    public static List<Account> BuildDefaultChartOfAccounts(Guid companyId)
    {
        var definitions = new (string Code, string Name, AccountType Type, AccountSubType SubType, bool System)[]
        {
            ("1000", "Checking Account", AccountType.Asset, AccountSubType.Bank, true),
            ("1010", "Savings Account", AccountType.Asset, AccountSubType.Bank, false),
            ("1050", "Undeposited Funds", AccountType.Asset, AccountSubType.OtherCurrentAsset, true),
            ("1100", "Accounts Receivable", AccountType.Asset, AccountSubType.AccountsReceivable, true),
            ("1200", "Inventory Asset", AccountType.Asset, AccountSubType.OtherCurrentAsset, false),
            ("1400", "Prepaid Expenses", AccountType.Asset, AccountSubType.OtherCurrentAsset, false),
            ("1500", "Equipment", AccountType.Asset, AccountSubType.FixedAsset, false),
            ("1510", "Accumulated Depreciation", AccountType.Asset, AccountSubType.FixedAsset, false),

            ("2000", "Accounts Payable", AccountType.Liability, AccountSubType.AccountsPayable, true),
            ("2100", "Business Credit Card", AccountType.Liability, AccountSubType.CreditCard, false),
            ("2200", "Sales Tax Payable", AccountType.Liability, AccountSubType.OtherCurrentLiability, true),
            ("2300", "Payroll Liabilities", AccountType.Liability, AccountSubType.OtherCurrentLiability, false),
            ("2500", "Notes Payable (Long Term)", AccountType.Liability, AccountSubType.LongTermLiability, false),

            ("3000", "Owner's Contributions", AccountType.Equity, AccountSubType.OwnersEquity, false),
            ("3050", "Opening Balance Equity", AccountType.Equity, AccountSubType.OwnersEquity, true),
            ("3100", "Owner's Draws", AccountType.Equity, AccountSubType.OwnersEquity, false),
            ("3900", "Retained Earnings", AccountType.Equity, AccountSubType.RetainedEarnings, true),

            ("4000", "Sales Income", AccountType.Income, AccountSubType.Income, false),
            ("4010", "Service Income", AccountType.Income, AccountSubType.Income, false),
            ("4900", "Other Income", AccountType.Income, AccountSubType.OtherIncome, false),

            ("5000", "Cost of Goods Sold", AccountType.CostOfGoodsSold, AccountSubType.CostOfGoodsSold, false),

            ("6000", "Advertising & Marketing", AccountType.Expense, AccountSubType.Expense, false),
            ("6010", "Bank Fees & Charges", AccountType.Expense, AccountSubType.Expense, false),
            ("6020", "Contract Labor", AccountType.Expense, AccountSubType.Expense, false),
            ("6030", "Insurance", AccountType.Expense, AccountSubType.Expense, false),
            ("6040", "Meals & Entertainment", AccountType.Expense, AccountSubType.Expense, false),
            ("6050", "Office Supplies", AccountType.Expense, AccountSubType.Expense, false),
            ("6060", "Payroll Expenses", AccountType.Expense, AccountSubType.Expense, false),
            ("6070", "Professional Fees (Legal/Accounting)", AccountType.Expense, AccountSubType.Expense, false),
            ("6080", "Rent Expense", AccountType.Expense, AccountSubType.Expense, false),
            ("6090", "Software & Subscriptions", AccountType.Expense, AccountSubType.Expense, false),
            ("6100", "Travel", AccountType.Expense, AccountSubType.Expense, false),
            ("6110", "Utilities", AccountType.Expense, AccountSubType.Expense, false),
            ("6900", "Depreciation Expense", AccountType.Expense, AccountSubType.OtherExpense, false),
            ("6999", "Uncategorized Expense", AccountType.Expense, AccountSubType.OtherExpense, true),
        };

        return definitions.Select(d => new Account
        {
            CompanyId = companyId,
            Code = d.Code,
            Name = d.Name,
            Type = d.Type,
            SubType = d.SubType,
            IsSystemAccount = d.System,
            IsActive = true
        }).ToList();
    }
}
