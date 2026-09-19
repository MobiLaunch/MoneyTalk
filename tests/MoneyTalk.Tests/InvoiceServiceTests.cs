using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using Xunit;

namespace MoneyTalk.Tests;

public class InvoiceServiceTests
{
    private static async Task<(Guid CompanyId, Guid CustomerId, Guid IncomeAccountId, Guid ArAccountId, Guid BankAccountId)> SeedAsync(TestDatabase db)
    {
        var company = await db.CreateCompanyAsync();
        using var uow = db.NewUnitOfWork();

        var customer = new Customer { CompanyId = company.Id, Name = "Acme Co" };
        await uow.Customers.AddAsync(customer);

        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == company.Id);
        var incomeAccount = accounts.Single(a => a.Code == "4000");
        var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == company.Id);

        await uow.SaveChangesAsync();

        return (company.Id, customer.Id, incomeAccount.Id, company.DefaultArAccountId!.Value, bankAccounts.Single().AccountId);
    }

    [Fact]
    public async Task PostInvoice_DebitsArAndCreditsIncome()
    {
        using var db = new TestDatabase();
        var (companyId, customerId, incomeAccountId, arAccountId, _) = await SeedAsync(db);
        var invoiceService = new InvoiceService(new LedgerService());

        Guid invoiceId;
        using (var uow = db.NewUnitOfWork())
        {
            var invoice = new Invoice { CompanyId = companyId, CustomerId = customerId, InvoiceNumber = "INV-1" };
            invoice.Lines.Add(new InvoiceLine { InvoiceId = invoice.Id, Description = "Consulting", Quantity = 2, UnitPrice = 150m, IncomeAccountId = incomeAccountId });
            await invoiceService.SaveDraftAsync(uow, invoice);
            invoiceId = invoice.Id;

            await invoiceService.PostInvoiceAsync(uow, invoiceId);
        }

        using var verifyUow = db.NewUnitOfWork();
        var posted = await verifyUow.Invoices.GetByIdAsync(invoiceId);
        Assert.Equal(InvoiceStatus.Sent, posted!.Status);
        Assert.Equal(300m, posted.Total);
        Assert.Equal(300m, posted.Balance);

        var arAccount = await verifyUow.Accounts.GetByIdAsync(arAccountId);
        var incomeAccount = await verifyUow.Accounts.GetByIdAsync(incomeAccountId);
        Assert.Equal(300m, arAccount!.CurrentBalance);
        Assert.Equal(300m, incomeAccount!.CurrentBalance);

        var customer = await verifyUow.Customers.GetByIdAsync(customerId);
        Assert.Equal(300m, customer!.Balance);
    }

    [Fact]
    public async Task RecordPayment_ReducesBalanceAndMarksPaid()
    {
        using var db = new TestDatabase();
        var (companyId, customerId, incomeAccountId, arAccountId, bankAccountId) = await SeedAsync(db);
        var invoiceService = new InvoiceService(new LedgerService());

        Guid invoiceId;
        using (var uow = db.NewUnitOfWork())
        {
            var invoice = new Invoice { CompanyId = companyId, CustomerId = customerId, InvoiceNumber = "INV-2" };
            invoice.Lines.Add(new InvoiceLine { InvoiceId = invoice.Id, Description = "Design work", Quantity = 1, UnitPrice = 500m, IncomeAccountId = incomeAccountId });
            await invoiceService.SaveDraftAsync(uow, invoice);
            invoiceId = invoice.Id;
            await invoiceService.PostInvoiceAsync(uow, invoiceId);
        }

        using (var uow = db.NewUnitOfWork())
        {
            var payment = new Payment { CompanyId = companyId, CustomerId = customerId, Amount = 500m, DepositToAccountId = bankAccountId };
            payment.Applications.Add(new PaymentApplication { PaymentId = payment.Id, InvoiceId = invoiceId, AmountApplied = 500m });
            await invoiceService.RecordPaymentAsync(uow, payment);
        }

        using var verifyUow = db.NewUnitOfWork();
        var invoice2 = await verifyUow.Invoices.GetByIdAsync(invoiceId);
        Assert.Equal(InvoiceStatus.Paid, invoice2!.Status);
        Assert.Equal(0m, invoice2.Balance);

        var arAccount = await verifyUow.Accounts.GetByIdAsync(arAccountId);
        Assert.Equal(0m, arAccount!.CurrentBalance);

        var bankAccount = await verifyUow.Accounts.GetByIdAsync(bankAccountId);
        Assert.Equal(500m, bankAccount!.CurrentBalance);

        var customer = await verifyUow.Customers.GetByIdAsync(customerId);
        Assert.Equal(0m, customer!.Balance);
    }

    [Fact]
    public async Task RecordPayment_RejectsAnAmountGreaterThanTheInvoiceBalance()
    {
        using var db = new TestDatabase();
        var (companyId, customerId, incomeAccountId, _, bankAccountId) = await SeedAsync(db);
        var invoiceService = new InvoiceService(new LedgerService());

        Guid invoiceId;
        using (var uow = db.NewUnitOfWork())
        {
            var invoice = new Invoice { CompanyId = companyId, CustomerId = customerId, InvoiceNumber = "INV-OVERPAY" };
            invoice.Lines.Add(new InvoiceLine { InvoiceId = invoice.Id, Description = "Repair", Quantity = 1, UnitPrice = 100m, IncomeAccountId = incomeAccountId });
            await invoiceService.SaveDraftAsync(uow, invoice);
            invoiceId = invoice.Id;
            await invoiceService.PostInvoiceAsync(uow, invoiceId);
        }

        using (var uow = db.NewUnitOfWork())
        {
            var payment = new Payment { CompanyId = companyId, CustomerId = customerId, Amount = 101m, DepositToAccountId = bankAccountId };
            payment.Applications.Add(new PaymentApplication { PaymentId = payment.Id, InvoiceId = invoiceId, AmountApplied = 101m });

            await Assert.ThrowsAsync<InvalidOperationException>(() => invoiceService.RecordPaymentAsync(uow, payment));
        }

        using var verifyUow = db.NewUnitOfWork();
        var invoiceAfter = await verifyUow.Invoices.GetByIdAsync(invoiceId);
        Assert.Equal(100m, invoiceAfter!.Balance);
        Assert.Empty(await verifyUow.Payments.GetAllAsync());
        Assert.Single(await verifyUow.JournalEntries.GetAllAsync()); // The invoice entry only; no payment entry was posted.
    }
}
