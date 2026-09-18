namespace MoneyTalk.Core.Entities;

/// <summary>Tracks a device passing through the shop's backup/diagnose/restore workflow — iPhone,
/// Android phone, Chromebook, Windows PC, or Mac. This is a tracking record, not a data pipe: for
/// every platform except Android, MoneyTalk has no way to actually run a backup, diagnostic, or
/// OS update itself (no third-party Windows app can trigger those on a connected iPhone/Mac/
/// Chromebook — see <c>NativeToolLauncherService</c>), so those fields are filled in by the tech
/// after using whatever the platform's own tool reports. Android is the one platform with an open
/// USB protocol (ADB) this app can genuinely drive — see <c>AdbService</c>.</summary>
public class DeviceTriageRecord : CompanyOwnedEntity
{
    public Guid? CustomerId { get; set; }
    public Guid? RepairTicketId { get; set; }

    public DeviceTriagePlatform Platform { get; set; }
    public string DeviceLabel { get; set; } = string.Empty;
    public string? SerialOrIdentifier { get; set; }
    public DeviceTriageStatus Status { get; set; } = DeviceTriageStatus.Intake;

    public bool BackupCompleted { get; set; }
    public bool DiagnosticsCompleted { get; set; }
    public bool RestoreCompleted { get; set; }

    /// <summary>For Android, populated automatically from <c>ro.build.version.release</c> via ADB;
    /// for every other platform, entered manually by the tech.</summary>
    public string? OsVersionDetected { get; set; }

    /// <summary>Free text — for Android this is pre-filled from real ADB diagnostics (battery
    /// level, storage free/total); for other platforms the tech pastes in what the platform's own
    /// diagnostic tool reported.</summary>
    public string? DiagnosticsSummary { get; set; }

    public string? Notes { get; set; }
}
