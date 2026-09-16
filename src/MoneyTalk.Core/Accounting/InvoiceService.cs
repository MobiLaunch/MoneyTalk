using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Customer-facing (accounts-receivable) side of the ledger: drafting invoices,
/// posting them to the books, and applying customer payments against open balances.</summary>
public class InvoiceService
{
    private readonly LedgerService _ledger;

    public InvoiceService(LedgerService ledger)
    {
        _ledger = ledger;
    }

    public static void RecalculateTotals(Invoice invoice, IReadOnlyDictionary<Guid, TaxRate>? taxRatesById = null)
    {
        invoice.Subtotal = invoice.Lines.Sum(l => l.Amount);
        decimal taxTotal = 0m;
        if (taxRatesById != null)
        {
            foreach (var line in invoice.Lines)
            {
                if (line.TaxRateId.HasValue && taxRatesById.TryGetValue(line.TaxRateId.Value, out var rate))
                    taxTotal += Math.Round(line.Amount * rate.RatePercent / 100m, 2);
            }
        }
        invoice.TaxTotal = taxTotal;
        invoice.Total = invoice.Subtotal + invoice.TaxTotal;
        invoice.Balance = invoice.Total - invoice.AmountPaid;
    }

    public async Task<Invoice> SaveDraftAsync(IUnitOfWork uow, Invoice invoice, CancellationToken ct = default)
    {
        RecalculateTotals(invoice);
        invoice.Status = InvoiceStatus.Draft;
        var existing = await uow.Invoices.GetByIdAsync(invoice.Id, ct);
        if (existing == null)
            await uow.Invoices.AddAsync(invoice, ct);
        else
            uow.Invoices.Update(invoice);

        await uow.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>Posts a draft invoice to the general ledger: Debit Accounts Receivable for the
    /// total, Credit each line's income account, Credit the sales-tax liability account for any
    /// tax collected.</summary>
    public async Task PostInvoiceAsync(IUnitOfWork uow, Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await uow.Invoices.GetByIdAsync(invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} was not found.");
        if (invoice.Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("Only draft invoices can be posted.");
        if (invoice.Lines.Count == 0)
            throw new InvalidOperationException("Cannot post an invoice with no line items.");
        if (invoice.Lines.Any(l => l.IncomeAccountId == null))
            throw new InvalidOperationException("Every invoice line needs an income account before it can be posted.");

        var company = await uow.Companies.GetByIdAsync(invoice.CompanyId, ct)
            ?? throw new InvalidOperationException("Company profile not found.");
        var arAccountId = company.DefaultArAccountId
            ?? throw new InvalidOperationException("No default Accounts Receivable account is configured for this company.");

        var lines = new List<JournalLineInput>
        {
            new(arAccountId, invoice.Total, 0m, $"Invoice {invoice.InvoiceNumber}", invoice.CustomerId)
        };

        foreach (var group in invoice.Lines.GroupBy(l => l.IncomeAccountId!.Value))
            lines.Add(new JournalLineInput(group.Key, 0m, group.Sum(l => l.Amount), $"Invoice {invoice.InvoiceNumber}", invoice.CustomerId));

        if (invoice.TaxTotal > 0)
        {
            var taxAccountId = company.DefaultSalesTaxLiabilityAccountId
                ?? throw new InvalidOperationException("No default sales-tax liability account is configured for this company.");
            lines.Add(new JournalLineInput(taxAccountId, 0m, invoice.TaxTotal, "Sales tax collected", invoice.CustomerId));
        }

        var journalEntry = await _ledger.PostJournalEntryAsync(
            uow, invoice.CompanyId, invoice.InvoiceDate, $"Invoice {invoice.InvoiceNumber}",
            Entities.JournalSourceType.Invoice, invoice.Id, lines, ct: ct);

        invoice.JournalEntryId = journalEntry.Id;
        invoice.Status = InvoiceStatus.Sent;
        invoice.ModifiedAtUtc = DateTime.UtcNow;
        uow.Invoices.Update(invoice);

        var customer = await uow.Customers.GetByIdAsync(invoice.CustomerId, ct);
        if (customer != null)
        {
            customer.Balance += invoice.Total;
            uow.Customers.Update(customer);
        }

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Records a customer payment, applies it across the requested invoices, and posts
    /// Debit [deposit account] / Credit Accounts Receivable.</summary>
    public async Task<Payment> RecordPaymentAsync(IUnitOfWork uow, Payment payment, CancellationToken ct = default)
    {
        if (payment.Applications.Sum(a => a.AmountApplied) > payment.Amount)
            throw new InvalidOperationException("Cannot apply more than the total payment amount.");

        var company = await uow.Companies.GetByIdAsync(payment.CompanyId, ct)
            ?? throw new InvalidOperationException("Company profile not found.");
        var arAccountId = company.DefaultArAccountId
            ?? throw new InvalidOperationException("No default Accounts Receivable account is configured for this company.");

        var lines = new List<JournalLineInput>
        {
            new(payment.DepositToAccountId, payment.Amount, 0m, "Customer payment", payment.CustomerId),
            new(arAccountId, 0m, payment.Amount, "Customer payment", payment.CustomerId)
        };

        var journalEntry = await _ledger.PostJournalEntryAsync(
            uow, payment.CompanyId, payment.PaymentDate, "Customer payment received",
            Entities.JournalSourceType.CustomerPayment, payment.Id, lines, ct: ct);
        payment.JournalEntryId = journalEntry.Id;

        await uow.Payments.AddAsync(payment, ct);

        foreach (var application in payment.Applications)
        {
            var invoice = await uow.Invoices.GetByIdAsync(application.InvoiceId, ct)
                ?? throw new InvalidOperationException($"Invoice {application.InvoiceId} was not found.");
            invoice.AmountPaid += application.AmountApplied;
            invoice.Balance = invoice.Total - invoice.AmountPaid;
            invoice.Status = invoice.Balance <= 0
                ? InvoiceStatus.Paid
                : InvoiceStatus.PartiallyPaid;
            invoice.ModifiedAtUtc = DateTime.UtcNow;
            uow.Invoices.Update(invoice);
        }

        var customer = await uow.Customers.GetByIdAsync(payment.CustomerId, ct);
        if (customer != null)
        {
            customer.Balance -= payment.Applications.Sum(a => a.AmountApplied);
            uow.Customers.Update(customer);
        }

        await uow.SaveChangesAsync(ct);
        return payment;
    }

    /// <summary>Marks overdue invoices whose due date has passed. Intended to be called once at
    /// app startup / daily refresh, not on every read.</summary>
    public async Task RefreshOverdueStatusesAsync(IUnitOfWork uow, Guid companyId, DateTime asOfDate, CancellationToken ct = default)
    {
        var openInvoices = await uow.Invoices.FindAsync(
            i => i.CompanyId == companyId && (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid), ct);

        foreach (var invoice in openInvoices.Where(i => i.IsOverdue(asOfDate)))
        {
            invoice.Status = InvoiceStatus.Overdue;
            uow.Invoices.Update(invoice);
        }

        await uow.SaveChangesAsync(ct);
    }
}
