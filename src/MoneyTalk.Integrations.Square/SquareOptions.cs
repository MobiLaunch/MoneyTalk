namespace MoneyTalk.Integrations.Square;

/// <summary>Register a MoneyTalk OAuth application at https://developer.squareup.com/apps to
/// obtain a client id/secret. Point <see cref="ApiBaseUrl"/> at the sandbox host while testing.
/// <see cref="RedirectUri"/> must be a deployed instance of the oauth-relay in /oauth-relay,
/// registered as this app's Redirect URL in Square's dashboard — Square requires a real HTTPS
/// endpoint, not a custom URI scheme.</summary>
public class SquareOptions
{
    public string ApiBaseUrl { get; set; } = "https://connect.squareup.com";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "2024-08-21";
}
