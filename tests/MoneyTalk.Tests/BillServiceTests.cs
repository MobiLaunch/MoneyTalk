using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using Xunit;

namespace MoneyTalk.Tests;

public class BillServiceTests
{
    private static async Task<(Guid CompanyId, Guid VendorId, Guid ExpenseAccountId, Guid ApAccountId, Guid BankAccountId)> SeedAsync(TestDatabase db)
    {
        var company = await db.CreateCompanyAsync();
        using var uow = db.NewUnitOfWork();

        var vendor = new Vendor { CompanyId = company.Id, Name = "Office Supply Co" };
        await uow.Vendors.AddAsync(vendor);

        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == company.Id);
        var expenseAccount = accounts.Single(a => a.Code == "6050"); // Office Supplies
        var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == company.Id);

        await uow.SaveChangesAsync();

        return (company.Id, vendor.Id, expenseAccount.Id, company.DefaultApAccountId!.Value, bankAccounts.Single().AccountId);
    }

    [Fact]
    public async Task PostBill_DebitsExpenseAndCreditsAccountsPayable()
    {
        using var db = new TestDatabase();
        var (companyId, vendorId, expenseAccountId, apAccountId, _) = await SeedAsync(db);
        var billService = new BillService(new LedgerService());

        Guid billId;
        using (var uow = db.NewUnitOfWork())
        {
            var bill = new Bill { CompanyId = companyId, VendorId = vendorId, BillNumber = "BILL-1" };
            bill.Lines.Add(new BillLine { BillId = bill.Id, ExpenseAccountId = expenseAccountId, Description = "Paper & toner", Quantity = 1, UnitPrice = 220m });
            await billService.SaveDraftAsync(uow, bill);
            billId = bill.Id;
            await billService.PostBillAsync(uow, billId);
        }

        using var verifyUow = db.NewUnitOfWork();
        var posted = await verifyUow.Bills.GetByIdAsync(billId);
        Assert.Equal(220m, posted!.Total);
        Assert.Equal(220m, posted.Balance);

        var apAccount = await verifyUow.Accounts.GetByIdAsync(apAccountId);
        var expenseAccount = await verifyUow.Accounts.GetByIdAsync(expenseAccountId);
        Assert.Equal(220m, apAccount!.CurrentBalance);
        Assert.Equal(220m, expenseAccount!.CurrentBalance);

        var vendor = await verifyUow.Vendors.GetByIdAsync(vendorId);
        Assert.Equal(220m, vendor!.Balance);
    }

    [Fact]
    public async Task RecordPayment_ClearsBillAndReducesCash()
    {
        using var db = new TestDatabase();
        var (companyId, vendorId, expenseAccountId, apAccountId, bankAccountId) = await SeedAsync(db);
        var billService = new BillService(new LedgerService());

        // Give the bank account a starting balance so paying the bill can go negative-free.
        using (var uow = db.NewUnitOfWork())
        {
            var ledger = new LedgerService();
            var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == companyId);
            var equity = accounts.Single(a => a.Code == "3050");
            await ledger.PostJournalEntryAsync(uow, companyId, DateTime.UtcNow, "Opening balance", JournalSourceType.OpeningBalance, null,
                new[] { new JournalLineInput(bankAccountId, 1000m, 0m), new JournalLineInput(equity.Id, 0m, 1000m) });
            await uow.SaveChangesAsync();
        }

        Guid billId;
        using (var uow = db.NewUnitOfWork())
        {
            var bill = new Bill { CompanyId = companyId, VendorId = vendorId, BillNumber = "BILL-2" };
            bill.Lines.Add(new BillLine { BillId = bill.Id, ExpenseAccountId = expenseAccountId, Description = "Consulting", Quantity = 1, UnitPrice = 400m });
            await billService.SaveDraftAsync(uow, bill);
            billId = bill.Id;
            await billService.PostBillAsync(uow, billId);
        }

        using (var uow = db.NewUnitOfWork())
        {
            var payment = new BillPayment { CompanyId = companyId, VendorId = vendorId, Amount = 400m, PaidFromAccountId = bankAccountId };
            payment.Applications.Add(new BillPaymentApplication { BillPaymentId = payment.Id, BillId = billId, AmountApplied = 400m });
            await billService.RecordPaymentAsync(uow, payment);
        }

        using var verifyUow = db.NewUnitOfWork();
        var bill2 = await verifyUow.Bills.GetByIdAsync(billId);
        Assert.Equal(BillStatus.Paid, bill2!.Status);
        Assert.Equal(0m, bill2.Balance);

        var apAccount = await verifyUow.Accounts.GetByIdAsync(apAccountId);
        Assert.Equal(0m, apAccount!.CurrentBalance);

        var bankAccount = await verifyUow.Accounts.GetByIdAsync(bankAccountId);
        Assert.Equal(600m, bankAccount!.CurrentBalance);
    }
}
