namespace MoneyTalk.Core.Dtos;

public record ReportLine(Guid AccountId, string AccountCode, string AccountName, decimal Amount);

public class ProfitAndLossReport
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public List<ReportLine> IncomeLines { get; set; } = new();
    public decimal TotalIncome => IncomeLines.Sum(l => l.Amount);
    public List<ReportLine> CogsLines { get; set; } = new();
    public decimal TotalCogs => CogsLines.Sum(l => l.Amount);
    public decimal GrossProfit => TotalIncome - TotalCogs;
    public List<ReportLine> ExpenseLines { get; set; } = new();
    public decimal TotalExpenses => ExpenseLines.Sum(l => l.Amount);
    public decimal NetIncome => GrossProfit - TotalExpenses;
}

public class BalanceSheetReport
{
    public DateTime AsOf { get; set; }
    public List<ReportLine> Assets { get; set; } = new();
    public decimal TotalAssets => Assets.Sum(l => l.Amount);
    public List<ReportLine> Liabilities { get; set; } = new();
    public decimal TotalLiabilities => Liabilities.Sum(l => l.Amount);
    public List<ReportLine> Equity { get; set; } = new();
    public decimal TotalEquityExcludingRetainedEarnings => Equity.Sum(l => l.Amount);
    public decimal RetainedEarnings { get; set; }
    public decimal TotalEquity => TotalEquityExcludingRetainedEarnings + RetainedEarnings;
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public bool IsBalanced => Math.Round(TotalAssets - TotalLiabilitiesAndEquity, 2) == 0;
}

public record TrialBalanceLine(Guid AccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit);

public class TrialBalanceReport
{
    public DateTime AsOf { get; set; }
    public List<TrialBalanceLine> Lines { get; set; } = new();
    public decimal TotalDebits => Lines.Sum(l => l.Debit);
    public decimal TotalCredits => Lines.Sum(l => l.Credit);
}

public class CashFlowStatement
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public decimal BeginningCash { get; set; }
    public List<ReportLine> OperatingLines { get; set; } = new();
    public decimal OperatingCashFlow => OperatingLines.Sum(l => l.Amount);
    public List<ReportLine> InvestingLines { get; set; } = new();
    public decimal InvestingCashFlow => InvestingLines.Sum(l => l.Amount);
    public List<ReportLine> FinancingLines { get; set; } = new();
    public decimal FinancingCashFlow => FinancingLines.Sum(l => l.Amount);
    public decimal NetCashChange => OperatingCashFlow + InvestingCashFlow + FinancingCashFlow;
    public decimal EndingCash => BeginningCash + NetCashChange;
}

public class AgingBucketLine
{
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public decimal Current { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Over90 { get; set; }
    public decimal Total => Current + Days1To30 + Days31To60 + Days61To90 + Over90;
}

public class AgingReport
{
    public DateTime AsOf { get; set; }
    public List<AgingBucketLine> Lines { get; set; } = new();
    public decimal TotalCurrent => Lines.Sum(l => l.Current);
    public decimal Total1To30 => Lines.Sum(l => l.Days1To30);
    public decimal Total31To60 => Lines.Sum(l => l.Days31To60);
    public decimal Total61To90 => Lines.Sum(l => l.Days61To90);
    public decimal TotalOver90 => Lines.Sum(l => l.Over90);
    public decimal GrandTotal => Lines.Sum(l => l.Total);
}

public record CashFlowForecastPoint(DateTime Date, decimal ProjectedBalance, string? Note);

public class CashFlowForecastResult
{
    public List<CashFlowForecastPoint> Points { get; set; } = new();
    public decimal LowestProjectedBalance => Points.Count == 0 ? 0 : Points.Min(p => p.ProjectedBalance);
    public DateTime? LowestProjectedDate => Points.Count == 0 ? null : Points.OrderBy(p => p.ProjectedBalance).First().Date;
}

public class DashboardSummary
{
    public decimal TotalCash { get; set; }
    public decimal TotalAccountsReceivable { get; set; }
    public decimal TotalAccountsPayable { get; set; }
    public decimal MonthToDateIncome { get; set; }
    public decimal MonthToDateExpenses { get; set; }
    public decimal NetIncomeMonthToDate => MonthToDateIncome - MonthToDateExpenses;
    public int OverdueInvoiceCount { get; set; }
    public decimal OverdueInvoiceTotal { get; set; }
    public int OverdueBillCount { get; set; }
    public decimal OverdueBillTotal { get; set; }
    public List<(DateTime Date, decimal Balance)> CashTrend { get; set; } = new();
    public RepairShopKpis RepairShop { get; set; } = new();
}

public record DeviceRevenueLine(string Device, decimal Revenue, int TicketCount);

/// <summary>Repair-shop-specific KPIs layered onto the same Dashboard rather than a second
/// analytics page. "Idle" ticket age and warranty countdowns match NovaOps's own dashboard
/// thresholds (amber at 3+ days, red at 7+ days, warranty alert at 14 days out). A ticket counts
/// as idle unless its Status is "Completed" or "Delivered" — the two terminal statuses in the
/// default <c>Company.TicketStatuses</c> list; a shop that renames its terminal statuses should
/// keep one of these two names, or idle counts will over-count finished tickets.</summary>
public class RepairShopKpis
{
    public int TicketsIdleAmberCount { get; set; }
    public int TicketsIdleRedCount { get; set; }
    public int WarrantyExpiringSoonCount { get; set; }
    public decimal? AverageRepairTimeDays { get; set; }
    public List<DeviceRevenueLine> RevenueByDeviceLast30Days { get; set; } = new();
}

public class BudgetVsActualLine
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int Month { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance => ActualAmount - BudgetAmount;
    public decimal VariancePercent => BudgetAmount == 0 ? 0 : Math.Round(Variance / BudgetAmount * 100m, 1);
}
