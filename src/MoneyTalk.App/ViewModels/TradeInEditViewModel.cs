using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public partial class TradeInEditViewModel : ViewModelBase
{
    private readonly TradeInPriceLookupService _priceLookupService;
    private readonly ISecureTokenStore _secureTokenStore;
    private readonly Services.INavigationService _navigationService;

    private Guid? _tradeInId;

    public static IReadOnlyList<string> ConditionGradeOptions { get; } = Enum.GetNames<ConditionGrade>();
    public static IReadOnlyList<string> ScreenConditionOptions { get; } = Enum.GetNames<ScreenCondition>();
    public static IReadOnlyList<string> StatusOptions { get; } = Enum.GetNames<TradeInStatus>();

    public ObservableCollection<Customer> Customers { get; } = new();

    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private string brand = string.Empty;
    [ObservableProperty] private string model = string.Empty;
    [ObservableProperty] private string? modelNumber;
    [ObservableProperty] private string? imei;
    [ObservableProperty] private string? storage;
    [ObservableProperty] private string? color;

    [ObservableProperty] private string conditionGradeText = nameof(ConditionGrade.Good);
    [ObservableProperty] private string screenConditionText = nameof(ScreenCondition.Perfect);
    [ObservableProperty] private decimal ageYears;
    [ObservableProperty] private int batteryHealthPercent = 80;
    [ObservableProperty] private string? functionalIssuesText;
    [ObservableProperty] private string? cosmeticIssuesText;
    [ObservableProperty] private string? accessoriesText;
    [ObservableProperty] private bool iCloudLocked;
    [ObservableProperty] private bool frpLocked;

    [ObservableProperty] private decimal marketPrice;
    [ObservableProperty] private decimal repairCostEstimate;
    [ObservableProperty] private decimal offerPrice;
    [ObservableProperty] private decimal estimatedResaleValue;
    [ObservableProperty] private decimal estimatedProfit;
    [ObservableProperty] private decimal deductions;
    [ObservableProperty] private decimal accessoryBonus;
    [ObservableProperty] private string? priceLookupNote;

    [ObservableProperty] private string statusText = nameof(TradeInStatus.Pending);
    [ObservableProperty] private string? notes;

    public bool IsNew => _tradeInId == null;
    public bool ShowInvalidImeiWarning => !string.IsNullOrWhiteSpace(Imei) && !TradeInValuationService.IsValidImei(Imei);

    public TradeInEditViewModel(
        Func<IUnitOfWork> unitOfWorkFactory, Services.LocalSettingsService settingsService,
        TradeInPriceLookupService priceLookupService, ISecureTokenStore secureTokenStore,
        Services.INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _priceLookupService = priceLookupService;
        _secureTokenStore = secureTokenStore;
        _navigationService = navigationService;
    }

    partial void OnImeiChanged(string? value) => OnPropertyChanged(nameof(ShowInvalidImeiWarning));

    public async Task LoadAsync(TradeInEditNavigationArgs args)
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId && c.IsActive);
            Customers.Clear();
            foreach (var customer in customers.OrderBy(c => c.Name)) Customers.Add(customer);

            if (args.TradeInId.HasValue)
            {
                var tradeIn = await uow.TradeIns.GetByIdAsync(args.TradeInId.Value)
                    ?? throw new InvalidOperationException("This trade-in no longer exists.");

                _tradeInId = tradeIn.Id;
                SelectedCustomer = tradeIn.CustomerId.HasValue ? Customers.FirstOrDefault(c => c.Id == tradeIn.CustomerId) : null;
                Brand = tradeIn.Brand;
                Model = tradeIn.Model;
                ModelNumber = tradeIn.ModelNumber;
                Imei = tradeIn.Imei;
                Storage = tradeIn.Storage;
                Color = tradeIn.Color;
                ConditionGradeText = tradeIn.ConditionGrade.ToString();
                ScreenConditionText = tradeIn.ScreenCondition.ToString();
                AgeYears = tradeIn.AgeYears;
                BatteryHealthPercent = tradeIn.BatteryHealthPercent;
                FunctionalIssuesText = tradeIn.FunctionalIssues;
                CosmeticIssuesText = tradeIn.CosmeticIssues;
                AccessoriesText = tradeIn.Accessories;
                ICloudLocked = tradeIn.ICloudLocked;
                FrpLocked = tradeIn.FrpLocked;
                MarketPrice = tradeIn.MarketPrice ?? 0;
                RepairCostEstimate = tradeIn.RepairCostEstimate ?? 0;
                OfferPrice = tradeIn.OfferPrice ?? 0;
                EstimatedResaleValue = tradeIn.EstimatedResaleValue ?? 0;
                EstimatedProfit = tradeIn.EstimatedProfit ?? 0;
                StatusText = tradeIn.Status.ToString();
                Notes = tradeIn.Notes;
            }
            else
            {
                SelectedCustomer = args.CustomerId.HasValue ? Customers.FirstOrDefault(c => c.Id == args.CustomerId) : null;
            }

            OnPropertyChanged(nameof(IsNew));
        });
    }

    [RelayCommand]
    private async Task LookUpMarketPriceAsync()
    {
        var query = string.Join(' ', new[] { Brand, Model, Storage }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(query)) { ErrorMessage = "Enter a brand and model first."; return; }

        await RunBusyAsync(async () =>
        {
            var geminiApiKey = _secureTokenStore.GetSecret(Services.SecretKeys.GeminiApiKey);
            var result = await _priceLookupService.LookupAsync(query, geminiApiKey);
            if (result == null)
            {
                PriceLookupNote = $"No pricing data found for \"{query}\" — enter the market price manually.";
                return;
            }

            MarketPrice = result.Median;
            PriceLookupNote = result.SourceNote;
        });
    }

    [RelayCommand]
    private void CalculateOffer()
    {
        var functionalCount = SplitList(FunctionalIssuesText).Count;
        var cosmeticCount = SplitList(CosmeticIssuesText).Count;
        var accessories = SplitList(AccessoriesText);

        var result = TradeInValuationService.Calculate(
            MarketPrice,
            Enum.Parse<ConditionGrade>(ConditionGradeText),
            Enum.Parse<ScreenCondition>(ScreenConditionText),
            AgeYears, BatteryHealthPercent, functionalCount, cosmeticCount,
            ICloudLocked, FrpLocked, accessories, RepairCostEstimate);

        Deductions = result.Deductions;
        AccessoryBonus = result.AccessoryBonus;
        OfferPrice = result.OfferPrice;
        EstimatedResaleValue = result.EstimatedResaleValue;
        EstimatedProfit = result.EstimatedProfit;
    }

    private static List<string> SplitList(string? text) =>
        (text ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Brand) || string.IsNullOrWhiteSpace(Model))
        {
            ErrorMessage = "Enter a brand and model first.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var tradeIn = _tradeInId.HasValue
                ? await uow.TradeIns.GetByIdAsync(_tradeInId.Value) ?? throw new InvalidOperationException("This trade-in no longer exists.")
                : new TradeIn { CompanyId = ActiveCompanyId };

            tradeIn.CustomerId = SelectedCustomer?.Id;
            tradeIn.Brand = Brand.Trim();
            tradeIn.Model = Model.Trim();
            tradeIn.ModelNumber = ModelNumber;
            tradeIn.Imei = Imei;
            tradeIn.Storage = Storage;
            tradeIn.Color = Color;
            tradeIn.ConditionGrade = Enum.Parse<ConditionGrade>(ConditionGradeText);
            tradeIn.ScreenCondition = Enum.Parse<ScreenCondition>(ScreenConditionText);
            tradeIn.AgeYears = AgeYears;
            tradeIn.BatteryHealthPercent = BatteryHealthPercent;
            tradeIn.FunctionalIssues = FunctionalIssuesText;
            tradeIn.CosmeticIssues = CosmeticIssuesText;
            tradeIn.Accessories = AccessoriesText;
            tradeIn.ICloudLocked = ICloudLocked;
            tradeIn.FrpLocked = FrpLocked;
            tradeIn.MarketPrice = MarketPrice > 0 ? MarketPrice : null;
            tradeIn.RepairCostEstimate = RepairCostEstimate > 0 ? RepairCostEstimate : null;
            tradeIn.OfferPrice = OfferPrice > 0 ? OfferPrice : null;
            tradeIn.EstimatedResaleValue = EstimatedResaleValue > 0 ? EstimatedResaleValue : null;
            tradeIn.EstimatedProfit = EstimatedProfit != 0 ? EstimatedProfit : null;
            tradeIn.Status = Enum.Parse<TradeInStatus>(StatusText);
            tradeIn.Notes = Notes;
            tradeIn.ModifiedAtUtc = DateTime.UtcNow;

            if (!_tradeInId.HasValue) await uow.TradeIns.AddAsync(tradeIn);
            else uow.TradeIns.Update(tradeIn);
            await uow.SaveChangesAsync();

            _tradeInId = tradeIn.Id;
            OnPropertyChanged(nameof(IsNew));
        });
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
