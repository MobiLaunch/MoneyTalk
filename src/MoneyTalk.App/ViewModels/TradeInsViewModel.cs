using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class TradeInListRow
{
    public Guid Id { get; init; }
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public TradeInStatus Status { get; init; }
    public decimal? OfferPrice { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public partial class TradeInsViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ObservableCollection<TradeInListRow> TradeIns { get; } = new();

    [ObservableProperty] private TradeInListRow? selectedTradeIn;

    public TradeInsViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _navigationService = navigationService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var tradeIns = await uow.TradeIns.FindAsync(t => t.CompanyId == companyId);
            var customers = (await uow.Customers.FindAsync(c => c.CompanyId == companyId)).ToDictionary(c => c.Id);

            TradeIns.Clear();
            foreach (var tradeIn in tradeIns.OrderByDescending(t => t.CreatedAtUtc))
            {
                TradeIns.Add(new TradeInListRow
                {
                    Id = tradeIn.Id,
                    Brand = tradeIn.Brand,
                    Model = tradeIn.Model,
                    CustomerName = tradeIn.CustomerId.HasValue && customers.TryGetValue(tradeIn.CustomerId.Value, out var c) ? c.Name : "Walk-in",
                    Status = tradeIn.Status,
                    OfferPrice = tradeIn.OfferPrice,
                    CreatedAtUtc = tradeIn.CreatedAtUtc
                });
            }
        });
    }

    [RelayCommand]
    private void NewTradeIn() => _navigationService.NavigateTo(PageKeys.TradeInEdit, new TradeInEditNavigationArgs(null));

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedTradeIn == null) return;
        _navigationService.NavigateTo(PageKeys.TradeInEdit, new TradeInEditNavigationArgs(SelectedTradeIn.Id));
    }
}
