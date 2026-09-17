using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;
using Windows.System;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class PosPage : Page
{
    public PosViewModel ViewModel { get; }

    // USB barcode scanners act as a keyboard that types very fast and ends with Enter — there's
    // no separate "scanner device" API to call. A burst is distinguished from normal typing by
    // speed alone: real keystrokes rarely land under ~60ms apart, but a scanner emits the whole
    // code in well under half a second.
    private readonly StringBuilder _scanBuffer = new();
    private DateTime _scanStartedAtUtc;
    private DateTime _lastCharAtUtc;
    private const double MaxInterCharGapMs = 60;
    private const double MaxTotalScanMs = 400;

    public PosPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<PosViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
        RootGrid.Focus(FocusState.Programmatic);
    }

    private void RootGrid_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        var now = DateTime.UtcNow;
        if (_scanBuffer.Length == 0 || (now - _lastCharAtUtc).TotalMilliseconds > MaxInterCharGapMs)
        {
            _scanBuffer.Clear();
            _scanStartedAtUtc = now;
        }
        _lastCharAtUtc = now;
        if (!char.IsControl(args.Character)) _scanBuffer.Append(args.Character);
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;

        var elapsedMs = (DateTime.UtcNow - _scanStartedAtUtc).TotalMilliseconds;
        var code = _scanBuffer.ToString();
        _scanBuffer.Clear();

        if (code.Length >= 3 && elapsedMs <= MaxTotalScanMs)
            ViewModel.AddToCartByScannedCode(code);
    }

    private void CatalogItem_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Item item) ViewModel.AddToCart(item);
    }

    private void RemoveFromCart_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: PosCartRow row })
            ViewModel.RemoveFromCartCommand.Execute(row);
    }
}
