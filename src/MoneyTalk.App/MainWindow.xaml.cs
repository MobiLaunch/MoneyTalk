using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.Services;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Interfaces;
using Windows.System;

namespace MoneyTalk.App;

public sealed partial class MainWindow : Window
{
    // App-wide idle screen lock: a DispatcherTimer reset on any pointer/key activity anywhere in
    // the window (captured via AddHandler(..., handledEventsToo: true) so a control marking the
    // event Handled still counts as activity), matching NovaOps's hardcoded 3-minute lock delay.
    private DispatcherTimer? _idleTimer;
    private bool _isLocked;

    public MainWindow()
    {
        InitializeComponent();
        Title = "MoneyTalk — Financial & Business Management";
        SetUpIdleLock();
    }

    private void SetUpIdleLock()
    {
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
        _idleTimer.Tick += (_, _) => TryLock();
        _idleTimer.Start();

        RootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => ResetIdleTimer()), true);
        RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => ResetIdleTimer()), true);
    }

    private void ResetIdleTimer()
    {
        if (_isLocked) return;
        _idleTimer?.Stop();
        _idleTimer?.Start();
    }

    private void TryLock()
    {
        if (_isLocked) return;

        var settingsService = App.Services.GetRequiredService<LocalSettingsService>();
        var secureTokenStore = App.Services.GetRequiredService<ISecureTokenStore>();
        if (!settingsService.Load().ScreenLockEnabled) return;
        if (string.IsNullOrEmpty(secureTokenStore.GetSecret(SecretKeys.ScreenLockPin))) return;

        _isLocked = true;
        _idleTimer?.Stop();
        LockErrorText.Visibility = Visibility.Collapsed;
        LockPinBox.Password = string.Empty;
        LockOverlay.Visibility = Visibility.Visible;
        RootNavigationView.IsEnabled = false;
        LockPinBox.Focus(FocusState.Programmatic);
    }

    private void TryUnlock()
    {
        var secureTokenStore = App.Services.GetRequiredService<ISecureTokenStore>();
        var storedPin = secureTokenStore.GetSecret(SecretKeys.ScreenLockPin);

        if (!string.IsNullOrEmpty(storedPin) && LockPinBox.Password == storedPin)
        {
            _isLocked = false;
            LockOverlay.Visibility = Visibility.Collapsed;
            RootNavigationView.IsEnabled = true;
            LockPinBox.Password = string.Empty;
            _idleTimer?.Start();
        }
        else
        {
            LockErrorText.Visibility = Visibility.Visible;
            LockPinBox.Password = string.Empty;
        }
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e) => TryUnlock();

    private void LockPinBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) TryUnlock();
    }

    private async void CommandPaletteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (_isLocked) return;
        await ShowCommandPaletteAsync();
    }

    private async Task ShowCommandPaletteAsync()
    {
        var dialog = new CommandPaletteDialog(SearchAsync) { XamlRoot = RootGrid.XamlRoot };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.SelectedResult is { } hit)
            App.Services.GetRequiredService<INavigationService>().NavigateTo(hit.PageKey, hit.NavigationParameter);
    }

    /// <summary>Backs the Ctrl+K command palette — searches customers/items/tickets by name/SKU/
    /// ticket number/device, five results per category. Customers and items land on their list
    /// page (neither has a dedicated single-record edit page); tickets deep-link straight to the
    /// matched ticket via <see cref="TicketEditNavigationArgs"/>.</summary>
    private static async Task<List<CommandPaletteResult>> SearchAsync(string query)
    {
        var results = new List<CommandPaletteResult>();

        var settingsService = App.Services.GetRequiredService<LocalSettingsService>();
        var companyId = settingsService.Load().ActiveCompanyId;
        if (companyId == null) return results;

        var term = query.Trim();
        using var uow = App.Services.GetRequiredService<Func<IUnitOfWork>>()();

        var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId && c.Name.Contains(term));
        foreach (var customer in customers.Take(5))
            results.Add(new CommandPaletteResult { Category = "Customer", Title = customer.Name, Subtitle = customer.Email ?? string.Empty, PageKey = PageKeys.Customers });

        var items = await uow.Items.FindAsync(i => i.CompanyId == companyId && (i.Name.Contains(term) || i.Sku.Contains(term)));
        foreach (var item in items.Take(5))
            results.Add(new CommandPaletteResult { Category = "Item", Title = item.Name, Subtitle = item.Sku, PageKey = PageKeys.Items });

        var tickets = await uow.RepairTickets.FindAsync(t => t.CompanyId == companyId && (t.TicketNumber.Contains(term) || t.Device.Contains(term)));
        foreach (var ticket in tickets.Take(5))
            results.Add(new CommandPaletteResult { Category = "Ticket", Title = ticket.TicketNumber, Subtitle = ticket.Device, PageKey = PageKeys.TicketEdit, NavigationParameter = new TicketEditNavigationArgs(ticket.Id) });

        return results;
    }

    private async void RootNavigationView_Loaded(object sender, RoutedEventArgs e)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.Initialize(ContentFrame);

        var unitOfWorkFactory = App.Services.GetRequiredService<Func<IUnitOfWork>>();
        var settingsService = App.Services.GetRequiredService<LocalSettingsService>();

        bool hasAnyCompany;
        using (var uow = unitOfWorkFactory())
        {
            var companies = await uow.Companies.GetAllAsync();
            hasAnyCompany = companies.Count > 0;

            // A stale pointer needs repairing just as much as a missing one: replacing or deleting
            // the database leaves settings.json naming a company that no longer exists, and because
            // startup used to treat "id is set" as "id is valid" it skipped onboarding and every
            // page then failed with "Company profile not found".
            var settings = settingsService.Load();
            var activeCompanyExists = settings.ActiveCompanyId.HasValue
                && companies.Any(c => c.Id == settings.ActiveCompanyId.Value);
            if (!activeCompanyExists)
            {
                settings.ActiveCompanyId = hasAnyCompany ? companies[0].Id : null;
                settingsService.Save(settings);
            }
        }

        if (!hasAnyCompany)
        {
            RootNavigationView.SelectedItem = null;
            navigationService.NavigateTo(PageKeys.Onboarding);
        }
        else
        {
            // Selecting the item is what navigates: SelectionChanged fires and calls NavigateTo with
            // the item's Tag. Navigating explicitly as well would build and load Dashboard twice.
            RootNavigationView.SelectedItem = RootNavigationView.MenuItems[0];
        }
    }

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item && item.Tag is string pageKey)
        {
            App.Services.GetRequiredService<INavigationService>().NavigateTo(pageKey);
        }
    }

    private void RootNavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        App.Services.GetRequiredService<INavigationService>().GoBack();
    }
}
