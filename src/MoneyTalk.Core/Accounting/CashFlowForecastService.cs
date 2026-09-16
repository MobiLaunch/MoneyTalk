using System.Text.Json;
using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Projects the combined cash position forward by walking known, dated near-term
/// events — open invoice due dates (expected collections), open bill due dates (expected
/// payments), and due recurring transactions — rather than a statistical trend line. Simple,
/// explainable, and good enough to warn "you'll be short on the 14th" a business owner can
/// actually act on.</summary>
public class CashFlowForecastService
{
    public async Task<CashFlowForecastResult> ForecastAsync(
        IUnitOfWork uow, Guid companyId, int horizonDays = 90, CancellationToken ct = default)
    {
        var accounts = await uow.Accounts.FindAsync(
            a => a.CompanyId == companyId && (a.SubType == AccountSubType.Bank || a.SubType == AccountSubType.CreditCard), ct);
        var startingCash = accounts.Sum(a => a.SubType == AccountSubType.CreditCard ? -a.CurrentBalance : a.CurrentBalance);

        var today = DateTime.UtcNow.Date;
        var horizonEnd = today.AddDays(horizonDays);

        var events = new List<(DateTime Date, decimal Amount, string Note)>();

        var openInvoices = await uow.Invoices.FindAsync(
            i => i.CompanyId == companyId && i.Balance > 0 && i.Status != InvoiceStatus.Void && i.Status != InvoiceStatus.Draft, ct);
        foreach (var invoice in openInvoices.Where(i => i.DueDate <= horizonEnd))
            events.Add((invoice.DueDate < today ? today : invoice.DueDate, invoice.Balance, $"Expected payment: invoice {invoice.InvoiceNumber}"));

        var openBills = await uow.Bills.FindAsync(
            b => b.CompanyId == companyId && b.Balance > 0 && b.Status != BillStatus.Void, ct);
        foreach (var bill in openBills.Where(b => b.DueDate <= horizonEnd))
            events.Add((bill.DueDate < today ? today : bill.DueDate, -bill.Balance, $"Bill due: {bill.BillNumber}"));

        var recurring = await uow.RecurringTransactions.FindAsync(r => r.CompanyId == companyId && r.IsActive, ct);
        foreach (var template in recurring)
        {
            var runDate = template.NextRunDate;
            var estimate = EstimateRecurringCashImpact(template);
            while (runDate <= horizonEnd && (template.EndDate == null || runDate <= template.EndDate))
            {
                if (runDate >= today)
                    events.Add((runDate, estimate, $"Recurring: {template.Name}"));
                runDate = RecurringTransaction.ComputeNextRunDate(runDate, template.Frequency);
            }
        }

        var result = new CashFlowForecastResult();
        var runningBalance = startingCash;
        result.Points.Add(new CashFlowForecastPoint(today, runningBalance, "Today"));

        foreach (var dayEvents in events.GroupBy(e => e.Date).OrderBy(g => g.Key))
        {
            runningBalance += dayEvents.Sum(e => e.Amount);
            var notes = string.Join("; ", dayEvents.Select(e => e.Note));
            result.Points.Add(new CashFlowForecastPoint(dayEvents.Key, runningBalance, notes));
        }

        return result;
    }

    private static decimal EstimateRecurringCashImpact(RecurringTransaction template)
    {
        try
        {
            switch (template.TemplateType)
            {
                case RecurringTemplateType.Invoice:
                    var invoiceTemplate = JsonSerializer.Deserialize<RecurringInvoiceTemplate>(template.TemplatePayloadJson);
                    return invoiceTemplate?.Lines.Sum(l => l.Quantity * l.UnitPrice) ?? 0m;
                case RecurringTemplateType.Bill:
                    var billTemplate = JsonSerializer.Deserialize<RecurringBillTemplate>(template.TemplatePayloadJson);
                    return -(billTemplate?.Lines.Sum(l => l.Quantity * l.UnitPrice) ?? 0m);
                default:
                    return 0m;
            }
        }
        catch (JsonException)
        {
            return 0m;
        }
    }
}
