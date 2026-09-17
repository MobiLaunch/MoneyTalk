using CommunityToolkit.Mvvm.ComponentModel;

namespace MoneyTalk.App.ViewModels;

public partial class PosCartRow : ObservableObject
{
    public Guid? ItemId { get; init; }
    public Guid? IncomeAccountId { get; init; }

    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private decimal unitPrice;

    public decimal Amount => Math.Round(Quantity * UnitPrice, 2);

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(Amount));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(Amount));
}
