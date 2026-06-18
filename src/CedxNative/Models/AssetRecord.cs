using System.Text;

namespace CedxNative.Models;

public sealed class AssetRecord
{
    public string SourceFilePath { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string PcDomain { get; set; } = string.Empty;
    public string AnyDeskId { get; set; } = string.Empty;
    public string WindowsAccount { get; set; } = string.Empty;
    public string UserEmails { get; set; } = string.Empty;

    public string OsVersion { get; set; } = string.Empty;
    public string OsInstallDate { get; set; } = string.Empty;
    public string LastRebootTime { get; set; } = string.Empty;
    public string SystemUptime { get; set; } = string.Empty;
    public string OsActivation { get; set; } = string.Empty;
    public string WindowsLanguage { get; set; } = string.Empty;
    public string WindowsKey { get; set; } = string.Empty;

    public string Cpu { get; set; } = string.Empty;
    public double? RamGb { get; set; }
    public string Gpu { get; set; } = string.Empty;
    public string MonitorModel { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;

    public string NetworkMode { get; set; } = string.Empty;
    public string DnsServers { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public string OnlineStatus { get; set; } = "unknown";

    public string OfficeVersion { get; set; } = string.Empty;
    public string OfficeActivation { get; set; } = string.Empty;
    public string Antivirus { get; set; } = string.Empty;
    public string AdobeAutodesk { get; set; } = string.Empty;
    public string LocalUsers { get; set; } = string.Empty;

    public List<string> StoredCredentials { get; } = [];
    public List<string> SharedFolders { get; } = [];
    public List<string> InstalledPrinters { get; } = [];
    public List<string> BitLockerStatus { get; } = [];
    public List<string> ParseWarnings { get; } = [];
    public List<StorageDevice> LocalDisks { get; } = [];
    public List<SmbCredentialEntry> SmbCredentials { get; } = [];

    public string WinRmCommand { get; set; } = string.Empty;

    public bool HasAnyDeskId => !string.IsNullOrWhiteSpace(AnyDeskId) && !AnyDeskId.Equals("N/A", StringComparison.OrdinalIgnoreCase);
    public bool HasStoredCredentials => StoredCredentials.Count > 0;
    public bool HasBitLockerOff => BitLockerStatus.Any(x => x.Contains("Protection: Off", StringComparison.OrdinalIgnoreCase));
    public double? CDriveFreeGb => LocalDisks.FirstOrDefault(d => d.DriveLetter.Equals("C", StringComparison.OrdinalIgnoreCase))?.FreeGb;
    public string RamDisplay => RamGb.HasValue ? $"{RamGb:0.#} GB" : string.Empty;
    public string CDriveFreeDisplay => CDriveFreeGb.HasValue ? $"{CDriveFreeGb:0.#} GB" : string.Empty;
    public string BitLockerSummary => BitLockerStatus.Count == 0 ? string.Empty : string.Join("; ", BitLockerStatus);
    public string PrinterSummary => InstalledPrinters.Count == 0 ? string.Empty : string.Join("; ", InstalledPrinters);
    public string StoredCredentialSummary => StoredCredentials.Count == 0 ? string.Empty : string.Join("; ", StoredCredentials);

    public string SearchBlob
    {
        get
        {
            var builder = new StringBuilder();
            builder.Append(Hostname).Append(' ').Append(IpAddress).Append(' ').Append(PcDomain).Append(' ');
            builder.Append(WindowsAccount).Append(' ').Append(AnyDeskId).Append(' ').Append(OsVersion).Append(' ');
            builder.Append(Manufacturer).Append(' ').Append(Model).Append(' ').Append(SerialNumber).Append(' ');
            builder.Append(Cpu).Append(' ').Append(Gpu).Append(' ').Append(Antivirus).Append(' ').Append(OfficeVersion).Append(' ');
            builder.Append(AdobeAutodesk).Append(' ').Append(PrinterSummary).Append(' ').Append(RawContent);
            return builder.ToString();
        }
    }
}

public sealed class StorageDevice
{
    public string DriveLetter { get; set; } = string.Empty;
    public double? TotalGb { get; set; }
    public double? FreeGb { get; set; }
    public string DriveType { get; set; } = string.Empty;

    public string Display => $"{DriveLetter}: Total {TotalGb:0.#} GB, Free {FreeGb:0.#} GB, {DriveType}".Trim().TrimEnd(',');
}

public sealed class SmbCredentialEntry
{
    public string NasIp { get; set; } = string.Empty;
    public string StoredUser { get; set; } = string.Empty;
    public string ActiveConnection { get; set; } = string.Empty;

    public string Display => $"{NasIp} | {StoredUser} | {ActiveConnection}";
}
