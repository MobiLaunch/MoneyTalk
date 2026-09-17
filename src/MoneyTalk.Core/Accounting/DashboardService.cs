using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Aggregates the numbers the Dashboard page and the AI advisor both need: cash
/// position, AR/AP totals, month-to-date P&amp;L, overdue counts, and a short cash trend.</summary>
public class DashboardService
{
    private readonly ReportingService _reporting = new();

    public async Task<DashboardSummary> GetSummaryAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct = default)
    {
        var accounts = await uow.Accounts.FindAsync(a => a.CompanyId == companyId, ct);
        var cash = accounts
            .Where(a => a.SubType is AccountSubType.Bank or AccountSubType.CreditCard)
            .Sum(a => a.SubType == AccountSubType.CreditCard ? -a.CurrentBalance : a.CurrentBalance);

        var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId, ct);
        var vendors = await uow.Vendors.FindAsync(v => v.CompanyId == companyId, ct);

        var monthStart = new DateTime(asOf.Year, asOf.Month, 1);
        var pnl = await _reporting.GetProfitAndLossAsync(uow, companyId, monthStart, asOf, ct);

        var overdueInvoices = await uow.Invoices.FindAsync(
            i => i.CompanyId == companyId && i.Balance > 0 && i.Status != InvoiceStatus.Void && i.DueDate < asOf, ct);
        var overdueBills = await uow.Bills.FindAsync(
            b => b.CompanyId == companyId && b.Balance > 0 && b.Status != BillStatus.Void && b.DueDate < asOf, ct);

        var summary = new DashboardSummary
        {
            TotalCash = cash,
            TotalAccountsReceivable = customers.Sum(c => c.Balance),
            TotalAccountsPayable = vendors.Sum(v => v.Balance),
            MonthToDateIncome = pnl.TotalIncome,
            MonthToDateExpenses = pnl.TotalCogs + pnl.TotalExpenses,
            OverdueInvoiceCount = overdueInvoices.Count,
            OverdueInvoiceTotal = overdueInvoices.Sum(i => i.Balance),
            OverdueBillCount = overdueBills.Count,
            OverdueBillTotal = overdueBills.Sum(b => b.Balance)
        };

        var forecast = await new CashFlowForecastService().ForecastAsync(uow, companyId, 30, ct);
        summary.CashTrend = forecast.Points.Select(p => (p.Date, p.ProjectedBalance)).ToList();

        summary.RepairShop = await GetRepairShopKpisAsync(uow, companyId, asOf, ct);

        return summary;
    }

    /// <summary>Ticket-age, warranty, and device-revenue KPIs — see the doc comment on
    /// <see cref="RepairShopKpis"/> for how "idle"/terminal status is determined.</summary>
    private static async Task<RepairShopKpis> GetRepairShopKpisAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct)
    {
        var tickets = await uow.RepairTickets.FindAsync(t => t.CompanyId == companyId, ct);
        var kpis = new RepairShopKpis();

        bool IsTerminal(RepairTicket t) => t.Status is "Completed" or "Delivered";

        foreach (var ticket in tickets.Where(t => !IsTerminal(t)))
        {
            var ageDays = (asOf.Date - ticket.CreatedAtUtc.Date).TotalDays;
            if (ageDays >= 7) kpis.TicketsIdleRedCount++;
            else if (ageDays >= 3) kpis.TicketsIdleAmberCount++;
        }

        kpis.WarrantyExpiringSoonCount = tickets.Count(t =>
            t.WarrantyEndDate.HasValue && t.WarrantyEndDate.Value >= asOf && (t.WarrantyEndDate.Value - asOf).TotalDays <= 14);

        var completedTickets = tickets.Where(IsTerminal).ToList();
        kpis.AverageRepairTimeDays = completedTickets.Count == 0
            ? null
            : Math.Round((decimal)completedTickets.Average(t => (t.ModifiedAtUtc - t.CreatedAtUtc).TotalDays), 1);

        var windowStart = asOf.AddDays(-30);
        kpis.RevenueByDeviceLast30Days = tickets
            .Where(t => t.CreatedAtUtc >= windowStart && t.AmountPaid > 0)
            .GroupBy(t => t.Device)
            .Select(g => new DeviceRevenueLine(g.Key, g.Sum(t => t.AmountPaid), g.Count()))
            .OrderByDescending(l => l.Revenue)
            .ToList();

        return kpis;
    }

    /// <summary>Cheap, deterministic rule-based insights computed locally (no AI call). These
    /// populate the dashboard instantly; the AI Assistant page can elaborate on any of them on
    /// request via Gemini.</summary>
    public async Task<List<AiInsight>> GenerateInsightsAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct = default)
    {
        var insights = new List<AiInsight>();
        var company = await uow.Companies.GetByIdAsync(companyId, ct);
        var summary = await GetSummaryAsync(uow, companyId, asOf, ct);

        if (company != null && summary.TotalCash < company.LowCashWarningThreshold)
        {
            insights.Add(new AiInsight
            {
                CompanyId = companyId,
                Title = "Cash balance is below your warning threshold",
                Detail = $"Total cash across bank accounts is {summary.TotalCash:C}, under your {company.LowCashWarningThreshold:C} threshold.",
                Severity = InsightSeverity.Warning
            });
        }

        var forecast = await new CashFlowForecastService().ForecastAsync(uow, companyId, 30, ct);
        if (forecast.LowestProjectedBalance < 0 && forecast.LowestProjectedDate.HasValue)
        {
            insights.Add(new AiInsight
            {
                CompanyId = companyId,
                Title = "Projected cash shortfall within 30 days",
                Detail = $"Based on open invoices, bills, and recurring transactions, cash is projected to go negative " +
                         $"({forecast.LowestProjectedBalance:C}) around {forecast.LowestProjectedDate.Value:MMM d}.",
                Severity = InsightSeverity.Critical
            });
        }

        if (summary.OverdueInvoiceCount > 0)
        {
            insights.Add(new AiInsight
            {
                CompanyId = companyId,
                Title = $"{summary.OverdueInvoiceCount} invoice(s) are past due",
                Detail = $"{summary.OverdueInvoiceTotal:C} in overdue customer invoices could be collected to improve cash flow.",
                Severity = InsightSeverity.Warning
            });
        }

        if (summary.OverdueBillCount > 0)
        {
            insights.Add(new AiInsight
            {
                CompanyId = companyId,
                Title = $"{summary.OverdueBillCount} bill(s) are past due",
                Detail = $"{summary.OverdueBillTotal:C} in vendor bills are overdue and may be accruing late fees or hurting vendor relationships.",
                Severity = InsightSeverity.Warning
            });
        }

        if (summary.NetIncomeMonthToDate < 0)
        {
            insights.Add(new AiInsight
            {
                CompanyId = companyId,
                Title = "Operating at a loss this month",
                Detail = $"Month-to-date expenses ({summary.MonthToDateExpenses:C}) exceed income ({summary.MonthToDateIncome:C}).",
                Severity = InsightSeverity.Info
            });
        }

        if (summary.RepairShop.TicketsIdleRedCount > 0)
        {
            insights.Add(new AiInsight
            {
                CompanyId = companyId,
                Title = $"{summary.RepairShop.TicketsIdleRedCount} ticket(s) have been idle 7+ days",
                Detail = "These open repair tickets haven't been touched in a week or more — worth a status check-in with the customer.",
                Severity = InsightSeverity.Warning
            });
        }

        if (summary.RepairShop.WarrantyExpiringSoonCount > 0)
        {
            insights.Add(new AiInsight
            {
                CompanyId = companyId,
                Title = $"{summary.RepairShop.WarrantyExpiringSoonCount} repair warranty(s) expiring within 14 days",
                Detail = "Consider a follow-up with these customers before their repair warranty lapses.",
                Severity = InsightSeverity.Info
            });
        }

        return insights;
    }
}
