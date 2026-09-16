using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _dashboardService;
    private readonly InvoiceService _invoiceService;
    private readonly RecurringTransactionService _recurringTransactionService;
    private readonly INavigationService _navigationService;

    [ObservableProperty] private string companyName = string.Empty;
    [ObservableProperty] private string totalCashDisplay = "$0.00";
    [ObservableProperty] private string totalArDisplay = "$0.00";
    [ObservableProperty] private string totalApDisplay = "$0.00";
    [ObservableProperty] private string netIncomeDisplay = "$0.00";
    [ObservableProperty] private string monthToDateIncomeDisplay = "$0.00";
    [ObservableProperty] private string monthToDateExpensesDisplay = "$0.00";
    [ObservableProperty] private string overdueInvoicesSubText = "No overdue invoices";
    [ObservableProperty] private string overdueBillsSubText = "No overdue bills";
    [ObservableProperty] private IReadOnlyList<double> cashTrendValues = Array.Empty<double>();

    public ObservableCollection<AiInsight> Insights { get; } = new();

    public DashboardViewModel(
        Func<IUnitOfWork> unitOfWorkFactory,
        LocalSettingsService settingsService,
        DashboardService dashboardService,
        InvoiceService invoiceService,
        RecurringTransactionService recurringTransactionService,
        INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _dashboardService = dashboardService;
        _invoiceService = invoiceService;
        _recurringTransactionService = recurringTransactionService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;
            var company = await uow.Companies.GetByIdAsync(companyId);
            CompanyName = company?.Name ?? "Your company";

            var today = DateTime.UtcNow.Date;

            // Keep the books current before showing numbers: flag newly-overdue invoices and
            // fire any recurring invoices/bills/journal entries that came due.
            await _invoiceService.RefreshOverdueStatusesAsync(uow, companyId, today);
            await _recurringTransactionService.ProcessDueTransactionsAsync(uow, companyId, today);

            var summary = await _dashboardService.GetSummaryAsync(uow, companyId, today);

            TotalCashDisplay = summary.TotalCash.ToString("C2");
            TotalArDisplay = summary.TotalAccountsReceivable.ToString("C2");
            TotalApDisplay = summary.TotalAccountsPayable.ToString("C2");
            NetIncomeDisplay = summary.NetIncomeMonthToDate.ToString("C2");
            MonthToDateIncomeDisplay = summary.MonthToDateIncome.ToString("C2");
            MonthToDateExpensesDisplay = summary.MonthToDateExpenses.ToString("C2");

            OverdueInvoicesSubText = summary.OverdueInvoiceCount == 0
                ? "No overdue invoices"
                : $"{summary.OverdueInvoiceCount} overdue · {summary.OverdueInvoiceTotal:C2}";
            OverdueBillsSubText = summary.OverdueBillCount == 0
                ? "No overdue bills"
                : $"{summary.OverdueBillCount} overdue · {summary.OverdueBillTotal:C2}";

            CashTrendValues = summary.CashTrend.Select(p => (double)p.Balance).ToList();

            var insights = await _dashboardService.GenerateInsightsAsync(uow, companyId, today);
            Insights.Clear();
            foreach (var insight in insights.OrderByDescending(i => i.Severity))
                Insights.Add(insight);
        });
    }

    [RelayCommand]
    private void GoToInvoices() => _navigationService.NavigateTo(PageKeys.Invoices);

    [RelayCommand]
    private void GoToBills() => _navigationService.NavigateTo(PageKeys.Bills);

    [RelayCommand]
    private void GoToAiAssistant() => _navigationService.NavigateTo(PageKeys.AiAssistant);
}
