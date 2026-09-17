using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

public record PosCartLine(Guid? ItemId, string Description, decimal Quantity, decimal UnitPrice, Guid? IncomeAccountId);

/// <summary>Point-of-sale checkout: ringing up a cart of items/services, or collecting payment
/// against an open repair ticket. A cart sale for a known customer posts as a real
/// <see cref="Invoice"/> that's immediately paid in full (so it shows up in that customer's
/// purchase history); an anonymous walk-in sale skips the invoice and posts a single direct
/// journal entry instead, since there's no A/R to track. Ticket-mode checkout is a thin wrapper
/// over <see cref="RepairTicketService.RecordPaymentAsync"/> — paying off a ticket at the
/// register is the same operation as recording a payment from the ticket's own detail page.</summary>
public class PosService
{
    private readonly InvoiceService _invoiceService;
    private readonly LedgerService _ledger;
    private readonly RepairTicketService _ticketService;

    public PosService(InvoiceService invoiceService, LedgerService ledger, RepairTicketService ticketService)
    {
        _invoiceService = invoiceService;
        _ledger = ledger;
        _ticketService = ticketService;
    }

    /// <summary>Pays down an open repair ticket's balance from the POS register.</summary>
    public Task<RepairTicketPayment> CompleteTicketPaymentAsync(
        IUnitOfWork uow, Guid ticketId, decimal amount, PaymentMethod method, Guid depositToAccountId, CancellationToken ct = default) =>
        _ticketService.RecordPaymentAsync(uow, ticketId, amount, DateTime.UtcNow, method, depositToAccountId, ct);

    /// <summary>Rings up a cart of items/services. Returns the posted invoice id when a customer
    /// was attached, or null for an anonymous walk-in sale (which posts a direct journal entry
    /// instead of an invoice).</summary>
    public async Task<Guid?> CompleteCartSaleAsync(
        IUnitOfWork uow, Guid companyId, Guid? customerId, IReadOnlyList<PosCartLine> lines,
        PaymentMethod method, Guid depositToAccountId, CancellationToken ct = default)
    {
        if (lines.Count == 0)
            throw new InvalidOperationException("Add at least one item to the cart first.");
        if (lines.Any(l => l.IncomeAccountId == null))
            throw new InvalidOperationException("Every item needs an income account assigned (set this on Products & Services).");

        var total = lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2));
        Guid? invoiceId = null;

        if (customerId.HasValue)
        {
            var existingCount = (await uow.Invoices.FindAsync(i => i.CompanyId == companyId, ct)).Count;
            var invoice = new Invoice { CompanyId = companyId, InvoiceNumber = $"POS-{1001 + existingCount}", CustomerId = customerId.Value };
            foreach (var line in lines)
            {
                invoice.Lines.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id,
                    ItemId = line.ItemId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    IncomeAccountId = line.IncomeAccountId
                });
            }

            await _invoiceService.SaveDraftAsync(uow, invoice, ct);
            await _invoiceService.PostInvoiceAsync(uow, invoice.Id, ct);

            var payment = new Payment
            {
                CompanyId = companyId,
                CustomerId = customerId.Value,
                PaymentDate = DateTime.UtcNow.Date,
                Amount = total,
                Method = method,
                DepositToAccountId = depositToAccountId
            };
            payment.Applications.Add(new PaymentApplication { PaymentId = payment.Id, InvoiceId = invoice.Id, AmountApplied = total });
            await _invoiceService.RecordPaymentAsync(uow, payment, ct);

            invoiceId = invoice.Id;
        }
        else
        {
            var journalLines = new List<JournalLineInput> { new(depositToAccountId, total, 0, "Walk-in POS sale") };
            foreach (var group in lines.GroupBy(l => l.IncomeAccountId!.Value))
                journalLines.Add(new JournalLineInput(group.Key, 0, group.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2)), "Walk-in POS sale"));

            await _ledger.PostJournalEntryAsync(
                uow, companyId, DateTime.UtcNow.Date, "Walk-in POS sale", JournalSourceType.PosSale, null, journalLines, ct: ct);
        }

        foreach (var line in lines.Where(l => l.ItemId.HasValue))
        {
            var item = await uow.Items.GetByIdAsync(line.ItemId!.Value, ct);
            if (item is { Type: ItemType.Inventory })
            {
                item.QuantityOnHand -= line.Quantity;
                uow.Items.Update(item);
            }
        }

        await uow.SaveChangesAsync(ct);
        return invoiceId;
    }
}
