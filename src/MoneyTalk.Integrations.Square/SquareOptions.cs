namespace MoneyTalk.Integrations.Square;

/// <summary>Register a MoneyTalk OAuth application at https://developer.squareup.com/apps to
/// obtain a client id/secret. Point <see cref="ApiBaseUrl"/> at the sandbox host while testing.</summary>
public class SquareOptions
{
    public string ApiBaseUrl { get; set; } = "https://connect.squareup.com";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "moneytalk://oauth/square";
    public string ApiVersion { get; set; } = "2024-08-21";
}
