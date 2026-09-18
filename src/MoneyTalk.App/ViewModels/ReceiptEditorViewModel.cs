using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

/// <summary>Lets a shop customize the optional header/footer text printed on POS receipts. The
/// itemized body (customer, lines, total) is always the same shape — only the surrounding text is
/// configurable, kept as plain settings rather than a full template language.</summary>
public partial class ReceiptEditorViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ObservableCollection<string> PreviewLines { get; } = new();

    [ObservableProperty] private string headerText = string.Empty;
    [ObservableProperty] private string footerText = string.Empty;
    [ObservableProperty] private string? statusMessage;

    public ReceiptEditorViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService, INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _navigationService = navigationService;
    }

    public void Load()
    {
        var settings = SettingsService.Load();
        HeaderText = settings.ReceiptHeaderText;
        FooterText = settings.ReceiptFooterText;
        UpdatePreview();
    }

    partial void OnHeaderTextChanged(string value) => UpdatePreview();
    partial void OnFooterTextChanged(string value) => UpdatePreview();

    private void UpdatePreview()
    {
        PreviewLines.Clear();
        if (!string.IsNullOrWhiteSpace(HeaderText))
        {
            PreviewLines.Add(HeaderText);
            PreviewLines.Add(string.Empty);
        }
        PreviewLines.Add("Customer: Walk-in");
        PreviewLines.Add(new string('-', 32));
        PreviewLines.Add("1 x Sample Item  $19.99");
        PreviewLines.Add(new string('-', 32));
        PreviewLines.Add("Total: $19.99");
        if (!string.IsNullOrWhiteSpace(FooterText))
        {
            PreviewLines.Add(string.Empty);
            PreviewLines.Add(FooterText);
        }
    }

    [RelayCommand]
    private void Save()
    {
        var settings = SettingsService.Load();
        settings.ReceiptHeaderText = HeaderText;
        settings.ReceiptFooterText = FooterText;
        SettingsService.Save(settings);
        StatusMessage = "Receipt template saved.";
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
