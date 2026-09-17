using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;
using MoneyTalk.Core.Interfaces.Integrations;

namespace MoneyTalk.App.ViewModels;

public class OpenTicketRow
{
    public Guid Id { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string Device { get; init; } = string.Empty;
    public decimal Balance { get; init; }
}

/// <summary>Point-of-sale register: ring up a cart of items/services, or collect payment against
/// an open repair ticket. Card payments go through a paired Square Terminal (see
/// <see cref="PairTerminalAsync"/>/<see cref="ChargeCardAsync"/>) — there's no card-not-present
/// path since Square has no native Windows SDK for that.</summary>
public partial class PosViewModel : ViewModelBase
{
    private readonly PosService _posService;
    private readonly ISquareClient _squareClient;
    private readonly ISecureTokenStore _secureTokenStore;

    public ObservableCollection<Item> CatalogItems { get; } = new();
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<BankAccount> DepositAccounts { get; } = new();
    public ObservableCollection<PosCartRow> Cart { get; } = new();
    public ObservableCollection<OpenTicketRow> OpenTickets { get; } = new();

    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private BankAccount? selectedDepositAccount;
    [ObservableProperty] private string subtotalDisplay = "$0.00";
    [ObservableProperty] private OpenTicketRow? selectedTicket;
    [ObservableProperty] private decimal ticketPaymentAmount;

    [ObservableProperty] private bool isPairingTerminal;
    [ObservableProperty] private string? pairingCode;
    [ObservableProperty] private string? pairingStatusMessage;

    [ObservableProperty] private bool isChargingCard;
    [ObservableProperty] private string? cardStatusMessage;
    private string? _pendingDeviceCodeId;
    private string? _pendingCheckoutId;

    [ObservableProperty] private bool hasLastReceipt;
    private List<(string Description, decimal Quantity, decimal UnitPrice, decimal Amount)> _lastReceiptLines = new();
    private decimal _lastReceiptTotal;
    private string _lastReceiptCustomerName = "Walk-in";

    public decimal CartTotal => Cart.Sum(l => l.Amount);
    public bool HasPairedTerminal => !string.IsNullOrEmpty(SettingsService.Load().SquareTerminalDeviceId);

    public PosViewModel(
        Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService,
        PosService posService, ISquareClient squareClient, ISecureTokenStore secureTokenStore)
        : base(unitOfWorkFactory, settingsService)
    {
        _posService = posService;
        _squareClient = squareClient;
        _secureTokenStore = secureTokenStore;
        Cart.CollectionChanged += (_, __) => RecalculateTotal();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var items = await uow.Items.FindAsync(i => i.CompanyId == companyId && i.IsActive);
            CatalogItems.Clear();
            foreach (var item in items.OrderBy(i => i.Name)) CatalogItems.Add(item);

            var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId && c.IsActive);
            Customers.Clear();
            foreach (var customer in customers.OrderBy(c => c.Name)) Customers.Add(customer);

            var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == companyId && b.IsActive);
            DepositAccounts.Clear();
            foreach (var account in bankAccounts) DepositAccounts.Add(account);
            SelectedDepositAccount ??= DepositAccounts.FirstOrDefault();

