using System.Net.Http.Headers;
using System.Net.Http.Json;
using MoneyTalk.Core.Interfaces.Integrations;
using MoneyTalk.Integrations.Square.Models;

namespace MoneyTalk.Integrations.Square;

/// <summary>Talks to Square's Connect API (https://developer.squareup.com/reference/square) for
/// OAuth, point-of-sale payments, and payouts (bank deposits) — the data MoneyTalk needs to
/// reconcile Square sales against the bank account they land in.</summary>
public class SquareClient : ISquareClient
{
    private readonly HttpClient _httpClient;
    private readonly SquareOptions _options;

    public SquareClient(HttpClient httpClient, SquareOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string BuildAuthorizationUrl(string state, IReadOnlyList<string> scopes)
    {
        var query = BuildQueryString(
            ("client_id", _options.ClientId),
            ("scope", string.Join(' ', scopes)),
            ("session", "false"),
            ("state", state),
            ("redirect_uri", _options.RedirectUri));
        return $"{_options.ApiBaseUrl}/oauth2/authorize?{query}";
    }

    public async Task<SquareOAuthTokens> ExchangeAuthorizationCodeAsync(string code, CancellationToken ct = default)
    {
        var response = await PostJsonAsync<SquareOAuthTokenResponse>("/oauth2/token", new
        {
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            code,
            grant_type = "authorization_code",
            redirect_uri = _options.RedirectUri
        }, ct);

        return ToTokens(response);
    }

    public async Task<SquareOAuthTokens> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var response = await PostJsonAsync<SquareOAuthTokenResponse>("/oauth2/token", new
        {
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            refresh_token = refreshToken,
            grant_type = "refresh_token"
        }, ct);

        return ToTokens(response);
    }

    public async Task<IReadOnlyList<SquareLocation>> GetLocationsAsync(string accessToken, CancellationToken ct = default)
    {
        var response = await GetJsonAsync<SquareLocationsResponse>("/v2/locations", accessToken, ct);
        return (response.Locations ?? new())
            .Select(l => new SquareLocation(l.Id, l.Name, l.Address?.ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<SquarePayment>> GetPaymentsAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default)
    {
        var results = new List<SquarePayment>();
        string? cursor = null;
        do
        {
            var query = BuildQueryString(
                ("location_id", locationId),
                ("begin_time", beginTimeUtc.ToString("o")),
                ("end_time", endTimeUtc.ToString("o")),
                ("sort_order", "ASC"),
                ("cursor", cursor));

            var response = await GetJsonAsync<SquareListPaymentsResponse>($"/v2/payments?{query}", accessToken, ct);
            results.AddRange((response.Payments ?? new()).Select(p => new SquarePayment(
                p.Id,
                p.CreatedAt,
                p.AmountMoney?.ToDecimal() ?? 0m,
                p.TipMoney?.ToDecimal(),
                p.ProcessingFee?.Sum(f => f.AmountMoney?.ToDecimal() ?? 0m),
                p.AmountMoney?.Currency ?? "USD",
                p.Status,
                p.OrderId,
                p.ReceiptUrl)));
            cursor = response.Cursor;
        } while (cursor != null);

        return results;
    }

    public async Task<IReadOnlyList<SquarePayout>> GetPayoutsAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default)
    {
        var results = new List<SquarePayout>();
        string? cursor = null;
        do
        {
            var query = BuildQueryString(
                ("location_id", locationId),
                ("begin_time", beginTimeUtc.ToString("o")),
                ("end_time", endTimeUtc.ToString("o")),
                ("cursor", cursor));

            var response = await GetJsonAsync<SquareListPayoutsResponse>($"/v2/payouts?{query}", accessToken, ct);
            results.AddRange((response.Payouts ?? new()).Select(p => new SquarePayout(
                p.Id,
                p.CreatedAt,
                p.AmountMoney?.ToDecimal() ?? 0m,
                p.AmountMoney?.Currency ?? "USD",
                p.Status,
                p.Destination?.Type)));
            cursor = response.Cursor;
        } while (cursor != null);

        return results;
    }

    private static SquareOAuthTokens ToTokens(SquareOAuthTokenResponse response) => new(
        response.AccessToken,
        response.RefreshToken,
        DateTime.TryParse(response.ExpiresAt, out var expires) ? expires : DateTime.UtcNow.AddDays(30),
        response.MerchantId);

    private async Task<T> GetJsonAsync<T>(string relativeUrl, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.ApiBaseUrl + relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Square-Version", _options.ApiVersion);

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Square API returned an empty response.");
    }

    private async Task<T> PostJsonAsync<T>(string relativeUrl, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiBaseUrl + relativeUrl)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Square-Version", _options.ApiVersion);

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Square API returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Square API call failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
    }

    private static string BuildQueryString(params (string Key, string? Value)[] pairs) =>
        string.Join('&', pairs
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
}
