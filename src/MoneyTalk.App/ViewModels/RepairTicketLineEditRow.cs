using CommunityToolkit.Mvvm.ComponentModel;

namespace MoneyTalk.App.ViewModels;

public partial class RepairTicketLineEditRow : ObservableObject
{
    public Guid? ItemId { get; set; }

    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private decimal unitPrice;

    public decimal Amount => Math.Round(Quantity * UnitPrice, 2);

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(Amount));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(Amount));
}
