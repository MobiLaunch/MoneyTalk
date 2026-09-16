namespace MoneyTalk.App.Services;

/// <summary>Central place for every on-disk location MoneyTalk touches. Kept in one file so
/// "where does the app store its data" is a one-line answer for support/backup purposes.</summary>
public static class AppPaths
{
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoneyTalk");

    public static string DatabaseFilePath => Path.Combine(RootFolder, "moneytalk.db");
    public static string SettingsFilePath => Path.Combine(RootFolder, "settings.json");
    public static string SecretsFilePath => Path.Combine(RootFolder, "secrets.dat");
    public static string AttachmentsFolder => Path.Combine(RootFolder, "Attachments");

    public static void EnsureFoldersExist()
    {
        Directory.CreateDirectory(RootFolder);
        Directory.CreateDirectory(AttachmentsFolder);
    }
}
