namespace MoneyTalk.Core.Entities;

/// <summary>Non-secret metadata about a connection to an external system. The actual OAuth
/// access/refresh tokens are never stored in the SQLite database — they live in the Windows
/// Credential Locker via <c>ISecureTokenStore</c>, keyed by <see cref="CredentialKey"/>. This
/// keeps a copy/leak of the .db file from also leaking live API credentials.</summary>
public class IntegrationConnection : CompanyOwnedEntity
{
    public IntegrationProvider Provider { get; set; }
    public bool IsConnected { get; set; }

    /// <summary>Key used to look the tokens up in <c>ISecureTokenStore</c>, e.g.
    /// "square:{companyId}" or "qbo:{companyId}".</summary>
    public string CredentialKey { get; set; } = string.Empty;

    public DateTime? TokenExpiresAtUtc { get; set; }

    /// <summary>QuickBooks Online "realm id" (company id) associated with this connection.</summary>
    public string? RealmId { get; set; }

    /// <summary>Square location id (or comma-separated list) to sync from.</summary>
    public string? ExternalAccountId { get; set; }

    public DateTime? LastSyncAtUtc { get; set; }
    public string? LastSyncStatus { get; set; }

    /// <summary>Small JSON blob for provider-specific, non-secret settings (e.g. which bank
    /// account to post Square deposits into, sync frequency).</summary>
    public string? SettingsJson { get; set; }
}
