using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using Xunit;

namespace MoneyTalk.Tests;

public class LedgerServiceTests
{
    [Fact]
    public async Task PostJournalEntry_UpdatesAccountBalancesByNormalBalance()
    {
        using var db = new TestDatabase();
        var company = await db.CreateCompanyAsync();
        var ledger = new LedgerService();

        using var uow = db.NewUnitOfWork();
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == company.Id);
        var checking = accounts.Single(a => a.Code == "1000");
        var sales = accounts.Single(a => a.Code == "4000");

        await ledger.PostJournalEntryAsync(uow, company.Id, DateTime.UtcNow, "Cash sale", JournalSourceType.Manual, null,
            new[]
            {
                new JournalLineInput(checking.Id, 100m, 0m),
                new JournalLineInput(sales.Id, 0m, 100m)
            });
        await uow.SaveChangesAsync();

        using var verifyUow = db.NewUnitOfWork();
        var updatedChecking = await verifyUow.Accounts.GetByIdAsync(checking.Id);
        var updatedSales = await verifyUow.Accounts.GetByIdAsync(sales.Id);

        Assert.Equal(100m, updatedChecking!.CurrentBalance); // Asset: debit increases balance.
        Assert.Equal(100m, updatedSales!.CurrentBalance);    // Income: credit increases balance.
    }

    [Fact]
    public async Task PostJournalEntry_ThrowsWhenNotBalanced()
    {
        using var db = new TestDatabase();
        var company = await db.CreateCompanyAsync();
        var ledger = new LedgerService();

        using var uow = db.NewUnitOfWork();
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == company.Id);
        var checking = accounts.Single(a => a.Code == "1000");
        var sales = accounts.Single(a => a.Code == "4000");

        await Assert.ThrowsAsync<InvalidOperationException>(() => ledger.PostJournalEntryAsync(
            uow, company.Id, DateTime.UtcNow, "Unbalanced", JournalSourceType.Manual, null,
            new[]
            {
                new JournalLineInput(checking.Id, 100m, 0m),
                new JournalLineInput(sales.Id, 0m, 50m)
            }));
    }

    [Fact]
    public async Task VoidJournalEntry_ReversesOriginalPosting()
    {
        using var db = new TestDatabase();
        var company = await db.CreateCompanyAsync();
        var ledger = new LedgerService();

        Guid checkingId, entryId;
        using (var uow = db.NewUnitOfWork())
        {
            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == company.Id);
            checkingId = accounts.Single(a => a.Code == "1000").Id;
            var salesId = accounts.Single(a => a.Code == "4000").Id;

            var entry = await ledger.PostJournalEntryAsync(uow, company.Id, DateTime.UtcNow, "Sale", JournalSourceType.Manual, null,
                new[] { new JournalLineInput(checkingId, 250m, 0m), new JournalLineInput(salesId, 0m, 250m) });
            entryId = entry.Id;
            await uow.SaveChangesAsync();
        }

        using (var uow = db.NewUnitOfWork())
        {
            await ledger.VoidJournalEntryAsync(uow, entryId, "test reversal");
            await uow.SaveChangesAsync();
        }

        using var verifyUow = db.NewUnitOfWork();
        var checking = await verifyUow.Accounts.GetByIdAsync(checkingId);
        Assert.Equal(0m, checking!.CurrentBalance);

        var original = await verifyUow.JournalEntries.GetByIdAsync(entryId);
        Assert.Equal(JournalEntryStatus.Void, original!.Status);
    }
}
