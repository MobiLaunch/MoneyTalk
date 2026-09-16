using CommunityToolkit.Mvvm.ComponentModel;

namespace MoneyTalk.App.ViewModels;

/// <summary>An editable line row bound to the line-items DataGrid on InvoiceEditPage. Kept
/// separate from <see cref="MoneyTalk.Core.Entities.InvoiceLine"/> so the grid can bind directly
/// to a mutable, change-notifying view model without dragging EF change-tracking into the UI
/// layer.</summary>
public partial class InvoiceLineEditRow : ObservableObject
{
    public Guid? ItemId { get; set; }
    public Guid? IncomeAccountId { get; set; }
    public Guid? TaxRateId { get; set; }

    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private decimal unitPrice;

    public decimal Amount => Math.Round(Quantity * UnitPrice, 2);

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(Amount));
    partial void OnUnitPriceChanged(decimal value) => OnPropertyChanged(nameof(Amount));
}
