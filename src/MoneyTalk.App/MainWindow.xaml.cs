using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "MoneyTalk — Financial & Business Management";
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
