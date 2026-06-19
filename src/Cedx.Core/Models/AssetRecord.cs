using System.Globalization;

namespace Cedx.Core.Models;

public sealed class AssetRecord
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
    public string RawContent { get; set; } = string.Empty;

    public SystemInfo System { get; set; } = new();
    public NetworkInfo Network { get; set; } = new();
    public OsInfo Os { get; set; } = new();
    public HardwareInfo Hardware { get; set; } = new();
    public SoftwareInfo Software { get; set; } = new();

    public IReadOnlyList<CredentialEntry> StoredCredentials { get; set; } = [];
    public IReadOnlyList<string> SharedFolders { get; set; } = [];
    public IReadOnlyList<SmbCredentialEntry> SmbCredentials { get; set; } = [];
    public IReadOnlyList<StorageDevice> LocalDisks { get; set; } = [];
    public IReadOnlyList<BitLockerVolume> BitLockerStatus { get; set; } = [];
    public IReadOnlyList<string> ParseWarnings { get; set; } = [];

    public string WinRmCommand { get; set; } = string.Empty;
    public ScanStatus OnlineStatus { get; set; } = ScanStatus.Unknown;

    public string Hostname => System.Hostname;
    public string IpAddress => Network.IpAddress;
    public string MacAddress => Network.MacAddress;
    public string PcDomain => System.PcDomain;
    public string WindowsAccount => System.WindowsAccount;
    public string AnyDeskId => Network.AnyDeskId;
    public string OsVersion => Os.Version;
    public string Manufacturer => System.Manufacturer;
    public string Model => System.Model;
    public string SerialNumber => System.SerialNumber;
    public string Cpu => Hardware.Cpu;
    public string Gpu => Hardware.Gpu;
    public string Antivirus => Software.Antivirus;
    public string OfficeVersion => Software.OfficeVersion;
    public double? RamGb => Hardware.RamGb;
    public double? CDriveFreeGb => LocalDisks.FirstOrDefault(d => d.DriveLetter.Equals("C:", StringComparison.OrdinalIgnoreCase))?.FreeGb;

    public string RamDisplay => RamGb is double value ? value.ToString("0.#", CultureInfo.InvariantCulture) + " GB" : string.Empty;
    public string CDriveFreeDisplay => CDriveFreeGb is double value ? value.ToString("0.#", CultureInfo.InvariantCulture) + " GB" : string.Empty;
    public string AnyDeskDisplay => string.IsNullOrWhiteSpace(AnyDeskId) ? string.Empty : AnyDeskId;
    public string AnyDeskActionText => HasAnyDesk ? $"Connect {AnyDeskId}" : "No AnyDesk";
    public string RemoteAccessStatus => HasAnyDesk ? "Ready" : "No ID";
    public string CDriveTileDisplay => CDriveFreeGb is double value ? value.ToString("0.#", CultureInfo.InvariantCulture) + " GB" : "N/A";
    public string StorageHealthDisplay => HasLowStorage ? "Low C:" : "Storage OK";
    public string LocalDiskSummary => LocalDisks.Count == 0
        ? "No local disk data"
        : string.Join("; ", LocalDisks.Select(d => string.IsNullOrWhiteSpace(d.DriveLetter)
            ? d.Raw
            : $"{d.DriveLetter} {d.FreeGb?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?"}/{d.TotalGb?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?"} GB free"));
    public string PrinterSummary => Software.InstalledPrinters.Count == 0
        ? "No printers parsed"
        : string.Join("; ", Software.InstalledPrinters.Select(printer => printer.Name));
    public string BitLockerSummary => BitLockerStatus.Count == 0
        ? "No BitLocker rows"
        : string.Join("; ", BitLockerStatus.Select(volume => volume.Raw));
    public string CredentialSummary => StoredCredentials.Count == 0
        ? "No stored credentials parsed"
        : string.Join("; ", StoredCredentials.Select(entry => string.IsNullOrWhiteSpace(entry.Target) ? entry.Raw : $"{entry.Target} ({entry.User})"));
    public string SmbCredentialSummary => SmbCredentials.Count == 0
        ? "No SMB credential rows"
        : string.Join("; ", SmbCredentials.Select(entry => string.IsNullOrWhiteSpace(entry.NasIp) ? entry.Raw : $"{entry.NasIp} {entry.StoredUser} {entry.ActiveConnection}"));
    public string SharedFolderSummary => SharedFolders.Count == 0 ? "No shared folders parsed" : string.Join("; ", SharedFolders);
    public string InstalledProgramCountDisplay => Software.InstalledPrograms.Count == 0 ? "No installed-program list" : Software.InstalledPrograms.Count.ToString(CultureInfo.InvariantCulture) + " programs";
    public string InstalledProgramPreview => Software.InstalledPrograms.Count == 0
        ? "No installed-program list parsed"
        : string.Join("; ", Software.InstalledPrograms.Take(12));
    public string NmapScanOutputDisplay => string.IsNullOrWhiteSpace(Network.NmapScanOutput)
        ? "Run Scan Status to populate Nmap output."
        : Network.NmapScanOutput;
    public string LastRebootOrUptime => string.IsNullOrWhiteSpace(Os.LastRebootTime) ? Os.SystemUptime : $"{Os.LastRebootTime} / {Os.SystemUptime}".Trim(' ', '/');
    public string WindowsUserDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(WindowsAccount))
            {
                return string.Empty;
            }

            var separatorIndex = WindowsAccount.LastIndexOf('\\');
            return separatorIndex >= 0 && separatorIndex < WindowsAccount.Length - 1
                ? WindowsAccount[(separatorIndex + 1)..]
                : WindowsAccount;
        }
    }

    public string OsShortDisplay
    {
        get
        {
            if (OsVersion.Contains("Windows 11", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows 11";
            }

            if (OsVersion.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows 10";
            }

            if (OsVersion.Contains("Windows 8", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows 8";
            }

            if (OsVersion.Contains("Windows 7", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows 7";
            }

            if (OsVersion.Contains("Windows Server", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows Server";
            }

            return OsVersion;
        }
    }

    public bool HasAnyDesk => !string.IsNullOrWhiteSpace(AnyDeskId) && !AnyDeskId.Equals("N/A", StringComparison.OrdinalIgnoreCase) && !AnyDeskId.Equals("Not Found", StringComparison.OrdinalIgnoreCase);
    public bool HasLowStorage => CDriveFreeGb is double freeGb && freeGb < 10d;
    public bool HasStoredCredentials => StoredCredentials.Count > 0;
    public bool HasBitLockerOff => BitLockerStatus.Any(v => v.Protection.IndexOf("Off", StringComparison.OrdinalIgnoreCase) >= 0 || v.Raw.IndexOf("Protection: Off", StringComparison.OrdinalIgnoreCase) >= 0);
}

public sealed class NetworkInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string PcDomain { get; set; } = string.Empty;
    public string AnyDeskId { get; set; } = string.Empty;
    public string NetworkMode { get; set; } = string.Empty;
    public string DnsServers { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public string NmapScanOutput { get; set; } = string.Empty;
    public string NmapLastScanned { get; set; } = string.Empty;
}

public sealed class OsInfo
{
    public string Version { get; set; } = string.Empty;
    public string InstallDate { get; set; } = string.Empty;
    public string LastRebootTime { get; set; } = string.Empty;
    public string SystemUptime { get; set; } = string.Empty;
    public string Activation { get; set; } = string.Empty;
    public string WindowsKey { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}

public sealed class HardwareInfo
{
    public string Cpu { get; set; } = string.Empty;
    public string RamRaw { get; set; } = string.Empty;
    public double? RamGb { get; set; }
    public string Gpu { get; set; } = string.Empty;
    public string MonitorModel { get; set; } = string.Empty;
}

public sealed class SystemInfo
{
    public string Hostname { get; set; } = string.Empty;
    public string PcDomain { get; set; } = string.Empty;
    public string WindowsAccount { get; set; } = string.Empty;
    public string UserEmails { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
}

public sealed class SoftwareInfo
{
    public string OfficeVersion { get; set; } = string.Empty;
    public string OfficeActivation { get; set; } = string.Empty;
    public string Antivirus { get; set; } = string.Empty;
    public string AdobeAutodesk { get; set; } = string.Empty;
    public string LocalUsers { get; set; } = string.Empty;
    public IReadOnlyList<PrinterEntry> InstalledPrinters { get; set; } = [];
    public IReadOnlyList<string> InstalledPrograms { get; set; } = [];
}

public sealed class StorageDevice
{
    public string DriveLetter { get; set; } = string.Empty;
    public double? TotalGb { get; set; }
    public double? FreeGb { get; set; }
    public string DriveType { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
}

public sealed class CredentialEntry
{
    public string Target { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
}

public sealed class SmbCredentialEntry
{
    public string NasIp { get; set; } = string.Empty;
    public string StoredUser { get; set; } = string.Empty;
    public string ActiveConnection { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
}

public sealed class PrinterEntry
{
    public string Name { get; set; } = string.Empty;
}

public sealed class BitLockerVolume
{
    public string Volume { get; set; } = string.Empty;
    public string Protection { get; set; } = string.Empty;
    public string Encryption { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
}
