using System.Text.Json;

namespace MoneyTalk.App.Services;

/// <summary>Non-secret app preferences (which company is active, OAuth client ids, sandbox
/// toggles, the chosen Gemini model). Anything sensitive — client secrets, access/refresh
/// tokens, API keys — belongs in <see cref="ISecureTokenStore"/>-backed storage instead.</summary>
public class AppSettings
{
    public Guid? ActiveCompanyId { get; set; }

    public string SquareClientId { get; set; } = string.Empty;
    public string SquareRedirectUri { get; set; } = "moneytalk://oauth/square";
    public bool SquareUseSandbox { get; set; } = true;

    public string QuickBooksClientId { get; set; } = string.Empty;
    public string QuickBooksRedirectUri { get; set; } = "moneytalk://oauth/quickbooks";
    public bool QuickBooksUseSandbox { get; set; } = true;

    public string GeminiModelId { get; set; } = "gemini-2.0-flash";

    public bool HasCompletedOnboarding { get; set; }
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
