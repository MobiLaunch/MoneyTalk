using System.Diagnostics;

namespace MoneyTalk.App.Services;

public record AdbDevice(string Serial, string State);

public record AndroidDeviceInfo(string Manufacturer, string Model, string OsVersion, int? BatteryLevelPercent, string StorageSummary);

/// <summary>Drives a locally-installed <c>adb.exe</c> (Android Debug Bridge, part of Google's free
/// Android SDK Platform Tools) to get real device info/diagnostics and take a real backup over
/// USB. Android is the one device-triage platform with an open, documented protocol a third-party
/// Windows app can genuinely use — this shells out to Google's own binary rather than
/// reimplementing the wire protocol, which is both simpler and the standard way every legitimate
/// Android tool does this. MoneyTalk does not bundle platform-tools: install it from
/// https://developer.android.com/tools/releases/platform-tools and either add it to PATH or drop
/// it in %LOCALAPPDATA%\Android\Sdk\platform-tools (the default location the Android SDK installer
/// uses), and enable USB debugging on the device.</summary>
public static class AdbService
{
    public static bool IsAvailable => FindAdbExecutable() != null;

    public static async Task<List<AdbDevice>> ListDevicesAsync(CancellationToken ct = default)
    {
        var (_, stdOut, _) = await RunAdbAsync("devices", ct);
        var devices = new List<AdbDevice>();

        foreach (var line in stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2) devices.Add(new AdbDevice(parts[0], parts[1]));
        }

        return devices;
    }

    public static async Task<AndroidDeviceInfo> GetDeviceInfoAsync(string serial, CancellationToken ct = default)
    {
        var manufacturer = (await GetPropertyAsync(serial, "ro.product.manufacturer", ct)).Trim();
        var model = (await GetPropertyAsync(serial, "ro.product.model", ct)).Trim();
        var osVersion = (await GetPropertyAsync(serial, "ro.build.version.release", ct)).Trim();

        var (_, batteryDump, _) = await RunAdbAsync($"-s {serial} shell dumpsys battery", ct);
        var batteryLevel = ParseBatteryLevel(batteryDump);

        var (_, storageDump, _) = await RunAdbAsync($"-s {serial} shell df /data", ct);
        var storageSummary = ParseStorageSummary(storageDump);

        return new AndroidDeviceInfo(manufacturer, model, osVersion, batteryLevel, storageSummary);
    }

    /// <summary>Real <c>adb backup</c>. Note this covers app data the app has opted into (and
    /// system settings with <c>-all</c>) — Google restricted third-party app data backup via ADB
    /// starting with Android 12 for security reasons, so results vary by OS version and by app;
    /// this surfaces whatever adb.exe actually reports rather than pretending it always fully
    /// succeeds.</summary>
    public static async Task<string> BackupAsync(string serial, string destinationFilePath, CancellationToken ct = default)
    {
        var (exitCode, stdOut, stdErr) = await RunAdbAsync($"-s {serial} backup -f \"{destinationFilePath}\" -all", ct);
        if (exitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr);
        return stdOut;
    }

    private static async Task<string> GetPropertyAsync(string serial, string propertyName, CancellationToken ct)
    {
        var (_, stdOut, _) = await RunAdbAsync($"-s {serial} shell getprop {propertyName}", ct);
        return stdOut;
    }

    private static int? ParseBatteryLevel(string dumpsysOutput)
    {
        foreach (var line in dumpsysOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("level:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(trimmed["level:".Length..].Trim(), out var level))
                return level;
        }
        return null;
    }

    private static string ParseStorageSummary(string dfOutput)
    {
        var lines = dfOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // `df` prints a header row then one data row: Filesystem Size Used Avail Use% Mounted-on
        if (lines.Length < 2) return "Unknown";
        var columns = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return columns.Length >= 4 ? $"{columns[2]} used of {columns[1]} ({columns[3]} free)" : "Unknown";
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAdbAsync(string arguments, CancellationToken ct)
    {
        var adbPath = FindAdbExecutable()
            ?? throw new InvalidOperationException(
                "adb.exe wasn't found. Install Android SDK Platform Tools " +
                "(https://developer.android.com/tools/releases/platform-tools) and either add it to your " +
                "PATH or place it at %LOCALAPPDATA%\\Android\\Sdk\\platform-tools.");

        var psi = new ProcessStartInfo(adbPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start adb.exe.");
        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }

    private static string? FindAdbExecutable()
    {
        const string exeName = "adb.exe";

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(directory, exeName);
            if (File.Exists(candidate)) return candidate;
        }

        var defaultSdkPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", exeName);
        return File.Exists(defaultSdkPath) ? defaultSdkPath : null;
    }
}
