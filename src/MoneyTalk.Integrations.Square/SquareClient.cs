using System.Net.Http.Headers;
using System.Net.Http.Json;
using MoneyTalk.Core.Interfaces.Integrations;
using MoneyTalk.Integrations.Square.Models;

namespace MoneyTalk.Integrations.Square;

/// <summary>Talks to Square's Connect API (https://developer.squareup.com/reference/square): OAuth,
/// payments/orders/payouts (reconciling Square sales into the ledger), customer directory search,
/// and the Terminal API (pairing a physical card reader and pushing card-present checkouts to it
/// for MoneyTalk's own in-app POS).</summary>
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

    public async Task<SquareLocation> GetLocationAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        var response = await GetJsonAsync<SquareGetLocationResponse>($"/v2/locations/{locationId}", accessToken, ct);
        var location = response.Location ?? throw new InvalidOperationException("Square did not return this location — check the location id.");
        return new SquareLocation(location.Id, location.Name, location.Address?.ToString());
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

    public async Task<IReadOnlyList<SquareOrder>> SearchOrdersAsync(
        string accessToken, string locationId, DateTime beginTimeUtc, DateTime endTimeUtc, CancellationToken ct = default)
    {
        var results = new List<SquareOrder>();
        string? cursor = null;
        do
        {
            var body = new
            {
                location_ids = new[] { locationId },
                cursor,
                query = new
                {
                    filter = new
                    {
                        date_time_filter = new
                        {
                            created_at = new { start_at = beginTimeUtc.ToString("o"), end_at = endTimeUtc.ToString("o") }
                        }
                    },
                    sort = new { sort_field = "CREATED_AT", sort_order = "ASC" }
                }
            };

            var response = await PostJsonAsync<SquareSearchOrdersResponse>("/v2/orders/search", accessToken, body, ct);
            results.AddRange((response.Orders ?? new()).Select(o => new SquareOrder(
                o.Id,
                o.CreatedAt,
                o.ClosedAt,
                o.State,
                o.TotalMoney?.ToDecimal() ?? 0m,
                o.TotalTaxMoney?.ToDecimal() ?? 0m,
                o.TotalDiscountMoney?.ToDecimal() ?? 0m,
                o.TotalTipMoney?.ToDecimal() ?? 0m,
                (o.LineItems ?? new()).Select(li => new SquareOrderLineItem(
                    li.Name ?? "Item",
                    decimal.TryParse(li.Quantity, out var qty) ? qty : 1m,
                    li.TotalMoney?.ToDecimal() ?? 0m)).ToList())));
            cursor = response.Cursor;
        } while (cursor != null);

        return results;
    }

    public async Task<IReadOnlyList<SquareCustomer>> SearchCustomersAsync(string accessToken, CancellationToken ct = default)
    {
        var results = new List<SquareCustomer>();
        string? cursor = null;
        do
        {
            var body = new
            {
                cursor,
                limit = 100,
                query = new { sort = new { field = "CREATED_AT", order = "DESC" } }
            };

            var response = await PostJsonAsync<SquareSearchCustomersResponse>("/v2/customers/search", accessToken, body, ct);
            results.AddRange((response.Customers ?? new()).Select(c => new SquareCustomer(
                c.Id,
                string.Join(' ', new[] { c.GivenName, c.FamilyName }.Where(s => !string.IsNullOrWhiteSpace(s))),
                c.EmailAddress,
                c.PhoneNumber,
                c.Note,
                c.CreatedAt)));
            cursor = response.Cursor;
        } while (cursor != null);

        return results;
    }

    public async Task<SquareDeviceCode> PairTerminalDeviceAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        var body = new
        {
            idempotency_key = Guid.NewGuid().ToString("N"),
            device_code = new
            {
                name = $"MoneyTalk Terminal {DateTime.UtcNow:yyyyMMddHHmmss}",
                product_type = "TERMINAL_API",
                location_id = locationId
            }
        };

        var response = await PostJsonAsync<SquareCreateDeviceCodeResponse>("/v2/devices/codes", accessToken, body, ct);
        var code = response.DeviceCode ?? throw new InvalidOperationException("Square did not return a device pairing code.");
        return new SquareDeviceCode(code.Id, code.PairingCode, code.Status, code.DeviceId);
    }

    public async Task<SquareDeviceCode> GetDeviceCodeStatusAsync(string accessToken, string deviceCodeId, CancellationToken ct = default)
    {
        var response = await GetJsonAsync<SquareGetDeviceCodeResponse>($"/v2/devices/codes/{deviceCodeId}", accessToken, ct);
        var code = response.DeviceCode ?? throw new InvalidOperationException("Square did not return this device code — it may have expired.");
        return new SquareDeviceCode(code.Id, code.PairingCode, code.Status, code.DeviceId);
    }

    public async Task<SquareTerminalCheckout> CreateTerminalCheckoutAsync(
        string accessToken, string deviceId, long amountCents, string currency = "USD",
        string? referenceId = null, string? note = null, CancellationToken ct = default)
    {
        var body = new
        {
            idempotency_key = Guid.NewGuid().ToString("N"),
            checkout = new
            {
                amount_money = new { amount = amountCents, currency },
                device_options = new { device_id = deviceId, skip_receipt_screen = false, collect_signature = false },
                payment_type = "CARD_PRESENT",
                reference_id = referenceId ?? "moneytalk-pos",
                note = note ?? "MoneyTalk Sale"
            }
        };

        var response = await PostJsonAsync<SquareCreateTerminalCheckoutResponse>("/v2/terminals/checkouts", accessToken, body, ct);
        var checkout = response.Checkout ?? throw new InvalidOperationException("Square did not return a terminal checkout.");
        return new SquareTerminalCheckout(checkout.Id, checkout.Status, checkout.PaymentIds ?? new());
    }

    public async Task<SquareTerminalCheckout> GetTerminalCheckoutStatusAsync(string accessToken, string checkoutId, CancellationToken ct = default)
    {
        var response = await GetJsonAsync<SquareGetTerminalCheckoutResponse>($"/v2/terminals/checkouts/{checkoutId}", accessToken, ct);
        var checkout = response.Checkout ?? throw new InvalidOperationException("Square did not return this terminal checkout.");
        return new SquareTerminalCheckout(checkout.Id, checkout.Status, checkout.PaymentIds ?? new());
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

    /// <summary>For the OAuth token endpoint only, which authenticates via client_id/client_secret
    /// in the body rather than a Bearer token.</summary>
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

    private async Task<T> PostJsonAsync<T>(string relativeUrl, string accessToken, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiBaseUrl + relativeUrl)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
