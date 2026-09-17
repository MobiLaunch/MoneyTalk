using System.Text.Json;

namespace MoneyTalk.Core.Accounting;

public record RepairGuideResult(string Title, string Url, string? Difficulty);

/// <summary>Thin wrapper over iFixit's public search API (no API key required). Lives here
/// alongside <see cref="TradeInPriceLookupService"/> for the same reason: a single plain HTTP GET
/// with no OAuth/vendor SDK surface, so a dedicated MoneyTalk.Integrations.* project would be
/// overkill for it.</summary>
public class IFixitClient
{
    private readonly HttpClient _httpClient;

    public IFixitClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<RepairGuideResult>> SearchGuidesAsync(string query, CancellationToken ct = default)
    {
        var results = new List<RepairGuideResult>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var url = $"https://www.ifixit.com/api/2.0/search/{Uri.EscapeDataString(query)}?doctypes=guide";
        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return results;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("results", out var items)) return results;

        foreach (var item in items.EnumerateArray().Take(10))
        {
            var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : null;
            var guideUrl = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(guideUrl)) continue;

            var difficulty = item.TryGetProperty("difficulty", out var difficultyProp) ? difficultyProp.GetString() : null;
            results.Add(new RepairGuideResult(title, guideUrl, difficulty));
        }

        return results;
    }
}
