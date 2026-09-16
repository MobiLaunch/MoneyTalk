using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MoneyTalk.Core.Interfaces.Integrations;
using MoneyTalk.Integrations.QuickBooks.Models;

namespace MoneyTalk.Integrations.QuickBooks;

/// <summary>Talks to the QuickBooks Online Accounting API v3
/// (https://developer.intuit.com/app/developer/qbo/docs/api/accounting) so a business that
/// already keeps books in QuickBooks — or whose accountant insists on it — can import its chart
/// of accounts/customers/open invoices into MoneyTalk, or push invoices MoneyTalk created back
/// out to QuickBooks.</summary>
public class QuickBooksClient : IQuickBooksClient
{
    private static readonly string[] Scopes = { "com.intuit.quickbooks.accounting" };

    private readonly HttpClient _httpClient;
    private readonly QuickBooksOptions _options;

    public QuickBooksClient(HttpClient httpClient, QuickBooksOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string BuildAuthorizationUrl(string state)
    {
        var query = string.Join('&', new[]
        {
            $"client_id={Uri.EscapeDataString(_options.ClientId)}",
            "response_type=code",
            $"scope={Uri.EscapeDataString(string.Join(' ', Scopes))}",
            $"redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}",
            $"state={Uri.EscapeDataString(state)}"
        });
        return $"{_options.AuthorizationBaseUrl}?{query}";
    }

    public async Task<QuickBooksOAuthTokens> ExchangeAuthorizationCodeAsync(string code, string realmId, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri
        };
        var response = await PostTokenRequestAsync(form, ct);
        return new QuickBooksOAuthTokens(response.AccessToken, response.RefreshToken, DateTime.UtcNow.AddSeconds(response.ExpiresInSeconds), realmId);
    }

    public async Task<QuickBooksOAuthTokens> RefreshTokenAsync(string refreshToken, string realmId, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };
        var response = await PostTokenRequestAsync(form, ct);
        return new QuickBooksOAuthTokens(response.AccessToken, response.RefreshToken, DateTime.UtcNow.AddSeconds(response.ExpiresInSeconds), realmId);
    }

    public async Task<QboCompanyInfo> GetCompanyInfoAsync(string accessToken, string realmId, CancellationToken ct = default)
    {
        var response = await GetJsonAsync<QboCompanyInfoResponse>($"/v3/company/{realmId}/companyinfo/{realmId}", accessToken, ct);
        var info = response.CompanyInfo ?? throw new InvalidOperationException("QuickBooks returned no company info.");
        return new QboCompanyInfo(realmId, info.CompanyName, info.LegalName, info.Country);
    }

    public async Task<IReadOnlyList<Core.Interfaces.Integrations.QboAccount>> QueryAccountsAsync(string accessToken, string realmId, CancellationToken ct = default)
    {
        var result = await RunQueryAsync<QboAccountQueryResult>(accessToken, realmId, "select * from Account maxresults 1000", ct);
        return (result.Account ?? new())
            .Select(a => new Core.Interfaces.Integrations.QboAccount(a.Id, a.Name, a.AccountType, a.AccountSubType ?? "", a.CurrentBalance))
            .ToList();
    }

    public async Task<IReadOnlyList<Core.Interfaces.Integrations.QboCustomer>> QueryCustomersAsync(string accessToken, string realmId, CancellationToken ct = default)
    {
        var result = await RunQueryAsync<QboCustomerQueryResult>(accessToken, realmId, "select * from Customer maxresults 1000", ct);
        return (result.Customer ?? new())
            .Select(c => new Core.Interfaces.Integrations.QboCustomer(c.Id, c.DisplayName, c.PrimaryEmailAddr?.Address, c.Balance))
            .ToList();
    }

    public async Task<IReadOnlyList<QboInvoice>> QueryInvoicesAsync(string accessToken, string realmId, CancellationToken ct = default)
    {
        var result = await RunQueryAsync<QboInvoiceQueryResult>(accessToken, realmId, "select * from Invoice maxresults 1000", ct);
        return (result.Invoice ?? new()).Select(MapInvoice).ToList();
    }

    public async Task<QboInvoice> CreateInvoiceAsync(string accessToken, string realmId, QboInvoice invoice, CancellationToken ct = default)
    {
        // QuickBooks requires every invoice line to reference an existing Item via ItemRef.
        // Callers are expected to have mapped MoneyTalk items to QuickBooks item ids ahead of
        // time (see the Integrations settings page); "1" is QuickBooks' well-known default
        // "Services" item id used as a fallback when no mapping exists.
        var payload = new
        {
            CustomerRef = new { value = invoice.CustomerId },
            DocNumber = invoice.DocNumber,
            TxnDate = invoice.TxnDate.ToString("yyyy-MM-dd"),
            DueDate = invoice.DueDate.ToString("yyyy-MM-dd"),
            Line = invoice.Lines.Select(l => new
            {
                Amount = l.Amount,
                DetailType = "SalesItemLineDetail",
                Description = l.Description,
                SalesItemLineDetail = new
                {
                    ItemRef = new { value = "1" },
                    Qty = l.Quantity,
                    UnitPrice = l.UnitPrice
                }
            }).ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl}/v3/company/{realmId}/invoice?minorversion={_options.MinorVersion}")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        var envelope = await response.Content.ReadFromJsonAsync<QboInvoiceEnvelope>(cancellationToken: ct)
            ?? throw new InvalidOperationException("QuickBooks returned an empty response creating the invoice.");
        return MapInvoice(envelope.Invoice ?? throw new InvalidOperationException("QuickBooks did not return the created invoice."));
    }

    private class QboInvoiceEnvelope
    {
        [System.Text.Json.Serialization.JsonPropertyName("Invoice")]
        public QboInvoiceDto? Invoice { get; set; }
    }

    private static QboInvoice MapInvoice(QboInvoiceDto dto) => new(
        dto.Id,
        dto.DocNumber ?? string.Empty,
        dto.CustomerRef?.Value ?? string.Empty,
        dto.TxnDate,
        dto.DueDate,
        dto.TotalAmt,
        dto.Balance,
        (dto.Line ?? new())
            .Where(l => l.DetailType == "SalesItemLineDetail")
            .Select(l => new QboInvoiceLine(
                l.Description ?? string.Empty,
                l.Amount,
                l.SalesItemLineDetail?.Qty ?? 1,
                l.SalesItemLineDetail?.UnitPrice ?? l.Amount))
            .ToList());

    private async Task<T> RunQueryAsync<T>(string accessToken, string realmId, string query, CancellationToken ct)
    {
        var envelope = await GetJsonAsync<QboQueryResponseEnvelope<T>>(
            $"/v3/company/{realmId}/query?query={Uri.EscapeDataString(query)}", accessToken, ct);
        return envelope.QueryResponse ?? throw new InvalidOperationException("QuickBooks query returned no data.");
    }

    private async Task<T> GetJsonAsync<T>(string relativeUrl, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.ApiBaseUrl + relativeUrl + (relativeUrl.Contains('?') ? "&" : "?") + $"minorversion={_options.MinorVersion}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
            ?? throw new InvalidOperationException("QuickBooks API returned an empty response.");
    }

    private async Task<QboTokenResponse> PostTokenRequestAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };
        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<QboTokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("QuickBooks token endpoint returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"QuickBooks API call failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
    }
}
