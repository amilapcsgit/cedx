using System.Globalization;
using System.Text.RegularExpressions;
using Cedx.Core.Models;

namespace Cedx.Core.Parsing;

public sealed partial class AssetTextParser : IAssetParser
{
    public AssetRecord Parse(string content, string sourceFilePath, DateTimeOffset lastModified)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFilePath);

        var lines = SplitLines(content);
        var warnings = new List<string>();
        var record = new AssetRecord
        {
            SourceFilePath = sourceFilePath,
            SourceFileName = Path.GetFileName(sourceFilePath),
            LastModified = lastModified,
            RawContent = content
        };

        record.System = new SystemInfo
        {
            Hostname = GetField(lines, "Hostname") ?? Path.GetFileNameWithoutExtension(sourceFilePath),
            PcDomain = GetField(lines, "PC Domain") ?? string.Empty,
            WindowsAccount = GetField(lines, "Windows account") ?? string.Empty,
            UserEmails = GetField(lines, "User Email(s)") ?? string.Empty,
            Manufacturer = GetField(lines, "System Manufacturer") ?? string.Empty,
            Model = GetField(lines, "System Model") ?? string.Empty,
            SerialNumber = GetField(lines, "Serial Number") ?? string.Empty,
            BiosVersion = GetField(lines, "BIOS Version") ?? string.Empty
        };

        record.Network = new NetworkInfo
        {
            IpAddress = GetField(lines, "IP Address") ?? string.Empty,
            PcDomain = record.System.PcDomain,
            AnyDeskId = GetField(lines, "AnyDesk ID") ?? string.Empty
        };

        record.Os = new OsInfo
        {
            Version = GetField(lines, "OS Version") ?? string.Empty,
            InstallDate = GetField(lines, "OS Install Date") ?? string.Empty,
            LastRebootTime = GetField(lines, "Last Reboot Time") ?? string.Empty,
            SystemUptime = GetField(lines, "System Uptime") ?? string.Empty,
            Language = GetField(lines, "Windows Language") ?? string.Empty
        };

        var ramRaw = GetField(lines, "RAM") ?? string.Empty;
        record.Hardware = new HardwareInfo
        {
            Cpu = GetField(lines, "CPU") ?? string.Empty,
            RamRaw = ramRaw,
            RamGb = ParseGb(ramRaw),
            Gpu = GetField(lines, "GPU") ?? string.Empty,
            MonitorModel = GetField(lines, "Monitor Model") ?? string.Empty
        };

        var networkBlock = GetColonBlock(lines, "Network Configuration:");
        record.Network.NetworkMode = GetField(networkBlock, "Network Mode") ?? string.Empty;
        record.Network.DnsServers = GetField(networkBlock, "DNS Servers") ?? string.Empty;
        record.Network.DefaultGateway = GetField(networkBlock, "Default Gateway") ?? string.Empty;

        record.Software = new SoftwareInfo
        {
            OfficeVersion = GetColonBlockText(lines, "Office Version:"),
            OfficeActivation = GetColonBlockText(lines, "Office Activation:"),
            Antivirus = GetColonBlockText(lines, "Antivirus:"),
            AdobeAutodesk = GetColonBlockText(lines, "Adobe/Autodesk:"),
            LocalUsers = GetColonBlockText(lines, "Local User Accounts:"),
            InstalledPrinters = ParsePrinters(GetColonBlockText(lines, "Installed Printers:")),
            InstalledPrograms = ParseInstalledPrograms(lines)
        };

        record.Os.Activation = GetColonBlockText(lines, "OS Activation:");
        record.Os.WindowsKey = GetColonBlockText(lines, "Windows key:");
        record.StoredCredentials = ParseStoredCredentials(GetColonBlock(lines, "Stored Network Credentials:"));
        record.SharedFolders = ParseSimpleList(GetColonBlock(lines, "Shared Folders:"));
        record.BitLockerStatus = ParseBitLocker(GetColonBlock(lines, "Bitlocker Status:"), warnings);
        record.SmbCredentials = ParseSmbCredentials(GetEqualsBlock(lines, "=== SMB Credentials for Provided NAS IPs ==="), warnings);
        record.LocalDisks = ParseLocalDisks(GetEqualsBlock(lines, "=== Local Disks (Space & Type) ==="), warnings);
        record.WinRmCommand = GetWinRmCommand(lines);

        if (string.IsNullOrWhiteSpace(record.System.Hostname))
        {
            warnings.Add("Hostname was not found; source filename was used.");
        }

        if (string.IsNullOrWhiteSpace(record.Network.IpAddress))
        {
            warnings.Add("IP Address was not found.");
        }

        if (!string.IsNullOrWhiteSpace(record.Hardware.RamRaw) && record.Hardware.RamGb is null)
        {
            warnings.Add("RAM value could not be converted to GB.");
        }

        record.ParseWarnings = warnings;
        return record;
    }

    private static IReadOnlyList<string> SplitLines(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string? GetField(IEnumerable<string> lines, string fieldName)
    {
        var prefix = fieldName + ":";
        foreach (var line in lines)
        {
            var trimmed = CleanLine(line);
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return CleanValue(trimmed[prefix.Length..]);
        }

        return null;
    }

    private static IReadOnlyList<string> GetColonBlock(IReadOnlyList<string> lines, string heading)
    {
        var start = FindLine(lines, heading);
        if (start < 0)
        {
            return [];
        }

        var result = new List<string>();
        for (var index = start + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (line.TrimStart().StartsWith("===", StringComparison.Ordinal))
            {
                break;
            }

            result.Add(line);
        }

        return result;
    }

    private static string GetColonBlockText(IReadOnlyList<string> lines, string heading)
    {
        return string.Join(Environment.NewLine, GetColonBlock(lines, heading).Select(CleanValue)).Trim();
    }

    private static IReadOnlyList<string> GetEqualsBlock(IReadOnlyList<string> lines, string heading)
    {
        var start = FindLine(lines, heading);
        if (start < 0)
        {
            return [];
        }

        var result = new List<string>();
        for (var index = start + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith("===", StringComparison.Ordinal))
            {
                break;
            }

            result.Add(line);
        }

        return result;
    }

    private static int FindLine(IReadOnlyList<string> lines, string heading)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (CleanLine(lines[index]).Equals(heading, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> ParseSimpleList(IEnumerable<string> lines)
    {
        return lines.Select(CleanValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(value => !value.Equals("None", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<CredentialEntry> ParseStoredCredentials(IEnumerable<string> lines)
    {
        var entries = new List<CredentialEntry>();
        foreach (var rawLine in lines)
        {
            var raw = CleanValue(rawLine);
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("No ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = CredentialRegex().Match(raw);
            entries.Add(new CredentialEntry
            {
                Raw = raw,
                Target = match.Success ? CleanValue(match.Groups["target"].Value) : string.Empty,
                User = match.Success ? CleanValue(match.Groups["user"].Value) : string.Empty
            });
        }

        return entries;
    }

    private static IReadOnlyList<PrinterEntry> ParsePrinters(string printerText)
    {
        if (string.IsNullOrWhiteSpace(printerText) || printerText.Equals("No printers found.", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return printerText.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(name => new PrinterEntry { Name = name })
            .ToArray();
    }

    private static IReadOnlyList<string> ParseInstalledPrograms(IReadOnlyList<string> lines)
    {
        var block = GetColonBlock(lines, "Installed Programs:");
        if (block.Count == 0)
        {
            block = GetColonBlock(lines, "Installed Program:");
        }

        if (block.Count == 0)
        {
            block = GetColonBlock(lines, "Software:");
        }

        if (block.Count == 0)
        {
            block = GetColonBlock(lines, "Applications:");
        }

        if (block.Count == 0)
        {
            block = GetEqualsBlock(lines, "=== Installed Programs ===");
        }

        return block.SelectMany(SplitSoftwareLine)
            .Select(CleanValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(value => !value.Equals("None", StringComparison.OrdinalIgnoreCase))
            .Where(value => !value.Equals("None Found", StringComparison.OrdinalIgnoreCase))
            .Where(value => !value.StartsWith("No installed", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> SplitSoftwareLine(string line)
    {
        return CleanValue(line)
            .TrimStart('-', '*', ' ')
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static IReadOnlyList<BitLockerVolume> ParseBitLocker(IEnumerable<string> lines, ICollection<string> warnings)
    {
        var volumes = new List<BitLockerVolume>();
        foreach (var rawLine in lines)
        {
            var raw = CleanValue(rawLine);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var match = BitLockerRegex().Match(raw);
            if (!match.Success)
            {
                warnings.Add($"BitLocker row was preserved but not parsed: {raw}");
                volumes.Add(new BitLockerVolume { Raw = raw });
                continue;
            }

            volumes.Add(new BitLockerVolume
            {
                Volume = CleanValue(match.Groups["volume"].Value),
                Protection = CleanValue(match.Groups["protection"].Value),
                Encryption = CleanValue(match.Groups["encryption"].Value),
                Raw = raw
            });
        }

        return volumes;
    }

    private static IReadOnlyList<SmbCredentialEntry> ParseSmbCredentials(IEnumerable<string> lines, ICollection<string> warnings)
    {
        var entries = new List<SmbCredentialEntry>();
        foreach (var rawLine in lines)
        {
            var raw = CleanValue(rawLine);
            if (string.IsNullOrWhiteSpace(raw) ||
                raw.StartsWith("NasIP", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("-----", StringComparison.Ordinal))
            {
                continue;
            }

            var rowMatch = SmbRowRegex().Match(raw);
            if (rowMatch.Success)
            {
                entries.Add(new SmbCredentialEntry
                {
                    NasIp = CleanValue(rowMatch.Groups["ip"].Value),
                    StoredUser = CleanValue(rowMatch.Groups["user"].Value),
                    ActiveConnection = CleanValue(rowMatch.Groups["active"].Value),
                    Raw = raw
                });
                continue;
            }

            var parts = Regex.Split(raw, @"\s{2,}").Where(part => part.Length > 0).ToArray();
            if (parts.Length < 3)
            {
                warnings.Add($"SMB credential row was preserved but not parsed: {raw}");
                entries.Add(new SmbCredentialEntry { Raw = raw });
                continue;
            }

            entries.Add(new SmbCredentialEntry
            {
                NasIp = parts[0],
                StoredUser = parts[1],
                ActiveConnection = parts[2],
                Raw = raw
            });
        }

        return entries;
    }

    private static IReadOnlyList<StorageDevice> ParseLocalDisks(IEnumerable<string> lines, ICollection<string> warnings)
    {
        var disks = new List<StorageDevice>();
        foreach (var rawLine in lines)
        {
            var raw = CleanValue(rawLine);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var match = DiskRegex().Match(raw);
            if (!match.Success)
            {
                warnings.Add($"Local disk row was preserved but not parsed: {raw}");
                disks.Add(new StorageDevice { Raw = raw });
                continue;
            }

            var totalMb = ParseDecimal(match.Groups["total"].Value);
            var freeMb = ParseDecimal(match.Groups["free"].Value);
            disks.Add(new StorageDevice
            {
                DriveLetter = CleanValue(match.Groups["drive"].Value),
                TotalGb = totalMb / 1024d,
                FreeGb = freeMb / 1024d,
                DriveType = CleanValue(match.Groups["type"].Value),
                Raw = raw
            });
        }

        return disks;
    }

    private static string GetWinRmCommand(IReadOnlyList<string> lines)
    {
        var block = GetEqualsBlock(lines, "=== Quick WinRM Access ===");
        return CleanValue(block.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty);
    }

    private static double? ParseGb(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = SizeRegex().Match(value);
        if (!match.Success)
        {
            return null;
        }

        var number = ParseDecimal(match.Groups["value"].Value);
        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        return unit switch
        {
            "TB" => number * 1024d,
            "GB" => number,
            "MB" => number / 1024d,
            "KB" => number / 1024d / 1024d,
            _ => number
        };
    }

    private static double ParseDecimal(string value)
    {
        var normalized = value.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
    }

    private static string CleanValue(string value)
    {
        return value.Replace("\0", string.Empty, StringComparison.Ordinal).Replace("\uFEFF", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static string CleanLine(string value)
    {
        return CleanValue(value);
    }

    [GeneratedRegex(@"Target=(?<target>.*?),\s*User=(?<user>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialRegex();

    [GeneratedRegex(@"^(?<volume>[^:]+):\s*Protection:\s*(?<protection>[^,]+),\s*Encryption:\s*(?<encryption>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BitLockerRegex();

    [GeneratedRegex(@"^\s*(?<drive>[A-Z]:)\s+Total:\s*(?<total>[0-9]+(?:[\.,][0-9]+)?)\s*MB,\s*Free:\s*(?<free>[0-9]+(?:[\.,][0-9]+)?)\s*MB,\s*Type:\s*(?<type>.+?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiskRegex();

    [GeneratedRegex(@"^(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<user>.*?)\s+(?<active>\S+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SmbRowRegex();

    [GeneratedRegex(@"(?<value>[0-9]+(?:[\.,][0-9]+)?)\s*(?<unit>TB|GB|MB|KB)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizeRegex();
}
