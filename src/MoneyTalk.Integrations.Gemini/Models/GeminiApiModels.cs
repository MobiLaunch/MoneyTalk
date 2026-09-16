using System.Text.Json.Serialization;

namespace MoneyTalk.Integrations.Gemini.Models;

internal class GeminiGenerateContentRequest
{
    [JsonPropertyName("system_instruction")] public GeminiContent? SystemInstruction { get; set; }
    [JsonPropertyName("contents")] public List<GeminiContent> Contents { get; set; } = new();
    [JsonPropertyName("generationConfig")] public GeminiGenerationConfig? GenerationConfig { get; set; }
}

internal class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")] public double Temperature { get; set; }
    [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
}

internal class GeminiContent
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("parts")] public List<GeminiPart> Parts { get; set; } = new();
}

internal class GeminiPart
{
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

internal class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; }
    [JsonPropertyName("promptFeedback")] public GeminiPromptFeedback? PromptFeedback { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")] public GeminiContent? Content { get; set; }
    [JsonPropertyName("finishReason")] public string? FinishReason { get; set; }
}

internal class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")] public string? BlockReason { get; set; }
}
