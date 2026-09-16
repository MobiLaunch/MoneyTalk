using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Dtos;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly ReportingService _reportingService;

    [ObservableProperty] private DateTimeOffset rangeStart = new(new DateTime(DateTime.UtcNow.Year, 1, 1));
    [ObservableProperty] private DateTimeOffset rangeEnd = DateTimeOffset.Now;

    public ObservableCollection<ReportLine> IncomeLines { get; } = new();
    public ObservableCollection<ReportLine> CogsLines { get; } = new();
    public ObservableCollection<ReportLine> ExpenseLines { get; } = new();
    [ObservableProperty] private string totalIncomeDisplay = "$0.00";
    [ObservableProperty] private string grossProfitDisplay = "$0.00";
    [ObservableProperty] private string netIncomeDisplay = "$0.00";

    public ObservableCollection<ReportLine> AssetLines { get; } = new();
    public ObservableCollection<ReportLine> LiabilityLines { get; } = new();
    public ObservableCollection<ReportLine> EquityLines { get; } = new();
    [ObservableProperty] private string totalAssetsDisplay = "$0.00";
    [ObservableProperty] private string totalLiabilitiesDisplay = "$0.00";
    [ObservableProperty] private string totalEquityDisplay = "$0.00";
    [ObservableProperty] private string retainedEarningsDisplay = "$0.00";

    public ObservableCollection<TrialBalanceLine> TrialBalanceLines { get; } = new();
    [ObservableProperty] private string trialBalanceDebitsDisplay = "$0.00";
    [ObservableProperty] private string trialBalanceCreditsDisplay = "$0.00";

    public ObservableCollection<ReportLine> OperatingCashFlowLines { get; } = new();
    public ObservableCollection<ReportLine> InvestingCashFlowLines { get; } = new();
    public ObservableCollection<ReportLine> FinancingCashFlowLines { get; } = new();
    [ObservableProperty] private string netCashChangeDisplay = "$0.00";
    [ObservableProperty] private string endingCashDisplay = "$0.00";

    public ObservableCollection<AgingBucketLine> ArAgingLines { get; } = new();
    public ObservableCollection<AgingBucketLine> ApAgingLines { get; } = new();

    public ReportsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, ReportingService reportingService)
        : base(unitOfWorkFactory, settingsService)
    {
        _reportingService = reportingService;
    }

    [RelayCommand]
    public async Task RunReportsAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;
            var start = RangeStart.DateTime;
            var end = RangeEnd.DateTime;

            var pnl = await _reportingService.GetProfitAndLossAsync(uow, companyId, start, end);
            ReplaceAll(IncomeLines, pnl.IncomeLines);
            ReplaceAll(CogsLines, pnl.CogsLines);
            ReplaceAll(ExpenseLines, pnl.ExpenseLines);
            TotalIncomeDisplay = pnl.TotalIncome.ToString("C2");
            GrossProfitDisplay = pnl.GrossProfit.ToString("C2");
            NetIncomeDisplay = pnl.NetIncome.ToString("C2");

            var balanceSheet = await _reportingService.GetBalanceSheetAsync(uow, companyId, end);
            ReplaceAll(AssetLines, balanceSheet.Assets);
            ReplaceAll(LiabilityLines, balanceSheet.Liabilities);
            ReplaceAll(EquityLines, balanceSheet.Equity);
            TotalAssetsDisplay = balanceSheet.TotalAssets.ToString("C2");
            TotalLiabilitiesDisplay = balanceSheet.TotalLiabilities.ToString("C2");
            TotalEquityDisplay = balanceSheet.TotalEquity.ToString("C2");
            RetainedEarningsDisplay = balanceSheet.RetainedEarnings.ToString("C2");

            var trialBalance = await _reportingService.GetTrialBalanceAsync(uow, companyId, end);
            ReplaceAll(TrialBalanceLines, trialBalance.Lines);
            TrialBalanceDebitsDisplay = trialBalance.TotalDebits.ToString("C2");
            TrialBalanceCreditsDisplay = trialBalance.TotalCredits.ToString("C2");

            var cashFlow = await _reportingService.GetCashFlowStatementAsync(uow, companyId, start, end);
            ReplaceAll(OperatingCashFlowLines, cashFlow.OperatingLines);
            ReplaceAll(InvestingCashFlowLines, cashFlow.InvestingLines);
            ReplaceAll(FinancingCashFlowLines, cashFlow.FinancingLines);
            NetCashChangeDisplay = cashFlow.NetCashChange.ToString("C2");
            EndingCashDisplay = cashFlow.EndingCash.ToString("C2");

            var arAging = await _reportingService.GetAccountsReceivableAgingAsync(uow, companyId, end);
            ReplaceAll(ArAgingLines, arAging.Lines);

            var apAging = await _reportingService.GetAccountsPayableAgingAsync(uow, companyId, end);
            ReplaceAll(ApAgingLines, apAging.Lines);
        });
    }

    private static void ReplaceAll<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}
