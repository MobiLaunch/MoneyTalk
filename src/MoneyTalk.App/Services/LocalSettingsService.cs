using System.Text.Json;

namespace MoneyTalk.App.Services;

/// <summary>Non-secret app preferences (which company is active, OAuth client ids, sandbox
/// toggles, the chosen Gemini model). Anything sensitive — client secrets, access/refresh
/// tokens, API keys — belongs in <see cref="ISecureTokenStore"/>-backed storage instead.</summary>
public class AppSettings
{
    public Guid? ActiveCompanyId { get; set; }

    public string SquareClientId { get; set; } = string.Empty;

    /// <summary>Must be the deployed oauth-relay's <c>/square/callback</c> URL (see /oauth-relay
    /// in the repo) — Square requires a real HTTPS redirect URL registered in its dashboard and
    /// won't accept a custom URI scheme. Left blank until the user configures it in Settings.</summary>
    public string SquareRedirectUri { get; set; } = string.Empty;
    public bool SquareUseSandbox { get; set; } = true;

    public string QuickBooksClientId { get; set; } = string.Empty;

    /// <summary>Must be the deployed oauth-relay's <c>/quickbooks/callback</c> URL — same
    /// reasoning as <see cref="SquareRedirectUri"/>. Intuit only allows localhost for sandbox.</summary>
    public string QuickBooksRedirectUri { get; set; } = string.Empty;
    public bool QuickBooksUseSandbox { get; set; } = true;

    /// <summary>Square Terminal API device id from the last successful "Pair Terminal" in POS —
    /// reused across sessions so the shop doesn't have to re-pair every launch.</summary>
    public string SquareTerminalDeviceId { get; set; } = string.Empty;

    public string GeminiModelId { get; set; } = "gemini-2.0-flash";

    public bool HasCompletedOnboarding { get; set; }

    /// <summary>Windows printer name to send receipts/labels to (see <see cref="Services.PrintService"/>).
    /// Empty means "use the system default printer".</summary>
    public string ReceiptPrinterName { get; set; } = string.Empty;

    /// <summary>App-wide idle screen lock (3-minute idle timeout, matching NovaOps's hardcoded
    /// lock delay) — see <c>MainWindow</c>. Off by default so a fresh install isn't locked out
    /// before a PIN has ever been set.</summary>
    public bool ScreenLockEnabled { get; set; }
}

public class LocalSettingsService
{
    private AppSettings? _cached;

    public AppSettings Load()
    {
        if (_cached != null) return _cached;

        if (File.Exists(AppPaths.SettingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(AppPaths.SettingsFilePath);
                _cached = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                return _cached;
            }
            catch (JsonException)
            {
                // Fall through to a fresh settings file rather than crash on startup.
            }
        }

        _cached = new AppSettings();
        return _cached;
    }

    public void Save(AppSettings settings)
    {
        AppPaths.EnsureFoldersExist();
        _cached = settings;
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.SettingsFilePath, json);
    }
}
