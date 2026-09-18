using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.App.Services;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class DeviceTriageRow
{
    public Guid Id { get; init; }
    public string Platform { get; init; } = string.Empty;
    public string DeviceLabel { get; init; } = string.Empty;
    public string? SerialOrIdentifier { get; init; }
    public Guid? CustomerId { get; init; }
    public string CustomerName { get; init; } = "—";
    public string Status { get; init; } = string.Empty;
    public bool BackupCompleted { get; init; }
    public bool DiagnosticsCompleted { get; init; }
    public bool RestoreCompleted { get; init; }
    public string? OsVersionDetected { get; init; }
    public string? DiagnosticsSummary { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Tracks devices moving through the shop's backup/diagnose/restore workflow. See
/// <see cref="DeviceTriageRecord"/>'s doc comment for the honest scope of what this actually
/// automates (Android via <see cref="AdbService"/>) versus what's a checklist + a launch button
/// for the platform's own tool (<see cref="NativeToolLauncherService"/>).</summary>
public partial class DeviceTriageViewModel : ViewModelBase
{
    public static IReadOnlyList<string> PlatformOptions { get; } = Enum.GetNames<DeviceTriagePlatform>();
    public static IReadOnlyList<string> StatusOptions { get; } = Enum.GetNames<DeviceTriageStatus>();

    public ObservableCollection<DeviceTriageRow> Records { get; } = new();
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<AdbDevice> AndroidDevices { get; } = new();

    [ObservableProperty] private DeviceTriageRow? selectedRecord;
    [ObservableProperty] private AdbDevice? selectedAndroidDevice;
    [ObservableProperty] private string? androidStatusMessage;
    [ObservableProperty] private bool isAdbAvailable;

    public DeviceTriageViewModel(Func<IUnitOfWork> unitOfWorkFactory, LocalSettingsService settingsService)
        : base(unitOfWorkFactory, settingsService)
    {
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsAdbAvailable = AdbService.IsAvailable;

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId && c.IsActive);
            Customers.Clear();
            foreach (var customer in customers.OrderBy(c => c.Name)) Customers.Add(customer);
            var customersById = customers.ToDictionary(c => c.Id);

            var records = await uow.DeviceTriageRecords.FindAsync(d => d.CompanyId == companyId);
            Records.Clear();
            foreach (var record in records.OrderByDescending(r => r.CreatedAtUtc))
            {
                Records.Add(new DeviceTriageRow
                {
                    Id = record.Id,
                    Platform = record.Platform.ToString(),
                    DeviceLabel = record.DeviceLabel,
                    SerialOrIdentifier = record.SerialOrIdentifier,
                    CustomerId = record.CustomerId,
                    CustomerName = record.CustomerId.HasValue && customersById.TryGetValue(record.CustomerId.Value, out var customer)
                        ? customer.Name : "—",
                    Status = record.Status.ToString(),
                    BackupCompleted = record.BackupCompleted,
                    DiagnosticsCompleted = record.DiagnosticsCompleted,
                    RestoreCompleted = record.RestoreCompleted,
                    OsVersionDetected = record.OsVersionDetected,
                    DiagnosticsSummary = record.DiagnosticsSummary,
                    Notes = record.Notes
                });
            }
        });
    }

    public async Task<bool> AddRecordAsync(string platformText, string deviceLabel, string? serial, Guid? customerId, string? notes)
    {
        if (string.IsNullOrWhiteSpace(deviceLabel))
        {
            ErrorMessage = "Enter a device label first.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var record = new DeviceTriageRecord
            {
                CompanyId = ActiveCompanyId,
                Platform = Enum.Parse<DeviceTriagePlatform>(platformText),
                DeviceLabel = deviceLabel.Trim(),
                SerialOrIdentifier = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim(),
                CustomerId = customerId,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };
            await uow.DeviceTriageRecords.AddAsync(record);
            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    public async Task<bool> EditRecordAsync(
        Guid recordId, string platformText, string deviceLabel, string? serial, Guid? customerId, string statusText,
        bool backupCompleted, bool diagnosticsCompleted, bool restoreCompleted,
        string? osVersionDetected, string? diagnosticsSummary, string? notes)
    {
        if (string.IsNullOrWhiteSpace(deviceLabel))
        {
            ErrorMessage = "Enter a device label first.";
            return false;
        }

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var record = await uow.DeviceTriageRecords.GetByIdAsync(recordId)
                ?? throw new InvalidOperationException("This record no longer exists.");

            record.Platform = Enum.Parse<DeviceTriagePlatform>(platformText);
            record.DeviceLabel = deviceLabel.Trim();
            record.SerialOrIdentifier = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim();
            record.CustomerId = customerId;
            record.Status = Enum.Parse<DeviceTriageStatus>(statusText);
            record.BackupCompleted = backupCompleted;
            record.DiagnosticsCompleted = diagnosticsCompleted;
            record.RestoreCompleted = restoreCompleted;
            record.OsVersionDetected = string.IsNullOrWhiteSpace(osVersionDetected) ? null : osVersionDetected.Trim();
            record.DiagnosticsSummary = string.IsNullOrWhiteSpace(diagnosticsSummary) ? null : diagnosticsSummary.Trim();
            record.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            await uow.SaveChangesAsync();
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }

    /// <summary>Opens whatever native tool the record's platform actually offers — see
    /// <see cref="NativeToolLauncherService"/> for why this varies so much by platform.</summary>
    [RelayCommand]
    private async Task LaunchNativeToolAsync(string? platformText)
    {
        if (string.IsNullOrEmpty(platformText) || !Enum.TryParse<DeviceTriagePlatform>(platformText, out var platform))
        {
            ErrorMessage = "Select a device first.";
            return;
        }
        ErrorMessage = null;

        try
        {
            switch (platform)
            {
                case DeviceTriagePlatform.Ios:
                    if (!NativeToolLauncherService.TryLaunchItunes())
                    {
                        NativeToolLauncherService.OpenFileExplorer();
                        ErrorMessage = "iTunes isn't installed — opened File Explorer instead (you can reach photos over MTP, but a real backup needs iTunes or the \"Apple Devices\" app from the Microsoft Store).";
                    }
                    break;

                case DeviceTriagePlatform.Android:
                    NativeToolLauncherService.OpenFileExplorer();
                    break;

                case DeviceTriagePlatform.ChromeOs:
                    await NativeToolLauncherService.OpenGoogleAccountAsync();
                    break;

                case DeviceTriagePlatform.Windows:
                    await NativeToolLauncherService.OpenWindowsBackupSettingsAsync();
                    break;

                case DeviceTriagePlatform.MacOs:
                    ErrorMessage = "A Mac's Time Machine backup can only be run on the Mac itself — there's no way to trigger it from this Windows PC. Connect the Mac's backup drive and run Time Machine directly on the Mac.";
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't open that tool: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAndroidDevicesAsync()
    {
        AndroidStatusMessage = "Scanning for connected Android devices...";
        try
        {
            var devices = await AdbService.ListDevicesAsync();
            AndroidDevices.Clear();
            foreach (var device in devices) AndroidDevices.Add(device);
            SelectedAndroidDevice = AndroidDevices.FirstOrDefault();

            AndroidStatusMessage = devices.Count == 0
                ? "No Android devices found. Make sure USB debugging is enabled and the device is plugged in, then authorize this computer on the device's screen if prompted."
                : $"{devices.Count} device(s) found.";
        }
        catch (Exception ex)
        {
            AndroidStatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RunAndroidDiagnosticsAsync()
    {
        if (SelectedRecord == null) { AndroidStatusMessage = "Select a device triage record first."; return; }
        if (SelectedAndroidDevice == null) { AndroidStatusMessage = "Select a connected Android device first."; return; }
        if (SelectedAndroidDevice.State != "device")
        {
            AndroidStatusMessage = $"Device is \"{SelectedAndroidDevice.State}\", not ready — authorize USB debugging on the device's screen.";
            return;
        }

        var recordId = SelectedRecord.Id;
        await RunBusyAsync(async () =>
        {
            var info = await AdbService.GetDeviceInfoAsync(SelectedAndroidDevice.Serial);

            using var uow = NewUnitOfWork();
            var record = await uow.DeviceTriageRecords.GetByIdAsync(recordId)
                ?? throw new InvalidOperationException("This record no longer exists.");
            record.OsVersionDetected = $"Android {info.OsVersion}";
            record.DiagnosticsSummary = $"{info.Manufacturer} {info.Model} · Battery " +
                $"{(info.BatteryLevelPercent.HasValue ? $"{info.BatteryLevelPercent}%" : "unknown")} · Storage: {info.StorageSummary}";
            record.DiagnosticsCompleted = true;
            await uow.SaveChangesAsync();

            AndroidStatusMessage = "Diagnostics saved to the selected record.";
        });

        await LoadAsync();
    }

    public async Task<bool> BackUpAndroidDeviceAsync(string destinationFilePath)
    {
        if (SelectedRecord == null) { AndroidStatusMessage = "Select a device triage record first."; return false; }
        if (SelectedAndroidDevice == null) { AndroidStatusMessage = "Select a connected Android device first."; return false; }

        var recordId = SelectedRecord.Id;
        var success = false;
        await RunBusyAsync(async () =>
        {
            AndroidStatusMessage = "Backing up — this can take a while and may prompt for a backup password on the device's screen...";
            await AdbService.BackupAsync(SelectedAndroidDevice.Serial, destinationFilePath);

            using var uow = NewUnitOfWork();
            var record = await uow.DeviceTriageRecords.GetByIdAsync(recordId)
                ?? throw new InvalidOperationException("This record no longer exists.");
            record.BackupCompleted = true;
            await uow.SaveChangesAsync();

            AndroidStatusMessage = $"Backup saved to {destinationFilePath}.";
            success = true;
        });

        if (success) await LoadAsync();
        return success;
    }
}
