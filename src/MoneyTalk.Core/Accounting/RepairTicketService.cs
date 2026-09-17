using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Repair-ticket workflow: saving tickets/notes/lines, and recording payments taken
/// against an open ticket. Payments post a real journal entry through <see cref="LedgerService"/>
/// (debit the deposit account, credit Service Income) rather than sitting in a flat, unposted
/// total — the same "every dollar goes through the ledger" rule the rest of the app follows.
/// Per-line/per-item income allocation (matching each <see cref="RepairTicketLine"/>'s own item)
/// is handled by the POS checkout flow; a ticket payment posts as one line against the shop's
/// default service-income account, which is correct for the common case of a flat repair price.</summary>
public class RepairTicketService
{
    private readonly LedgerService _ledger;

    public RepairTicketService(LedgerService ledger)
    {
        _ledger = ledger;
    }

    public async Task SaveAsync(IUnitOfWork uow, RepairTicket ticket, CancellationToken ct = default)
    {
        var existing = ticket.Id != Guid.Empty ? await uow.RepairTickets.GetByIdAsync(ticket.Id, ct) : null;
        if (existing == null) await uow.RepairTickets.AddAsync(ticket, ct);
        else uow.RepairTickets.Update(ticket);
        await uow.SaveChangesAsync(ct);
    }

    public async Task AddNoteAsync(IUnitOfWork uow, Guid ticketId, string text, string author, CancellationToken ct = default)
    {
        var ticket = await uow.RepairTickets.GetByIdAsync(ticketId, ct)
            ?? throw new InvalidOperationException("This ticket no longer exists.");
        ticket.Notes.Add(new RepairTicketNote { RepairTicketId = ticketId, Text = text, Author = author });
        ticket.ModifiedAtUtc = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    public async Task<RepairTicketPayment> RecordPaymentAsync(
        IUnitOfWork uow, Guid ticketId, decimal amount, DateTime paymentDate, PaymentMethod method,
        Guid depositToAccountId, CancellationToken ct = default)
    {
        if (amount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero.");

        var ticket = await uow.RepairTickets.GetByIdAsync(ticketId, ct)
            ?? throw new InvalidOperationException("This ticket no longer exists.");
        if (amount > ticket.Balance)
            throw new InvalidOperationException($"That's more than the {ticket.Balance:C2} balance owed on this ticket.");

        var incomeAccount = await uow.Accounts.FirstOrDefaultAsync(a => a.CompanyId == ticket.CompanyId && a.Code == "4010", ct)
            ?? await uow.Accounts.FirstOrDefaultAsync(a => a.CompanyId == ticket.CompanyId && a.Type == AccountType.Income, ct)
            ?? throw new InvalidOperationException("No income account is set up — add one on the Chart of Accounts page first.");

        await _ledger.PostJournalEntryAsync(
            uow, ticket.CompanyId, paymentDate, $"Payment for repair ticket ({ticket.Device})",
            JournalSourceType.RepairTicketPayment, ticket.Id,
            new[]
            {
                new JournalLineInput(depositToAccountId, amount, 0, "Ticket payment", ticket.CustomerId),
                new JournalLineInput(incomeAccount.Id, 0, amount, "Ticket payment", ticket.CustomerId)
            },
            ct: ct);

        var payment = new RepairTicketPayment
        {
            RepairTicketId = ticketId,
            Amount = amount,
            Method = method,
            PaidAtUtc = paymentDate
        };
        ticket.Payments.Add(payment);
        ticket.ModifiedAtUtc = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return payment;
    }
}
