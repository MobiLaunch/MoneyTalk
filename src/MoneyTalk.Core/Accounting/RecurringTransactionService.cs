using System.Text.Json;
using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Turns recurring templates (rent bill, subscription invoice, monthly depreciation
/// entry, ...) into real, dated records once their <see cref="RecurringTransaction.NextRunDate"/>
/// arrives. Intended to be invoked once per app launch / daily background tick.</summary>
public class RecurringTransactionService
{
    private readonly InvoiceService _invoiceService;
    private readonly BillService _billService;
    private readonly LedgerService _ledgerService;

    public RecurringTransactionService(InvoiceService invoiceService, BillService billService, LedgerService ledgerService)
    {
        _invoiceService = invoiceService;
        _billService = billService;
        _ledgerService = ledgerService;
    }

    /// <summary>Generates (and, for invoices/bills, posts) every due recurring transaction as of
    /// <paramref name="asOfDate"/>, then advances each one's next run date. Returns how many ran.</summary>
    public async Task<int> ProcessDueTransactionsAsync(IUnitOfWork uow, Guid companyId, DateTime asOfDate, CancellationToken ct = default)
    {
        var due = await uow.RecurringTransactions.FindAsync(
            r => r.CompanyId == companyId && r.IsActive && r.NextRunDate <= asOfDate
                 && (r.EndDate == null || r.EndDate >= r.NextRunDate), ct);

        int processed = 0;
        foreach (var recurring in due)
        {
            switch (recurring.TemplateType)
            {
                case RecurringTemplateType.Invoice:
                    await RunInvoiceTemplateAsync(uow, companyId, recurring, ct);
                    break;
                case RecurringTemplateType.Bill:
                    await RunBillTemplateAsync(uow, companyId, recurring, ct);
                    break;
                case RecurringTemplateType.JournalEntry:
                    await RunJournalEntryTemplateAsync(uow, companyId, recurring, ct);
                    break;
            }

            recurring.LastRunDate = asOfDate;
            recurring.NextRunDate = RecurringTransaction.ComputeNextRunDate(recurring.NextRunDate, recurring.Frequency);
            if (recurring.EndDate.HasValue && recurring.NextRunDate > recurring.EndDate.Value)
                recurring.IsActive = false;

            uow.RecurringTransactions.Update(recurring);
            processed++;
        }

        await uow.SaveChangesAsync(ct);
        return processed;
    }

    private async Task RunInvoiceTemplateAsync(IUnitOfWork uow, Guid companyId, RecurringTransaction recurring, CancellationToken ct)
    {
        var template = JsonSerializer.Deserialize<RecurringInvoiceTemplate>(recurring.TemplatePayloadJson)
            ?? throw new InvalidOperationException("Invalid recurring invoice template.");

        var invoice = new Invoice
        {
            CompanyId = companyId,
            InvoiceNumber = $"REC-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CustomerId = template.CustomerId,
            InvoiceDate = recurring.NextRunDate,
            DueDate = recurring.NextRunDate.AddDays(template.DueDays),
            Memo = template.Memo,
            Terms = template.Terms
        };
        foreach (var line in template.Lines)
        {
            invoice.Lines.Add(new InvoiceLine
            {
                InvoiceId = invoice.Id,
                ItemId = line.ItemId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRateId = line.TaxRateId,
                IncomeAccountId = line.IncomeAccountId
            });
        }

        await _invoiceService.SaveDraftAsync(uow, invoice, ct);
        await _invoiceService.PostInvoiceAsync(uow, invoice.Id, ct);
    }

    private async Task RunBillTemplateAsync(IUnitOfWork uow, Guid companyId, RecurringTransaction recurring, CancellationToken ct)
    {
        var template = JsonSerializer.Deserialize<RecurringBillTemplate>(recurring.TemplatePayloadJson)
            ?? throw new InvalidOperationException("Invalid recurring bill template.");

        var bill = new Bill
        {
            CompanyId = companyId,
            BillNumber = $"REC-{DateTime.UtcNow:yyyyMMddHHmmss}",
            VendorId = template.VendorId,
            BillDate = recurring.NextRunDate,
            DueDate = recurring.NextRunDate.AddDays(template.DueDays),
            Memo = template.Memo
        };
        foreach (var line in template.Lines)
        {
            bill.Lines.Add(new BillLine
            {
                BillId = bill.Id,
                ItemId = line.ItemId,
                ExpenseAccountId = line.ExpenseAccountId,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRateId = line.TaxRateId
            });
        }

        await _billService.SaveDraftAsync(uow, bill, ct);
        await _billService.PostBillAsync(uow, bill.Id, ct);
    }

    private async Task RunJournalEntryTemplateAsync(IUnitOfWork uow, Guid companyId, RecurringTransaction recurring, CancellationToken ct)
    {
        var template = JsonSerializer.Deserialize<RecurringJournalEntryTemplate>(recurring.TemplatePayloadJson)
            ?? throw new InvalidOperationException("Invalid recurring journal entry template.");

        var lines = template.Lines.Select(l => new JournalLineInput(l.AccountId, l.Debit, l.Credit, l.Memo));
        await _ledgerService.PostJournalEntryAsync(
            uow, companyId, recurring.NextRunDate, template.Memo ?? recurring.Name,
            JournalSourceType.RecurringTransaction, recurring.Id, lines, ct: ct);
        await uow.SaveChangesAsync(ct);
    }
}
