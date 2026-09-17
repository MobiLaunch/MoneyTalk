using Microsoft.UI.Xaml.Controls;
using MoneyTalk.App.Views.Pages;

namespace MoneyTalk.App.Services;

/// <summary>Thin wrapper over a WinUI <see cref="Frame"/> so ViewModels can trigger navigation
/// by a string key instead of taking a hard dependency on page types (which would pull XAML/UI
/// types into what should be a testable ViewModel layer).</summary>
public class NavigationService : INavigationService
{
    private static readonly Dictionary<string, Type> PageRegistry = new()
    {
        [PageKeys.Dashboard] = typeof(DashboardPage),
        [PageKeys.ChartOfAccounts] = typeof(ChartOfAccountsPage),
        [PageKeys.Journal] = typeof(JournalPage),
        [PageKeys.Customers] = typeof(CustomersPage),
        [PageKeys.Invoices] = typeof(InvoicesPage),
        [PageKeys.InvoiceEdit] = typeof(InvoiceEditPage),
        [PageKeys.Tickets] = typeof(TicketsPage),
        [PageKeys.TicketEdit] = typeof(TicketEditPage),
        [PageKeys.Pos] = typeof(PosPage),
        [PageKeys.Calendar] = typeof(CalendarPage),
        [PageKeys.VendorRepairs] = typeof(VendorRepairsPage),
        [PageKeys.TradeIns] = typeof(TradeInsPage),
        [PageKeys.TradeInEdit] = typeof(TradeInEditPage),
        [PageKeys.Vendors] = typeof(VendorsPage),
        [PageKeys.Bills] = typeof(BillsPage),
        [PageKeys.BillEdit] = typeof(BillEditPage),
        [PageKeys.Items] = typeof(ItemsPage),
        [PageKeys.BankAccounts] = typeof(BankAccountsPage),
        [PageKeys.Reconciliation] = typeof(ReconciliationPage),
        [PageKeys.Budgets] = typeof(BudgetsPage),
        [PageKeys.RecurringTransactions] = typeof(RecurringTransactionsPage),
        [PageKeys.Reports] = typeof(ReportsPage),
        [PageKeys.AiAssistant] = typeof(AiAssistantPage),
        [PageKeys.Integrations] = typeof(IntegrationsSettingsPage),
        [PageKeys.CompanySettings] = typeof(CompanySettingsPage),
        [PageKeys.Users] = typeof(UsersPage),
        [PageKeys.Onboarding] = typeof(OnboardingPage),
    };

    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public bool NavigateTo(string pageKey, object? parameter = null)
    {
        if (_frame == null) return false;
        if (!PageRegistry.TryGetValue(pageKey, out var pageType)) return false;
        if (_frame.Content?.GetType() == pageType && parameter == null) return true;
        return _frame.Navigate(pageType, parameter);
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public bool GoBack()
    {
        if (_frame == null || !_frame.CanGoBack) return false;
        _frame.GoBack();
        return true;
    }
}

/// <summary>String keys for every page, referenced from the NavigationView's items and from
/// ViewModels that navigate programmatically (e.g. "Edit" buttons in a list page).</summary>
public static class PageKeys
{
    public const string Dashboard = "Dashboard";
    public const string ChartOfAccounts = "ChartOfAccounts";
    public const string Journal = "Journal";
    public const string Customers = "Customers";
    public const string Invoices = "Invoices";
    public const string InvoiceEdit = "InvoiceEdit";
    public const string Tickets = "Tickets";
    public const string TicketEdit = "TicketEdit";
    public const string Pos = "Pos";
    public const string Calendar = "Calendar";
    public const string VendorRepairs = "VendorRepairs";
    public const string TradeIns = "TradeIns";
    public const string TradeInEdit = "TradeInEdit";
    public const string Vendors = "Vendors";
    public const string Bills = "Bills";
    public const string BillEdit = "BillEdit";
    public const string Items = "Items";
    public const string BankAccounts = "BankAccounts";
    public const string Reconciliation = "Reconciliation";
    public const string Budgets = "Budgets";
    public const string RecurringTransactions = "RecurringTransactions";
    public const string Reports = "Reports";
    public const string AiAssistant = "AiAssistant";
    public const string Integrations = "Integrations";
    public const string CompanySettings = "CompanySettings";
    public const string Users = "Users";
    public const string Onboarding = "Onboarding";
}
