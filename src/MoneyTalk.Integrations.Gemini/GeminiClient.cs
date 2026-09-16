using System.Net.Http.Json;
using MoneyTalk.Core.Interfaces.Integrations;
using MoneyTalk.Integrations.Gemini.Models;

namespace MoneyTalk.Integrations.Gemini;

/// <summary>Talks to Google's Gemini API (https://ai.google.dev/api/generate-content) to power
/// MoneyTalk's financial-advisor chat. The API key travels as a query-string parameter per
/// Google's documented REST contract; it is read once from <c>ISecureTokenStore</c> by the
/// caller and never logged or persisted here.</summary>
public class GeminiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiClient(HttpClient httpClient, GeminiOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<string> GenerateContentAsync(
        string apiKey,
        string systemInstruction,
        IReadOnlyList<GeminiChatTurn> history,
        string userMessage,
        CancellationToken ct = default)
    {
        var request = new GeminiGenerateContentRequest
        {
            SystemInstruction = new GeminiContent { Parts = { new GeminiPart { Text = systemInstruction } } },
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = _options.Temperature,
                MaxOutputTokens = _options.MaxOutputTokens
            }
        };

        foreach (var turn in history)
            request.Contents.Add(new GeminiContent
            {
                Role = turn.IsUser ? "user" : "model",
                Parts = { new GeminiPart { Text = turn.Text } }
            });

        request.Contents.Add(new GeminiContent { Role = "user", Parts = { new GeminiPart { Text = userMessage } } });

        var url = $"{_options.ApiBaseUrl}/models/{_options.ModelId}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        using var response = await _httpClient.PostAsJsonAsync(url, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Gemini API call failed ({(int)response.StatusCode} {response.StatusCode}): {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Gemini API returned an empty response.");

        if (payload.PromptFeedback?.BlockReason != null)
            throw new InvalidOperationException($"Gemini blocked this request: {payload.PromptFeedback.BlockReason}");

        var text = payload.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini did not return any content for this request.");

        return text;
    }
}
