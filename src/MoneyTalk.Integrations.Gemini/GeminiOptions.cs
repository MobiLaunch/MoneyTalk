namespace MoneyTalk.Integrations.Gemini;

/// <summary>Get an API key at https://aistudio.google.com/apikey. The key is supplied per-call
/// (see <see cref="Core.Interfaces.Integrations.IGeminiClient"/>) rather than stored on this
/// options object, since it lives in the OS credential vault, not app configuration.</summary>
public class GeminiOptions
{
    public string ApiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Swap this in Settings if Google renames/retires a model; the client never
    /// hardcodes a single model id anywhere else. Google has cycled through several flash-tier
    /// model names in a single year (2.0 → 2.5 → 3.x) — when this 404s with "model no longer
    /// available", update it in Integrations → Gemini AI Advisor rather than editing code.</summary>
    public string ModelId { get; set; } = "gemini-2.5-flash";

    public double Temperature { get; set; } = 0.4;
    public int MaxOutputTokens { get; set; } = 2048;
}
