using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

/// <summary>Lets a shop customize what prints on item labels (Products &amp; Services → Print
/// Label): which fields appear under the item name, and which barcode symbology encodes the SKU.
/// Settings are machine-wide (see <see cref="AppSettings"/>), same as the printer selections this
/// page is reached from.</summary>
public partial class LabelEditorViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public static IReadOnlyList<string> BarcodeFormatOptions { get; } = new[] { nameof(LabelBarcodeFormat.Code128), nameof(LabelBarcodeFormat.QrCode) };

    [ObservableProperty] private bool showSku = true;
    [ObservableProperty] private bool showPrice = true;
    [ObservableProperty] private string barcodeFormatText = nameof(LabelBarcodeFormat.Code128);
    [ObservableProperty] private string previewSecondaryText = string.Empty;
    [ObservableProperty] private WriteableBitmap? previewBarcode;
    [ObservableProperty] private string? statusMessage;

    public LabelEditorViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _navigationService = navigationService;
    }

    public void Load()
    {
        var settings = SettingsService.Load();
        ShowSku = settings.LabelShowSku;
        ShowPrice = settings.LabelShowPrice;
        BarcodeFormatText = settings.LabelBarcodeFormat;
        UpdatePreview();
    }

    partial void OnShowSkuChanged(bool value) => UpdatePreview();
    partial void OnShowPriceChanged(bool value) => UpdatePreview();
    partial void OnBarcodeFormatTextChanged(string value) => UpdatePreview();

    private void UpdatePreview()
    {
        var parts = new List<string>();
        if (ShowSku) parts.Add("SKU-1001");
        if (ShowPrice) parts.Add("$19.99");
        PreviewSecondaryText = string.Join(" · ", parts);

        var format = BarcodeFormatText == nameof(LabelBarcodeFormat.QrCode) ? LabelBarcodeFormat.QrCode : LabelBarcodeFormat.Code128;
        var pixelData = BarcodeService.Encode("SKU-1001", format, 220, 70);
        PreviewBarcode = BarcodeService.ToWriteableBitmap(pixelData);
    }

    [RelayCommand]
    private void Save()
    {
        var settings = SettingsService.Load();
        settings.LabelShowSku = ShowSku;
        settings.LabelShowPrice = ShowPrice;
        settings.LabelBarcodeFormat = BarcodeFormatText;
        SettingsService.Save(settings);
        StatusMessage = "Label template saved.";
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
