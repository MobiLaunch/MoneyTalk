using System.Text;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Core.Accounting;

/// <summary>Builds the grounding text handed to Gemini as a system instruction before every
/// financial-advisor chat turn. Everything here is computed locally from the ledger — the AI
/// only ever sees a compact summary, never raw customer/vendor PII beyond names and balances
/// already needed to answer the question, and never any integration credentials.</summary>
public class FinancialContextBuilder
{
    private readonly DashboardService _dashboardService = new();
    private readonly ReportingService _reportingService = new();

    public async Task<string> BuildSystemInstructionAsync(IUnitOfWork uow, Guid companyId, DateTime asOf, CancellationToken ct = default)
    {
        var company = await uow.Companies.GetByIdAsync(companyId, ct);
        var summary = await _dashboardService.GetSummaryAsync(uow, companyId, asOf, ct);
        var monthStart = new DateTime(asOf.Year, asOf.Month, 1);
        var pnl = await _reportingService.GetProfitAndLossAsync(uow, companyId, monthStart, asOf, ct);
        var arAging = await _reportingService.GetAccountsReceivableAgingAsync(uow, companyId, asOf, ct);
        var apAging = await _reportingService.GetAccountsPayableAgingAsync(uow, companyId, asOf, ct);
        var forecast = await new CashFlowForecastService().ForecastAsync(uow, companyId, 60, ct);

        var sb = new StringBuilder();
        sb.AppendLine("You are MoneyTalk's in-app financial advisor for a small/medium business. " +
                      "Answer using ONLY the figures below plus the user's question — do not invent numbers. " +
                      "Be concise, concrete, and actionable. Flag risks plainly. You are not a substitute for a CPA or tax attorney; " +
                      "say so when a question calls for licensed tax/legal advice.");
        sb.AppendLine();
        sb.AppendLine($"Company: {company?.Name ?? "Unknown"} (base currency {company?.BaseCurrency ?? "USD"})");
        sb.AppendLine($"As of: {asOf:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("== Cash position ==");
        sb.AppendLine($"Total cash across bank/credit accounts: {summary.TotalCash:C}");
        sb.AppendLine($"60-day cash forecast low point: {forecast.LowestProjectedBalance:C}" +
                       (forecast.LowestProjectedDate.HasValue ? $" around {forecast.LowestProjectedDate:yyyy-MM-dd}" : ""));
        sb.AppendLine();
        sb.AppendLine("== Month-to-date P&L ==");
        sb.AppendLine($"Income: {pnl.TotalIncome:C}, COGS: {pnl.TotalCogs:C}, Gross profit: {pnl.GrossProfit:C}");
        sb.AppendLine($"Operating expenses: {pnl.TotalExpenses:C}, Net income: {pnl.NetIncome:C}");
        sb.AppendLine();
        sb.AppendLine("== Accounts receivable ==");
        sb.AppendLine($"Total AR: {summary.TotalAccountsReceivable:C}, overdue: {summary.OverdueInvoiceTotal:C} across {summary.OverdueInvoiceCount} invoice(s)");
        sb.AppendLine($"Aging — current: {arAging.TotalCurrent:C}, 1-30: {arAging.Total1To30:C}, 31-60: {arAging.Total31To60:C}, 61-90: {arAging.Total61To90:C}, 90+: {arAging.TotalOver90:C}");
        sb.AppendLine();
        sb.AppendLine("== Accounts payable ==");
        sb.AppendLine($"Total AP: {summary.TotalAccountsPayable:C}, overdue: {summary.OverdueBillTotal:C} across {summary.OverdueBillCount} bill(s)");
        sb.AppendLine($"Aging — current: {apAging.TotalCurrent:C}, 1-30: {apAging.Total1To30:C}, 31-60: {apAging.Total31To60:C}, 61-90: {apAging.Total61To90:C}, 90+: {apAging.TotalOver90:C}");

        return sb.ToString();
    }
}
