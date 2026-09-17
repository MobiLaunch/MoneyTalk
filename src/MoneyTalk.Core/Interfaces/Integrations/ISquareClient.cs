namespace MoneyTalk.Core.Interfaces.Integrations;

public record SquareOAuthTokens(
    string AccessToken,
    string? RefreshToken,
    DateTime ExpiresAtUtc,
    string MerchantId);

public record SquareLocation(string Id, string Name, string? Address);

public record SquarePayment(
    string Id,
    DateTime CreatedAt,
    decimal AmountMoney,
    decimal? TipMoney,
    decimal? ProcessingFeeMoney,
    string Currency,
    string Status,
    string? OrderId,
    string? ReceiptUrl);

public record SquarePayout(
    string Id,
    DateTime CreatedAt,
    decimal AmountMoney,
    string Currency,
    string Status,
    string? DestinationType);

public record SquareOrderLineItem(string Name, decimal Quantity, decimal AmountMoney);

public record SquareOrder(
    string Id,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    string State,
    decimal Total,
    decimal Tax,
    decimal Discount,
    decimal Tip,
    IReadOnlyList<SquareOrderLineItem> LineItems);

public record SquareCustomer(string Id, string Name, string? Email, string? Phone, string? Note, DateTime CreatedAt);

/// <summary>A Terminal API device pairing code — <see cref="DeviceId"/> is null until the physical
/// terminal has actually entered the code and completed pairing.</summary>
public record SquareDeviceCode(string DeviceCodeId, string? PairingCode, string Status, string? DeviceId);

/// <summary>A Terminal API checkout — a request to charge a card on a paired, physical terminal.
/// <see cref="Status"/> transitions PENDING → IN_PROGRESS → COMPLETED/CANCELED, polled via
/// <see cref="ISquareClient.GetTerminalCheckoutStatusAsync"/>.</summary>
public record SquareTerminalCheckout(string CheckoutId, string Status, IReadOnlyList<string> PaymentIds);

/// <summary>Client for Square's Connect API — OAuth, Payments, Orders, Payouts, Customers, and
/// Terminal API (card-present checkout on a paired physical reader) — the full surface MoneyTalk
/// needs to both reconcile Square sales into the ledger and run in-app checkout itself.</summary>
public interface ISquareClient
{
    string BuildAuthorizationUrl(string state, IReadOnlyList<string> scopes);
    Task<SquareOAuthTokens> ExchangeAuthorizationCodeAsync(string code, CancellationToken ct = default);
    Task<SquareOAuthTokens> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<IReadOnlyList<SquareLocation>> GetLocationsAsync(string accessToken, CancellationToken ct = default);

    /// <summary>Fetches a single location — used as a lightweight "are these credentials valid"
    /// connection test as well as for display (location name) purposes.</summary>
    Task<SquareLocation> GetLocationAsync(string accessToken, string locationId, CancellationToken ct = default);

    Task<IReadOnlyList<SquarePayment>> GetPaymentsAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default);

    Task<IReadOnlyList<SquarePayout>> GetPayoutsAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default);

    Task<IReadOnlyList<SquareOrder>> SearchOrdersAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default);

    Task<IReadOnlyList<SquareCustomer>> SearchCustomersAsync(string accessToken, CancellationToken ct = default);

    /// <summary>Generates a 4-digit pairing code the user enters on a physical Square Terminal's
    /// own Settings → Connect screen. Poll <see cref="GetDeviceCodeStatusAsync"/> until
    /// <c>DeviceId</c> is populated, which means pairing succeeded.</summary>
    Task<SquareDeviceCode> PairTerminalDeviceAsync(string accessToken, string locationId, CancellationToken ct = default);

    Task<SquareDeviceCode> GetDeviceCodeStatusAsync(string accessToken, string deviceCodeId, CancellationToken ct = default);

    /// <summary>Pushes a card-present charge to a paired terminal; the customer taps/inserts on
    /// the physical device. <paramref name="amountCents"/> is the charge amount in the smallest
    /// currency unit (cents for USD), matching Square's own Money type.</summary>
    Task<SquareTerminalCheckout> CreateTerminalCheckoutAsync(
        string accessToken, string deviceId, long amountCents, string currency = "USD",
        string? referenceId = null, string? note = null, CancellationToken ct = default);

    Task<SquareTerminalCheckout> GetTerminalCheckoutStatusAsync(string accessToken, string checkoutId, CancellationToken ct = default);
}
