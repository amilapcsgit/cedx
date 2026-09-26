using System.Text.RegularExpressions;
using Cedx.Core.Models;

namespace Cedx.Core.Parsing;

public static partial class AssetDetails
{
    // Exactly three equals signs: embedded vNext diagnostic banners must not split a section.
    private static readonly Regex Heading = new(@"^=== (?<title>.+?) ===\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex Property = new(@"^(?<name>[\p{L}_][\p{L}\p{N}_ ()/.-]*?)\s*:\s*(?<value>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex Key = new(@"\b[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string RedactKeys(string text) => Key.Replace(text, "[key hidden]");

    public static IReadOnlyList<AssetDetailSection> Build(AssetRecord a, string content)
    {
        var result = new List<AssetDetailSection>();
        void Add(string title, string category, IEnumerable<AssetDetailRow> rows)
        {
            var data = rows.ToArray();
            result.Add(new(title, category, data, string.Join(Environment.NewLine, data.Select(r => $"{r.Record} | {r.Field}: {r.Value}"))));
        }
        AssetDetailRow R(string field, string value) => new(a.Hostname, field, value);
        Add("Overview", "Overview", [
            R("Hostname",a.Hostname), R("Windows account",a.WindowsAccount), R("User Email(s)",a.System.UserEmails),
            R("IP Address",a.IpAddress), R("MAC Address",a.MacAddress), R("PC Domain",a.PcDomain), R("AnyDesk ID",a.AnyDeskId),
            R("OS Version",a.OsVersion), R("OS Activation",a.Os.Activation), R("OS Install Date",a.Os.InstallDate),
            R("Last Reboot Time",a.Os.LastRebootTime), R("System Uptime",a.Os.SystemUptime), R("Windows Language",a.Os.Language),
            R("Manufacturer",a.Manufacturer), R("System Model",a.Model), R("Serial Number",a.SerialNumber), R("BIOS Version",a.System.BiosVersion),
            R("CPU",a.Cpu), R("RAM",a.Hardware.RamRaw), R("GPU",a.Gpu), R("Monitor",a.Hardware.MonitorModel),
            R("Office Version",a.OfficeVersion), R("Office Activation",a.Software.OfficeActivation), R("Antivirus",a.Antivirus),
            R("Network Mode",a.Network.NetworkMode), R("DNS Servers",a.Network.DnsServers), R("Default Gateway",a.Network.DefaultGateway),
            R("WinRM command",a.WinRmCommand), R("Source file",a.SourceFileName), R("File modified",a.LastModified.ToString("O"))]);
        Add("Volumes", "Hardware", a.LocalDisks.SelectMany(d => new[] {
            new AssetDetailRow(d.DriveLetter,"Total GB",d.TotalGb?.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown"),
            new AssetDetailRow(d.DriveLetter,"Free GB",d.FreeGb?.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown"),
            new AssetDetailRow(d.DriveLetter,"Type",d.DriveType)}));
        Add("Installed programs", "Software", a.Software.InstalledPrograms.Select((s,i) => new AssetDetailRow((i+1).ToString(),"Program",s)));
        Add("Printers", "Hardware", a.Software.InstalledPrinters.Select((p,i) => new AssetDetailRow((i+1).ToString(),"Name",p.Name)));
        Add("Stored credentials", "Security", a.StoredCredentials.SelectMany((c,i) => new[] {new AssetDetailRow((i+1).ToString(),"Target",c.Target),new AssetDetailRow((i+1).ToString(),"User",c.User)}));
        Add("NAS sessions", "Network", a.SmbCredentials.SelectMany(s => new[] {new AssetDetailRow(s.NasIp,"Stored user",s.StoredUser),new AssetDetailRow(s.NasIp,"Active session",s.ActiveConnection)}));
        Add("Local shares", "Network", a.SharedFolders.Select((s,i) => new AssetDetailRow((i+1).ToString(),"Share",s)));
        Add("Local accounts", "Security", [R("Accounts",a.Software.LocalUsers)]);
        Add("BitLocker volumes", "Security", a.BitLockerStatus.SelectMany(b => new[] {new AssetDetailRow(b.Volume,"Protection",b.Protection),new AssetDetailRow(b.Volume,"Encryption",b.Encryption)}));
        Add("Licensing summary", "Licenses", [R("Windows activation",a.Os.Activation),R("Windows key",a.Os.WindowsKey),R("Office version",a.OfficeVersion),R("Office activation",a.Software.OfficeActivation)]);
        var normalized=content.Replace("\r\n","\n").Replace('\r','\n');
        var matches=Heading.Matches(normalized);
        for(var i=0;i<matches.Count;i++)
        {
            var m=matches[i]; var title=m.Groups["title"].Value;
            if(title is "SMB Credentials for Provided NAS IPs" or "Local Disks (Space & Type)" or "Local Disks (in MB)" or "Quick WinRM Access" or "Asset inventory extensions") continue;
            var start=m.Index+m.Length;var end=i+1<matches.Count?matches[i+1].Index:normalized.Length;
            var raw=normalized[start..end].Trim();
            result.Add(new(title,Category(title),ParseRows(raw),raw));
        }
        Add("Parser diagnostics", "Diagnostics", a.ParseWarnings.Select((s,i)=>new AssetDetailRow((i+1).ToString(),"Warning",s)));
        return result;
    }

    public static IReadOnlyList<AssetDetailRow> ParseRows(string raw)
    {
        var rows=new List<AssetDetailRow>();var record=1;var pendingBreak=false;
        foreach(var line in raw.Replace("\r\n","\n").Split('\n'))
        {
            if(string.IsNullOrWhiteSpace(line)){pendingBreak=rows.Count>0;continue;}
            if(pendingBreak){record++;pendingBreak=false;}
            var m=Property.Match(line);
            if(m.Success) rows.Add(new(record.ToString(),m.Groups["name"].Value.TrimEnd(),m.Groups["value"].Value));
            else if(rows.Count>0 && char.IsWhiteSpace(line[0]) && rows[^1].Record==record.ToString())
                rows[^1]=rows[^1] with {Value=rows[^1].Value+"\n"+line.TrimStart()};
            else rows.Add(new(record.ToString(),"Text",line));
        }
        return rows;
    }

    private static string Category(string title)
    {
        if(Regex.IsMatch(title,"license|licensing|activation|Office|Windows key",RegexOptions.IgnoreCase))return "Licenses";
        if(Regex.IsMatch(title,"CPU|GPU|Monitor|disk|Printer",RegexOptions.IgnoreCase))return "Hardware";
        if(Regex.IsMatch(title,"Firewall|Defender|account|administrator|cmdkey|credential",RegexOptions.IgnoreCase))return "Security";
        if(Regex.IsMatch(title,"network|IP address|Routing|Mapped|mapping|SMB|Proxy|net use",RegexOptions.IgnoreCase))return "Network";
        return "Diagnostics";
    }
}
