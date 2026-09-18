using System.Diagnostics;
using Windows.System;

namespace MoneyTalk.App.Services;

/// <summary>Opens whatever native tool a platform actually offers for backup/restore from a
/// Windows PC. This is deliberately a thin launcher, not an integration: no third-party Windows
/// app can trigger a backup, restore, diagnostic, or OS update on a connected iPhone, Mac, or
/// Chromebook — Apple, Google, and Microsoft don't expose that to outside software. Android is the
/// one platform with an open USB protocol (ADB) this app can genuinely drive; see
/// <see cref="AdbService"/> for that.</summary>
public static class NativeToolLauncherService
{
    private static readonly string[] KnownItunesPaths =
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "iTunes", "iTunes.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "iTunes", "iTunes.exe")
    };

    /// <summary>Launches iTunes if it's installed at one of its usual locations. iTunes (or the
    /// Microsoft Store "Apple Devices" app) is the only way to actually back up an iPhone/iPad
    /// from Windows; if neither is installed, the caller should fall back to
    /// <see cref="OpenFileExplorer"/> so the tech can at least reach the device's photos over MTP,
    /// and tell them to install iTunes or Apple Devices for a real backup.</summary>
    public static bool TryLaunchItunes()
    {
        foreach (var path in KnownItunesPaths)
        {
            if (!File.Exists(path)) continue;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        return false;
    }

    public static void OpenFileExplorer() =>
        Process.Start(new ProcessStartInfo("explorer.exe", "shell:MyComputerFolder") { UseShellExecute = true });

    /// <summary>Opens Windows' own Backup settings page — the real native backup entry point on
    /// Windows 10/11.</summary>
    public static async Task OpenWindowsBackupSettingsAsync() => await Launcher.LaunchUriAsync(new Uri("ms-settings:backup"));

    /// <summary>Chromebooks back up automatically to the signed-in Google account rather than to a
    /// local file — there's no local "run a backup" action, so this opens the Google Account page
    /// where sync/backup status actually lives.</summary>
    public static async Task OpenGoogleAccountAsync() => await Launcher.LaunchUriAsync(new Uri("https://myaccount.google.com/"));
}
