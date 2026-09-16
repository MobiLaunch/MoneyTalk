using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using Xunit;

namespace MoneyTalk.Tests;

public class ReportingServiceTests
{
    [Fact]
    public async Task TrialBalance_AlwaysBalances()
    {
        using var db = new TestDatabase();
        var company = await db.CreateCompanyAsync();
        var ledger = new LedgerService();
        var reporting = new ReportingService();

        using (var uow = db.NewUnitOfWork())
        {
            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == company.Id);
            var checking = accounts.Single(a => a.Code == "1000");
            var sales = accounts.Single(a => a.Code == "4000");
            var rent = accounts.Single(a => a.Code == "6080");

            await ledger.PostJournalEntryAsync(uow, company.Id, DateTime.UtcNow, "Sale", JournalSourceType.Manual, null,
                new[] { new JournalLineInput(checking.Id, 1000m, 0m), new JournalLineInput(sales.Id, 0m, 1000m) });
            await ledger.PostJournalEntryAsync(uow, company.Id, DateTime.UtcNow, "Rent", JournalSourceType.Manual, null,
                new[] { new JournalLineInput(rent.Id, 400m, 0m), new JournalLineInput(checking.Id, 0m, 400m) });
            await uow.SaveChangesAsync();
        }

        using var reportUow = db.NewUnitOfWork();
        var trialBalance = await reporting.GetTrialBalanceAsync(reportUow, company.Id, DateTime.UtcNow);
        Assert.Equal(trialBalance.TotalDebits, trialBalance.TotalCredits);

        var pnl = await reporting.GetProfitAndLossAsync(reportUow, company.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        Assert.Equal(1000m, pnl.TotalIncome);
        Assert.Equal(400m, pnl.TotalExpenses);
        Assert.Equal(600m, pnl.NetIncome);

        var balanceSheet = await reporting.GetBalanceSheetAsync(reportUow, company.Id, DateTime.UtcNow);
        Assert.True(balanceSheet.IsBalanced);
        Assert.Equal(600m, balanceSheet.TotalAssets); // net cash after the rent payment
    }

    [Fact]
    public async Task ArAging_BucketsOverdueInvoiceCorrectly()
    {
        using var db = new TestDatabase();
        var company = await db.CreateCompanyAsync();
        var reporting = new ReportingService();
        var invoiceService = new InvoiceService(new LedgerService());

        using var uow = db.NewUnitOfWork();
        var customer = new Customer { CompanyId = company.Id, Name = "Late Payer LLC" };
        await uow.Customers.AddAsync(customer);
        var incomeAccount = (await uow.Accounts.FindAsync(a => a.CompanyId == company.Id && a.Code == "4000")).Single();
        await uow.SaveChangesAsync();

        var invoice = new Invoice
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            InvoiceNumber = "INV-OLD",
            InvoiceDate = DateTime.UtcNow.AddDays(-90),
            DueDate = DateTime.UtcNow.AddDays(-45)
        };
        invoice.Lines.Add(new InvoiceLine { InvoiceId = invoice.Id, Description = "Overdue work", Quantity = 1, UnitPrice = 750m, IncomeAccountId = incomeAccount.Id });
        await invoiceService.SaveDraftAsync(uow, invoice);
        await invoiceService.PostInvoiceAsync(uow, invoice.Id);

        var aging = await reporting.GetAccountsReceivableAgingAsync(uow, company.Id, DateTime.UtcNow);
        var line = Assert.Single(aging.Lines);
        Assert.Equal(750m, line.Days31To60);
        Assert.Equal(0m, line.Current);
    }
}
