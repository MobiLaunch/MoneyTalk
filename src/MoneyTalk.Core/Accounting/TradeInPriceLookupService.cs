using System.Collections.Concurrent;
using System.Text.Json;
using MoneyTalk.Core.Interfaces.Integrations;

namespace MoneyTalk.Core.Accounting;

public record DevicePriceResult(decimal EbayAvg, decimal SwappaAvg, decimal Median, string SourceNote);

/// <summary>Two-tier device resale-price lookup, ported from NovaOps's
/// server/api/trade-in/lookup.post.ts: PriceCharting's free public API first (real sold-item
/// data, no key needed), falling back to Gemini's training-data estimate only when PriceCharting
/// has no match. Results are cached in-memory for 6 hours per normalized query, same as the
/// source. This talks to PriceCharting directly with a plain HttpClient rather than through a
/// dedicated MoneyTalk.Integrations.* project — unlike Square/QuickBooks/Gemini there's no OAuth
/// or vendor SDK surface here, just one public JSON endpoint, so a whole extra project would be
/// ceremony without benefit.</summary>
public class TradeInPriceLookupService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private static readonly ConcurrentDictionary<string, (DevicePriceResult Result, DateTime ExpiresAtUtc)> Cache = new();

    private readonly HttpClient _httpClient;
    private readonly IGeminiClient _geminiClient;

    public TradeInPriceLookupService(HttpClient httpClient, IGeminiClient geminiClient)
    {
        _httpClient = httpClient;
        _geminiClient = geminiClient;
    }

    /// <param name="geminiApiKey">Pass null/empty to skip the Gemini fallback tier entirely
    /// (matches the source's behavior when no key is configured).</param>
    public async Task<DevicePriceResult?> LookupAsync(string query, string? geminiApiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var cacheKey = query.Trim().ToLowerInvariant();
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            if (cached.ExpiresAtUtc > DateTime.UtcNow) return cached.Result;
            Cache.TryRemove(cacheKey, out _);
        }

        var result = await SearchPriceChartingAsync(query, ct) ?? await SearchGeminiAsync(query, geminiApiKey, ct);
        if (result != null)
            Cache[cacheKey] = (result, DateTime.UtcNow.Add(CacheTtl));

        return result;
    }

    private async Task<DevicePriceResult?> SearchPriceChartingAsync(string query, CancellationToken ct)
    {
        try
        {
            var encoded = Uri.EscapeDataString(query);
            JsonElement? bestProduct = null;

            foreach (var url in new[]
            {
                $"https://www.pricecharting.com/api/products?q={encoded}&type=phone",
                $"https://www.pricecharting.com/api/products?q={encoded}"
            })
            {
                using var response = await _httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array
                    && products.GetArrayLength() > 0)
                {
                    bestProduct = products[0].Clone();
                    break;
                }
            }

            if (bestProduct is not { } product || !product.TryGetProperty("id", out var idElement)) return null;

            var productName = product.TryGetProperty("product-name", out var nameEl) ? nameEl.GetString() ?? query : query;

            using var detailResponse = await _httpClient.GetAsync($"https://www.pricecharting.com/api/product?id={idElement}", ct);
            if (!detailResponse.IsSuccessStatusCode) return null;

            using var detailDoc = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync(ct));
            var looseCents = GetLong(detailDoc.RootElement, "loose-price");
            var completeCents = GetLong(detailDoc.RootElement, "complete-price");
            var cents = looseCents > 0 ? looseCents : completeCents;
            if (cents <= 0) return null;

            var price = Math.Round(cents / 100m, 2);
            if (price < 5m) return null;

            return new DevicePriceResult(price, 0m, price, $"PriceCharting · {productName}");
        }
        catch
        {
            // Matches the source's behavior: any network/parse failure just falls through to
            // the Gemini tier rather than surfacing an error to the user.
            return null;
        }
    }

    private static long GetLong(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;

    private async Task<DevicePriceResult?> SearchGeminiAsync(string query, string? geminiApiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(geminiApiKey)) return null;

        const string systemInstruction =
            "You estimate used-device resale prices from training data only. Reply with ONLY a raw JSON " +
            "object, no markdown or code fences: " +
            "{\"ebay_avg\":<number>,\"swappa_avg\":<number>,\"median\":<number>,\"source_note\":\"<one short sentence>\"}. " +
            "All values must be plain numbers. If you have no data, return all zeros.";
        var userMessage = $"What is the typical used resale value (USD) for: {query}";

        try
        {
            var text = await _geminiClient.GenerateContentAsync(geminiApiKey, systemInstruction, Array.Empty<GeminiChatTurn>(), userMessage, ct);
            var json = ExtractJson(text);
            if (json == null) return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var median = root.TryGetProperty("median", out var medianEl) && medianEl.ValueKind == JsonValueKind.Number ? medianEl.GetDecimal() : 0m;
            if (median <= 0) return null;

            var ebayAvg = root.TryGetProperty("ebay_avg", out var ebayEl) && ebayEl.ValueKind == JsonValueKind.Number ? ebayEl.GetDecimal() : median;
            var swappaAvg = root.TryGetProperty("swappa_avg", out var swappaEl) && swappaEl.ValueKind == JsonValueKind.Number ? swappaEl.GetDecimal() : 0m;
            var sourceNote = root.TryGetProperty("source_note", out var noteEl) && noteEl.ValueKind == JsonValueKind.String
                ? noteEl.GetString() ?? "Gemini estimate" : "Gemini estimate";

            return new DevicePriceResult(ebayAvg, swappaAvg, median, sourceNote);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractJson(string text)
    {
        var clean = text.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("```", string.Empty).Trim();
        var start = clean.IndexOf('{');
        var end = clean.LastIndexOf('}');
        return start >= 0 && end > start ? clean[start..(end + 1)] : null;
    }
}
