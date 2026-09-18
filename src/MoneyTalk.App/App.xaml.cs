using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using MoneyTalk.App.Services;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Interfaces;
using MoneyTalk.Core.Interfaces.Integrations;
using MoneyTalk.Data;
using MoneyTalk.Data.Repositories;
using MoneyTalk.Integrations.Gemini;
using MoneyTalk.Integrations.QuickBooks;
using MoneyTalk.Integrations.Square;

namespace MoneyTalk.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Exposed for WinRT interop calls that need an HWND (file/folder pickers) — see
    /// <c>WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow)</c>.</summary>
    public static Window MainWindow { get; private set; } = null!;

    private readonly IHost _host;
    private Window? _window;

    public App()
    {
        InitializeComponent();

        // Without these, a startup failure in an unpackaged WinExe app is invisible: there's no
        // console to print to, and if Windows Error Reporting is off on the machine the process
        // just flashes and vanishes with nothing on screen. Every path below gets a message box
        // with the real exception instead of a silent exit.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowFatalErrorAndExit("Unhandled exception", e.ExceptionObject as Exception);
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            ShowFatalErrorAndExit("Unhandled UI exception", e.Exception);
        };

        try
        {
            AppPaths.EnsureFoldersExist();

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
            Services = _host.Services;
        }
        catch (Exception ex)
        {
            ShowFatalErrorAndExit("Startup failed while configuring services", ex);
            throw;
        }
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var settingsService = new LocalSettingsService();
        var settings = settingsService.Load();
        services.AddSingleton(settingsService);

        // Constructed directly (not resolved from the container) so the client secrets below
        // can be loaded before the rest of the service graph is built.
        var secureTokenStore = new DpapiSecureTokenStore();

        services.AddDbContext<MoneyTalkDbContext>(
            options => options.UseSqlite($"Data Source={AppPaths.DatabaseFilePath}"),
            contextLifetime: ServiceLifetime.Transient,
            optionsLifetime: ServiceLifetime.Singleton);

        // Every unit-of-work gets its own DbContext instance; ViewModels resolve a fresh one per
        // operation (or hold one for the duration of an edit session) via this factory rather
        // than sharing a single app-wide DbContext.
        //
        // The context is newed up from the (singleton) options instead of being resolved, because
        // the container keeps a reference to every IDisposable transient it hands out so it can
        // dispose them with the provider. ViewModels resolve from the root provider, which only
        // goes away at shutdown, so resolving the context would pile up one leaked DbContext per
        // operation for the life of the app even though callers dispose their unit of work.
        services.AddTransient<IUnitOfWork>(sp => new EfUnitOfWork(sp.GetRequiredService<MoneyTalkDbContext>()));
        services.AddTransient<Func<IUnitOfWork>>(sp => () =>
            new EfUnitOfWork(new MoneyTalkDbContext(sp.GetRequiredService<DbContextOptions<MoneyTalkDbContext>>())));

        services.AddSingleton<ISecureTokenStore>(secureTokenStore);
        services.AddSingleton<INavigationService, NavigationService>();

        // Core accounting engine — stateless, safe as singletons; every method takes the
        // IUnitOfWork it should operate against as a parameter.
        services.AddSingleton<LedgerService>();
        services.AddSingleton<InvoiceService>();
        services.AddSingleton<RepairTicketService>();
        services.AddSingleton<PosService>();
        services.AddSingleton<TradeInPriceLookupService>(sp => new TradeInPriceLookupService(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<IGeminiClient>()));
        services.AddSingleton<IFixitClient>(sp => new IFixitClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        services.AddSingleton<BillService>();
        services.AddSingleton<ReconciliationService>();
        services.AddSingleton<ReportingService>();
        services.AddSingleton<BudgetService>();
        services.AddSingleton<RecurringTransactionService>();
        services.AddSingleton<CashFlowForecastService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<FinancialContextBuilder>();
        services.AddSingleton<FinancialAdvisorService>();

        // Integration clients.
        services.AddHttpClient();
        services.AddSingleton(new SquareOptions
        {
            ClientId = settings.SquareClientId,
            ClientSecret = secureTokenStore.GetSecret(SecretKeys.SquareClientSecret) ?? string.Empty,
            RedirectUri = settings.SquareRedirectUri,
            ApiBaseUrl = settings.SquareUseSandbox ? "https://connect.squareupsandbox.com" : "https://connect.squareup.com"
        });
        services.AddSingleton(new QuickBooksOptions
        {
            ClientId = settings.QuickBooksClientId,
            ClientSecret = secureTokenStore.GetSecret(SecretKeys.QuickBooksClientSecret) ?? string.Empty,
            RedirectUri = settings.QuickBooksRedirectUri,
            UseSandbox = settings.QuickBooksUseSandbox
        });
        services.AddSingleton(new GeminiOptions { ModelId = settings.GeminiModelId });
        services.AddSingleton(new EmailOptions
        {
            Host = settings.EmailHost,
            Port = settings.EmailPort,
            Username = settings.EmailUsername,
            Password = secureTokenStore.GetSecret(SecretKeys.EmailPassword) ?? string.Empty,
            UseSsl = settings.EmailUseSsl,
            FromAddress = settings.EmailFromAddress,
            FromName = settings.EmailFromName
        });
        services.AddSingleton<EmailService>();

        services.AddSingleton<ISquareClient>(sp => new SquareClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<SquareOptions>()));
        services.AddSingleton<IQuickBooksClient>(sp => new QuickBooksClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<QuickBooksOptions>()));
        services.AddSingleton<IGeminiClient>(sp => new GeminiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<GeminiOptions>()));

        // ViewModels — transient so each page navigation gets a fresh instance.
        services.AddTransient<OnboardingViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ChartOfAccountsViewModel>();
        services.AddTransient<JournalViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<InvoicesViewModel>();
        services.AddTransient<InvoiceEditViewModel>();
        services.AddTransient<TicketsViewModel>();
        services.AddTransient<TicketEditViewModel>();
        services.AddTransient<PosViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<VendorRepairsViewModel>();
        services.AddTransient<TradeInsViewModel>();
        services.AddTransient<TradeInEditViewModel>();
        services.AddTransient<VendorsViewModel>();
        services.AddTransient<BillsViewModel>();
        services.AddTransient<BillEditViewModel>();
        services.AddTransient<ItemsViewModel>();
        services.AddTransient<BankAccountsViewModel>();
        services.AddTransient<ReconciliationViewModel>();
        services.AddTransient<BudgetsViewModel>();
        services.AddTransient<RecurringTransactionsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<AiAssistantViewModel>();
        services.AddTransient<IntegrationsSettingsViewModel>();
        services.AddTransient<CompanySettingsViewModel>();
        services.AddTransient<UsersViewModel>();
        services.AddTransient<LabelEditorViewModel>();
        services.AddTransient<ReceiptEditorViewModel>();
        services.AddTransient<DeviceTriageViewModel>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            using (var scopeContext = Services.GetRequiredService<MoneyTalkDbContext>())
            {
                scopeContext.Database.EnsureCreated();
            }

            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();
        }
        catch (Exception ex)
        {
            ShowFatalErrorAndExit("Startup failed while launching the main window", ex);
        }
    }

    private static void ShowFatalErrorAndExit(string title, Exception? ex)
    {
        var message = ex?.ToString() ?? "An unknown fatal error occurred (no exception details were captured).";
        MessageBoxW(IntPtr.Zero, message, $"MoneyTalk — {title}", 0x00000010 /* MB_ICONERROR */);
        Environment.Exit(1);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
