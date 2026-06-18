using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CedxNative.Models;

namespace CedxNative.Services;

public sealed class AssetParserService
{
    public async Task<AssetRecord?> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            content = await File.ReadAllTextAsync(filePath, Encoding.GetEncoding(1252), cancellationToken);
        }
        catch (IOException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var asset = new AssetRecord
        {
            SourceFilePath = filePath,
            SourceFileName = Path.GetFileName(filePath),
            RawContent = content,
            Hostname = FirstValue(content, "Hostname", "Computer Name", "System Name") ?? Path.GetFileNameWithoutExtension(filePath),
            IpAddress = FirstValue(content, "IP Address", "IPv4 Address", "Network Address") ?? string.Empty,
            PcDomain = FirstValue(content, "PC Domain") ?? string.Empty,
            AnyDeskId = FirstValue(content, "AnyDesk ID", "AnyDesk", "Remote ID") ?? string.Empty,
            WindowsAccount = FirstValue(content, "Windows account", "Windows Account", "User Account") ?? string.Empty,
            UserEmails = FirstValue(content, "User Email(s)", "User Email", "Email") ?? string.Empty,
            OsVersion = FirstValue(content, "OS Version", "Operating System", "Windows Version") ?? string.Empty,
            OsInstallDate = FirstValue(content, "OS Install Date", "Original Install Date") ?? string.Empty,
            LastRebootTime = FirstValue(content, "Last Reboot Time") ?? string.Empty,
            SystemUptime = FirstValue(content, "System Uptime") ?? string.Empty,
            Cpu = FirstValue(content, "CPU", "Processor") ?? string.Empty,
            Gpu = FirstValue(content, "GPU", "Graphics", "Video Card") ?? string.Empty,
            MonitorModel = CleanDisplayText(FirstValue(content, "Monitor Model") ?? string.Empty),
            Manufacturer = FirstValue(content, "System Manufacturer", "Manufacturer", "Computer Manufacturer") ?? string.Empty,
            Model = FirstValue(content, "System Model", "Model", "Computer Model") ?? string.Empty,
            SerialNumber = FirstValue(content, "Serial Number") ?? string.Empty,
            BiosVersion = FirstValue(content, "BIOS Version", "BIOS") ?? string.Empty,
            WindowsLanguage = FirstValue(content, "Windows Language", "Language") ?? string.Empty,
            NetworkMode = FirstValue(content, "Network Mode") ?? string.Empty,
            DnsServers = FirstValue(content, "DNS Servers") ?? string.Empty,
            DefaultGateway = FirstValue(content, "Default Gateway") ?? string.Empty,
            OfficeVersion = SectionSingleLine(content, "Office Version") ?? string.Empty,
            OfficeActivation = SectionSingleLine(content, "Office Activation") ?? string.Empty,
            OsActivation = SectionSingleLine(content, "OS Activation") ?? string.Empty,
            WindowsKey = FirstValue(content, "Windows key", "Windows Key") ?? string.Empty,
            Antivirus = SectionSingleLine(content, "Antivirus") ?? string.Empty,
            AdobeAutodesk = SectionSingleLine(content, "Adobe/Autodesk") ?? string.Empty,
            LocalUsers = SectionText(content, "Local User Accounts") ?? string.Empty,
            WinRmCommand = ExtractWinRmCommand(content) ?? string.Empty
        };

        asset.RamGb = ParseGigabytes(FirstValue(content, "RAM", "Total Physical Memory", "Memory"));
        asset.OnlineStatus = string.IsNullOrWhiteSpace(asset.IpAddress) || asset.IpAddress.StartsWith("169.254", StringComparison.Ordinal) ? "offline" : "unknown";

        AddPlainLines(asset.StoredCredentials, SectionText(content, "Stored Network Credentials"));
        AddPlainLines(asset.SharedFolders, SectionText(content, "Shared Folders"));
        AddPlainLines(asset.InstalledPrinters, SectionText(content, "Installed Printers"));
        AddPlainLines(asset.BitLockerStatus, SectionText(content, "Bitlocker Status") ?? SectionText(content, "BitLocker Status"));

        ParseLocalDisks(content, asset);
        ParseSmbCredentials(content, asset);

        if (string.IsNullOrWhiteSpace(asset.IpAddress))
        {
            asset.ParseWarnings.Add("Missing IP Address.");
        }

        return asset;
    }

    private static string? FirstValue(string content, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = Regex.Match(content, $"^{Regex.Escape(key)}\\s*:\\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        return null;
    }

    private static string? ExtractWinRmCommand(string content)
    {
        var match = Regex.Match(content, @"^Enter-PSSession\s+.+$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Value.Trim() : null;
    }

    private static string? SectionSingleLine(string content, string title)
    {
        var text = SectionText(content, title);
        return text?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0);
    }

    private static string? SectionText(string content, string title)
    {
        var pattern = $"^{Regex.Escape(title)}\\s*:\\s*\\r?\\n(?<body>.*?)(?=\\r?\\n\\r?\\n|\\r?\\n===|\\z)";
        var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);
        return match.Success ? match.Groups["body"].Value.Trim() : null;
    }

    private static void AddPlainLines(List<string> target, string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        foreach (var line in section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            target.Add(trimmed);
        }
    }

    private static double? ParseGigabytes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace(',', '.');
        var match = Regex.Match(normalized, @"(?<number>\d+(?:\.\d+)?)\s*(?<unit>GB|MB|KB)?", RegexOptions.IgnoreCase);
        if (!match.Success || !double.TryParse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        return unit switch
        {
            "MB" => Math.Round(number / 1024d, 1),
            "KB" => Math.Round(number / 1024d / 1024d, 1),
            _ => Math.Round(number, 1)
        };
    }

    private static void ParseLocalDisks(string content, AssetRecord asset)
    {
        var section = ExtractDelimitedSection(content, "=== Local Disks");
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        foreach (var line in section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"(?<drive>[A-Z]):\s+Total:\s*(?<total>[\d.,]+)\s*MB,\s*Free:\s*(?<free>[\d.,]+)\s*MB,\s*Type:\s*(?<type>.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var total = ParseDouble(match.Groups["total"].Value);
            var free = ParseDouble(match.Groups["free"].Value);
            asset.LocalDisks.Add(new StorageDevice
            {
                DriveLetter = match.Groups["drive"].Value,
                TotalGb = total.HasValue ? Math.Round(total.Value / 1024d, 1) : null,
                FreeGb = free.HasValue ? Math.Round(free.Value / 1024d, 1) : null,
                DriveType = match.Groups["type"].Value.Trim()
            });
        }
    }

    private static void ParseSmbCredentials(string content, AssetRecord asset)
    {
        var section = ExtractDelimitedSection(content, "=== SMB Credentials for Provided NAS IPs ===");
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        foreach (var line in section.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!Regex.IsMatch(trimmed, @"^\d+\.\d+\.\d+\.\d+"))
            {
                continue;
            }

            var parts = Regex.Split(trimmed, @"\s{2,}").Where(x => x.Length > 0).ToArray();
            if (parts.Length >= 3)
            {
                asset.SmbCredentials.Add(new SmbCredentialEntry
                {
                    NasIp = parts[0],
                    StoredUser = parts[1],
                    ActiveConnection = parts[2]
                });
            }
        }
    }

    private static string? ExtractDelimitedSection(string content, string startsWith)
    {
        var start = content.IndexOf(startsWith, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        var afterHeader = content.IndexOf('\n', start);
        if (afterHeader < 0)
        {
            return null;
        }

        var next = content.IndexOf("\n===", afterHeader + 1, StringComparison.Ordinal);
        return next < 0 ? content[(afterHeader + 1)..].Trim() : content[(afterHeader + 1)..next].Trim();
    }

    private static double? ParseDouble(string value)
    {
        return double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static string CleanDisplayText(string value)
    {
        return value.Replace('\0', ' ').Trim();
    }
}
