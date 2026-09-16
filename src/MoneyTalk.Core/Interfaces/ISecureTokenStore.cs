namespace MoneyTalk.Core.Interfaces;

/// <summary>Abstraction over an OS-level secret store. On Windows this is implemented with the
/// Credential Locker (<c>Windows.Security.Credentials.PasswordVault</c>) so OAuth tokens and API
/// keys for Square/QuickBooks/Gemini never touch the SQLite database or plain-text config.</summary>
public interface ISecureTokenStore
{
    void SaveSecret(string key, string secret);
    string? GetSecret(string key);
    void DeleteSecret(string key);
}