            var tickets = await uow.RepairTickets.FindAsync(t => t.CompanyId == companyId);
            OpenTickets.Clear();
            foreach (var ticket in tickets.Where(t => t.Balance > 0).OrderByDescending(t => t.CreatedAtUtc))
                OpenTickets.Add(new OpenTicketRow { Id = ticket.Id, TicketNumber = ticket.TicketNumber, Device = ticket.Device, Balance = ticket.Balance });
        });

        OnPropertyChanged(nameof(HasPairedTerminal));
    }

    public void AddToCart(Item item)
    {
        var existing = Cart.FirstOrDefault(c => c.ItemId == item.Id);
        if (existing != null) { existing.Quantity += 1; return; }

        Cart.Add(new PosCartRow
        {
            ItemId = item.Id,
            IncomeAccountId = item.IncomeAccountId,
            Description = item.Name,
            Quantity = 1,
            UnitPrice = item.SalesPrice
        });
    }

    [RelayCommand]
    private void RemoveFromCart(PosCartRow? row)
    {
        if (row != null) Cart.Remove(row);
    }

    [RelayCommand]
    private void ClearCart() => Cart.Clear();

    private void RecalculateTotal()
    {
        SubtotalDisplay = CartTotal.ToString("C2");
        OnPropertyChanged(nameof(CartTotal));
    }

    private async Task<bool> CompleteCartSaleAsync(PaymentMethod method)
    {
        if (SelectedDepositAccount == null) { ErrorMessage = "Choose a deposit account first."; return false; }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var lines = Cart.Select(c => new PosCartLine(c.ItemId, c.Description, c.Quantity, c.UnitPrice, c.IncomeAccountId)).ToList();
            await _posService.CompleteCartSaleAsync(uow, ActiveCompanyId, SelectedCustomer?.Id, lines, method, SelectedDepositAccount.Id);

            _lastReceiptLines = Cart.Select(c => (c.Description, c.Quantity, c.UnitPrice, c.Amount)).ToList();
            _lastReceiptTotal = CartTotal;
            _lastReceiptCustomerName = SelectedCustomer?.Name ?? "Walk-in";
            HasLastReceipt = true;

            Cart.Clear();
            success = true;
        });
        return success;
    }

    [RelayCommand]
    private Task CheckoutCashAsync() => CompleteCartSaleAsync(PaymentMethod.Cash);

    [RelayCommand]
    private Task CheckoutOtherAsync() => CompleteCartSaleAsync(PaymentMethod.Other);

    /// <summary>Reprints the last completed sale — receipts print on demand rather than
    /// automatically so a shop without a receipt printer configured never sees a failed-print
    /// error interrupt an otherwise-successful sale.</summary>
    [RelayCommand]
    private void PrintReceipt()
    {
        if (!HasLastReceipt) return;

        var lines = new List<string>();
        lines.Add($"Customer: {_lastReceiptCustomerName}");
        lines.Add(new string('-', 32));
        foreach (var line in _lastReceiptLines)
            lines.Add($"{line.Quantity:0.##} x {line.Description}  {line.Amount:C2}");
        lines.Add(new string('-', 32));
        lines.Add($"Total: {_lastReceiptTotal:C2}");

        try
        {
            PrintService.PrintReceipt(SettingsService.Load().ReceiptPrinterName, "MoneyTalk Receipt", lines);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't print receipt: {ex.Message}";
        }
    }

    /// <summary>Looks up a scanned barcode/SKU against the loaded catalog and adds it to the cart
    /// — see <c>PosPage.xaml.cs</c> for the keyboard-emulation scanner input that calls this
    /// (USB barcode scanners act as a fast, Enter-terminated keyboard, not a separate device API).</summary>
    public void AddToCartByScannedCode(string code)
    {
        var item = CatalogItems.FirstOrDefault(i => string.Equals(i.Sku, code, StringComparison.OrdinalIgnoreCase));
        if (item == null)
        {
            ErrorMessage = $"No item found for scanned code \"{code}\".";
            return;
        }
        AddToCart(item);
    }

    private async Task<(string AccessToken, string LocationId)?> GetSquareContextAsync()
    {
        var accessToken = _secureTokenStore.GetSecret(SecretKeys.SquareAccessToken(ActiveCompanyId));
        if (string.IsNullOrEmpty(accessToken)) { ErrorMessage = "Connect Square first on the Integrations page."; return null; }

        var locations = await _squareClient.GetLocationsAsync(accessToken);
        var location = locations.FirstOrDefault();
        if (location == null) { ErrorMessage = "This Square account has no locations."; return null; }

        return (accessToken, location.Id);
    }

    [RelayCommand]
    private async Task PairTerminalAsync()
    {
        await RunBusyAsync(async () =>
        {
            var context = await GetSquareContextAsync();
            if (context == null) return;

            var deviceCode = await _squareClient.PairTerminalDeviceAsync(context.Value.AccessToken, context.Value.LocationId);
            _pendingDeviceCodeId = deviceCode.DeviceCodeId;
            PairingCode = deviceCode.PairingCode;
            PairingStatusMessage = "Enter this code on your Square Terminal's Settings → Connect screen, then click \"Check Status\".";
            IsPairingTerminal = true;
        });
    }

    [RelayCommand]
    private async Task CheckPairingStatusAsync()
    {
        if (_pendingDeviceCodeId == null) return;

        await RunBusyAsync(async () =>
        {
            var accessToken = _secureTokenStore.GetSecret(SecretKeys.SquareAccessToken(ActiveCompanyId));
            if (string.IsNullOrEmpty(accessToken)) return;

            var status = await _squareClient.GetDeviceCodeStatusAsync(accessToken, _pendingDeviceCodeId);
            if (!string.IsNullOrEmpty(status.DeviceId))
            {
                var settings = SettingsService.Load();
                settings.SquareTerminalDeviceId = status.DeviceId;
                SettingsService.Save(settings);

                IsPairingTerminal = false;
                PairingCode = null;
                _pendingDeviceCodeId = null;
                OnPropertyChanged(nameof(HasPairedTerminal));
            }
            else
            {
                PairingStatusMessage = "Not paired yet — enter the code on the terminal, then check again.";
            }
        });
    }

    [RelayCommand]
    private void CancelPairing()
    {
        IsPairingTerminal = false;
        PairingCode = null;
        _pendingDeviceCodeId = null;
    }

    [RelayCommand]
    private async Task ChargeCardAsync()
    {
        var deviceId = SettingsService.Load().SquareTerminalDeviceId;
        if (string.IsNullOrEmpty(deviceId)) { ErrorMessage = "Pair a Square Terminal first."; return; }
        if (Cart.Count == 0) { ErrorMessage = "Add at least one item to the cart first."; return; }

        await RunBusyAsync(async () =>
        {
            var accessToken = _secureTokenStore.GetSecret(SecretKeys.SquareAccessToken(ActiveCompanyId));
            if (string.IsNullOrEmpty(accessToken)) { ErrorMessage = "Connect Square first on the Integrations page."; return; }

            var amountCents = (long)Math.Round(CartTotal * 100m, 0);
            var checkout = await _squareClient.CreateTerminalCheckoutAsync(accessToken, deviceId, amountCents);
            _pendingCheckoutId = checkout.CheckoutId;
            CardStatusMessage = "Waiting for the customer to tap, insert, or swipe on the terminal — click \"Check Status\" once they have.";
            IsChargingCard = true;
        });
    }

    [RelayCommand]
    private async Task CheckCardStatusAsync()
    {
        if (_pendingCheckoutId == null) return;

        await RunBusyAsync(async () =>
        {
            var accessToken = _secureTokenStore.GetSecret(SecretKeys.SquareAccessToken(ActiveCompanyId));
            if (string.IsNullOrEmpty(accessToken)) return;

            var checkout = await _squareClient.GetTerminalCheckoutStatusAsync(accessToken, _pendingCheckoutId);
            switch (checkout.Status)
            {
                case "COMPLETED":
                    IsChargingCard = false;
                    _pendingCheckoutId = null;
                    await CompleteCartSaleAsync(PaymentMethod.Square);
                    break;
                case "CANCELED":
                    IsChargingCard = false;
                    _pendingCheckoutId = null;
                    ErrorMessage = "The card charge was canceled on the terminal.";
                    break;
                default:
                    CardStatusMessage = $"Still waiting ({checkout.Status.ToLowerInvariant()})... click \"Check Status\" again in a moment.";
                    break;
            }
        });
    }

    [RelayCommand]
    private void CancelCardCharge()
    {
        IsChargingCard = false;
        _pendingCheckoutId = null;
    }

    [RelayCommand]
    private async Task PayTicketAsync()
    {
        if (SelectedTicket == null) { ErrorMessage = "Select a ticket first."; return; }
        if (SelectedDepositAccount == null) { ErrorMessage = "Choose a deposit account first."; return; }
        if (TicketPaymentAmount <= 0) { ErrorMessage = "Enter an amount greater than zero."; return; }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _posService.CompleteTicketPaymentAsync(
                uow, SelectedTicket.Id, TicketPaymentAmount, PaymentMethod.Cash, SelectedDepositAccount.Id);

            OpenTickets.Remove(SelectedTicket);
            SelectedTicket = null;
            TicketPaymentAmount = 0;
        });
    }
}
