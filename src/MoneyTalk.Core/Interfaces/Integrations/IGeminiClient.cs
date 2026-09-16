namespace MoneyTalk.Core.Interfaces.Integrations;

public record GeminiChatTurn(bool IsUser, string Text);

/// <summary>Client for Google's Gemini generative-language API, used to power the in-app
/// financial advisor chat and one-off narrative insights (e.g. "explain this month's P&L").</summary>
public interface IGeminiClient
{
    Task<string> GenerateContentAsync(
        string apiKey,
        string systemInstruction,
        IReadOnlyList<GeminiChatTurn> history,
        string userMessage,
        CancellationToken ct = default);
}
