using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class CompanySettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string? legalName;
    [ObservableProperty] private string? ein;
    [ObservableProperty] private string? address;
    [ObservableProperty] private string? city;
    [ObservableProperty] private string? state;
    [ObservableProperty] private string? postalCode;
    [ObservableProperty] private string? phone;
    [ObservableProperty] private string? email;
    [ObservableProperty] private string baseCurrency = "USD";
    [ObservableProperty] private int fiscalYearStartMonth = 1;
    [ObservableProperty] private decimal lowCashWarningThreshold = 5000m;
    [ObservableProperty] private string? statusMessage;

    public CompanySettingsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
        : base(unitOfWorkFactory, settingsService)
    {
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var company = await uow.Companies.GetByIdAsync(ActiveCompanyId)
                ?? throw new InvalidOperationException("Company profile not found.");

            Name = company.Name;
            LegalName = company.LegalName;
            Ein = company.Ein;
            Address = company.Address;
            City = company.City;
            State = company.State;
            PostalCode = company.PostalCode;
            Phone = company.Phone;
            Email = company.Email;
            BaseCurrency = company.BaseCurrency;
            FiscalYearStartMonth = company.FiscalYearStartMonth;
            LowCashWarningThreshold = company.LowCashWarningThreshold;
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var company = await uow.Companies.GetByIdAsync(ActiveCompanyId)
                ?? throw new InvalidOperationException("Company profile not found.");

            company.Name = Name.Trim();
            company.LegalName = LegalName;
            company.Ein = Ein;
            company.Address = Address;
            company.City = City;
            company.State = State;
            company.PostalCode = PostalCode;
            company.Phone = Phone;
            company.Email = Email;
            company.BaseCurrency = BaseCurrency;
            company.FiscalYearStartMonth = FiscalYearStartMonth;
            company.LowCashWarningThreshold = LowCashWarningThreshold;
            company.ModifiedAtUtc = DateTime.UtcNow;

            uow.Companies.Update(company);
            await uow.SaveChangesAsync();
            StatusMessage = "Company settings saved.";
        });
    }
}
