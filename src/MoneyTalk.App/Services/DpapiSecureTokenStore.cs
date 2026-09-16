using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.Services;

/// <summary>Stores OAuth tokens and API keys (Square, QuickBooks, Gemini) encrypted at rest
/// with Windows Data Protection API, scoped to the current Windows user. Chosen over the
/// Credential Locker (<c>PasswordVault</c>) because DPAPI works identically whether or not the
/// app has a packaging identity, which matters since MoneyTalk ships unpackaged.</summary>
public class DpapiSecureTokenStore : ISecureTokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MoneyTalk.SecureTokenStore.v1");
    private readonly object _lock = new();

    public void SaveSecret(string key, string secret)
    {
        lock (_lock)
        {
            var secrets = LoadAll();
            secrets[key] = secret;
            PersistAll(secrets);
        }
    }

    public string? GetSecret(string key)
    {
        lock (_lock)
        {
            return LoadAll().TryGetValue(key, out var value) ? value : null;
        }
    }

    public void DeleteSecret(string key)
    {
        lock (_lock)
        {
            var secrets = LoadAll();
            if (secrets.Remove(key))
                PersistAll(secrets);
        }
    }

    private static Dictionary<string, string> LoadAll()
    {
        if (!File.Exists(AppPaths.SecretsFilePath))
            return new Dictionary<string, string>();

        try
        {
            var encrypted = File.ReadAllBytes(AppPaths.SecretsFilePath);
            var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted) ?? new Dictionary<string, string>();
        }
        catch (CryptographicException)
        {
            // The file is unreadable (different user profile, corrupted, or moved from another
            // machine). Treat it as empty rather than crashing the app on startup.
            return new Dictionary<string, string>();
        }
    }

    private static void PersistAll(Dictionary<string, string> secrets)
    {
        AppPaths.EnsureFoldersExist();
        var plainBytes = JsonSerializer.SerializeToUtf8Bytes(secrets);
        var encrypted = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(AppPaths.SecretsFilePath, encrypted);
    }
}
