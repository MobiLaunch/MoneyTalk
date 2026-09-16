namespace MoneyTalk.Integrations.QuickBooks;

/// <summary>Register a MoneyTalk app at https://developer.intuit.com/app/developer/myapps to get
/// a client id/secret. Toggle <see cref="UseSandbox"/> off once you're ready to connect to a
/// real QuickBooks Online company.</summary>
public class QuickBooksOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "moneytalk://oauth/quickbooks";
    public bool UseSandbox { get; set; } = true;
    public string MinorVersion { get; set; } = "70";

    public string AuthorizationBaseUrl => "https://appcenter.intuit.com/connect/oauth2";
    public string TokenUrl => "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";
    public string ApiBaseUrl => UseSandbox
        ? "https://sandbox-quickbooks.api.intuit.com"
        : "https://quickbooks.api.intuit.com";
}
