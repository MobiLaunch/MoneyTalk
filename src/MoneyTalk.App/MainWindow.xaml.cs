using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MoneyTalk.App.Services;
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
            if (hasAnyCompany && settingsService.Load().ActiveCompanyId == null)
            {
                var settings = settingsService.Load();
                settings.ActiveCompanyId = companies[0].Id;
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
            RootNavigationView.SelectedItem = RootNavigationView.MenuItems[0];
            navigationService.NavigateTo(PageKeys.Dashboard);
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
