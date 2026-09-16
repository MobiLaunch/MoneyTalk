using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Interfaces;
using MoneyTalk.Data.Seed;

namespace MoneyTalk.App.ViewModels;

public partial class OnboardingViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string companyName = string.Empty;

    public OnboardingViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task CreateCompanyAsync()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            ErrorMessage = "Enter a company name to get started.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var company = await DataSeeder.CreateCompanyWithDefaultsAsync(uow, CompanyName.Trim());

            var settings = SettingsService.Load();
            settings.ActiveCompanyId = company.Id;
            settings.HasCompletedOnboarding = true;
            SettingsService.Save(settings);

            _navigationService.NavigateTo(PageKeys.Dashboard);
        });
    }
}
