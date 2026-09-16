namespace MoneyTalk.App.Services;

/// <summary>Well-known keys used with <see cref="MoneyTalk.Core.Interfaces.ISecureTokenStore"/>.
/// Square/QuickBooks keys are namespaced per company since a machine could in principle manage
/// more than one company's books over time; the Gemini API key is a single account-wide key.</summary>
public static class SecretKeys
{
    public const string GeminiApiKey = "gemini:apiKey";

    public static string SquareAccessToken(Guid companyId) => $"square:{companyId}:accessToken";
    public static string SquareRefreshToken(Guid companyId) => $"square:{companyId}:refreshToken";
    public static string SquareClientSecret => "square:clientSecret";

    public static string QuickBooksAccessToken(Guid companyId) => $"qbo:{companyId}:accessToken";
    public static string QuickBooksRefreshToken(Guid companyId) => $"qbo:{companyId}:refreshToken";
    public static string QuickBooksClientSecret => "qbo:clientSecret";
}
