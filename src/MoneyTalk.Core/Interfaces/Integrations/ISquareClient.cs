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

/// <summary>Client for Square's Connect API (OAuth + Payments + Payouts) used to pull card/POS
/// sales and bank deposits into MoneyTalk's ledger and cash reconciliation.</summary>
public interface ISquareClient
{
    string BuildAuthorizationUrl(string state, IReadOnlyList<string> scopes);
    Task<SquareOAuthTokens> ExchangeAuthorizationCodeAsync(string code, CancellationToken ct = default);
    Task<SquareOAuthTokens> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<IReadOnlyList<SquareLocation>> GetLocationsAsync(string accessToken, CancellationToken ct = default);

    Task<IReadOnlyList<SquarePayment>> GetPaymentsAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default);

    Task<IReadOnlyList<SquarePayout>> GetPayoutsAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default);
}
