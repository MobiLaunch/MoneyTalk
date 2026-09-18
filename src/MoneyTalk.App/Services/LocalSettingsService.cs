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

    public string GeminiModelId { get; set; } = "gemini-2.5-flash";

    public bool HasCompletedOnboarding { get; set; }

    /// <summary>Windows printer to send receipts to (see <see cref="Services.PrintService"/>).
    /// Empty means "use the system default printer". Deliberately separate from
    /// <see cref="LabelPrinterName"/> — a repair shop's receipt printer (often a thermal
    /// till-roll printer at the register) and its label printer (often a dedicated barcode-label
    /// printer at the bench) are normally two different physical devices.</summary>
    public string ReceiptPrinterName { get; set; } = string.Empty;

    /// <summary>Windows printer to send item/ticket labels to. See <see cref="ReceiptPrinterName"/>
    /// for why this is a separate setting.</summary>
    public string LabelPrinterName { get; set; } = string.Empty;

    /// <summary>Receipt template: optional line printed under the "Customer:" line, and optional
    /// line printed at the very end — see the Receipt Editor page.</summary>
    public string ReceiptHeaderText { get; set; } = string.Empty;
    public string ReceiptFooterText { get; set; } = "Thank you for your business!";

    /// <summary>Label template: which fields appear under the item name, and which barcode symbology
    /// to encode the SKU as — see the Label Editor page. Stored as the enum's name rather than the
    /// enum itself since <see cref="AppSettings"/> is plain JSON.</summary>
    public bool LabelShowSku { get; set; } = true;
    public bool LabelShowPrice { get; set; } = true;
    public string LabelBarcodeFormat { get; set; } = nameof(global::MoneyTalk.App.Services.LabelBarcodeFormat.Code128); // "Code128" or "QrCode"

    /// <summary>App-wide idle screen lock (3-minute idle timeout, matching NovaOps's hardcoded
    /// lock delay) — see <c>MainWindow</c>. Off by default so a fresh install isn't locked out
    /// before a PIN has ever been set.</summary>
    public bool ScreenLockEnabled { get; set; }

    /// <summary>SMTP settings for outbound notification emails — the password is stored via the
    /// secure token store instead, same as every other credential in this app.</summary>
    public string EmailHost { get; set; } = string.Empty;
    public int EmailPort { get; set; } = 587;
    public string EmailUsername { get; set; } = string.Empty;
    public bool EmailUseSsl { get; set; } = true;
    public string EmailFromAddress { get; set; } = string.Empty;
    public string EmailFromName { get; set; } = "MoneyTalk";
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
