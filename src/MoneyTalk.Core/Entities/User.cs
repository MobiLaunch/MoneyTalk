namespace MoneyTalk.Core.Entities;

/// <summary>A local application user. This is a lightweight, single-machine identity model
/// (no external auth provider) intended for small businesses with a handful of people sharing
/// the same install — role gates which actions the UI exposes.</summary>
public class User : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Owner;
    public bool IsActive { get; set; } = true;

    /// <summary>PBKDF2 hash of an optional local PIN/password gate. Null means no local lock.</summary>
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }
}
