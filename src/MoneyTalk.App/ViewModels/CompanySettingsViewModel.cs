using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class CompanySettingsViewModel : ViewModelBase
{
    private const string SystemDefaultPrinterOption = "(System default)";

    private readonly ISecureTokenStore _secureTokenStore;

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

    public ObservableCollection<string> ReceiptPrinterOptions { get; } = new();
    [ObservableProperty] private string selectedReceiptPrinter = SystemDefaultPrinterOption;

    [ObservableProperty] private bool screenLockEnabled;
    [ObservableProperty] private bool hasScreenLockPin;
    [ObservableProperty] private string newPinInput = string.Empty;
    [ObservableProperty] private string confirmPinInput = string.Empty;
    [ObservableProperty] private string? pinStatusMessage;

    public CompanySettingsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, ISecureTokenStore secureTokenStore)
        : base(unitOfWorkFactory, settingsService)
    {
        _secureTokenStore = secureTokenStore;
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

            var settings = SettingsService.Load();
            ReceiptPrinterOptions.Clear();
            ReceiptPrinterOptions.Add(SystemDefaultPrinterOption);
            foreach (var printerName in PrintService.GetInstalledPrinterNames())
                ReceiptPrinterOptions.Add(printerName);
            SelectedReceiptPrinter = string.IsNullOrEmpty(settings.ReceiptPrinterName) || !ReceiptPrinterOptions.Contains(settings.ReceiptPrinterName)
                ? SystemDefaultPrinterOption
                : settings.ReceiptPrinterName;

            ScreenLockEnabled = settings.ScreenLockEnabled;
            HasScreenLockPin = !string.IsNullOrEmpty(_secureTokenStore.GetSecret(SecretKeys.ScreenLockPin));
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

            var lockRequestedWithoutPin = ScreenLockEnabled && !HasScreenLockPin;
            if (lockRequestedWithoutPin) ScreenLockEnabled = false;

            var settings = SettingsService.Load();
            settings.ReceiptPrinterName = SelectedReceiptPrinter == SystemDefaultPrinterOption ? string.Empty : SelectedReceiptPrinter;
            settings.ScreenLockEnabled = ScreenLockEnabled;
            SettingsService.Save(settings);

            StatusMessage = lockRequestedWithoutPin
                ? "Company settings saved. Screen lock needs a PIN set below before it can be turned on."
                : "Company settings saved.";
        });
    }

    /// <summary>PIN is stored via <see cref="ISecureTokenStore"/> (DPAPI-encrypted at rest, same
    /// mechanism used for the Square/QuickBooks/Gemini secrets) rather than hashed — consistent
    /// with how every other secret in this app is stored, and simple enough for a 4-6 digit PIN.</summary>
    [RelayCommand]
    private void SetPin()
    {
        if (NewPinInput.Length < 4 || NewPinInput.Length > 6 || !NewPinInput.All(char.IsDigit))
        {
            PinStatusMessage = "PIN must be 4-6 digits.";
            return;
        }
        if (NewPinInput != ConfirmPinInput)
        {
            PinStatusMessage = "PINs don't match.";
            return;
        }

        _secureTokenStore.SaveSecret(SecretKeys.ScreenLockPin, NewPinInput);
        HasScreenLockPin = true;
        NewPinInput = string.Empty;
        ConfirmPinInput = string.Empty;
        PinStatusMessage = "PIN saved.";
    }
}
