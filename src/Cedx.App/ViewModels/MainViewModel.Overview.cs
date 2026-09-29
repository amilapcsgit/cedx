namespace Cedx.App.ViewModels;

public sealed partial class MainViewModel
{
    private IReadOnlyList<OverviewGroup> _overviewGroups = [];
    public IReadOnlyList<OverviewGroup> OverviewGroups
    {
        get => _overviewGroups;
        private set => SetProperty(ref _overviewGroups, value);
    }

    private void RefreshOverview()
    {
        if (SelectedAsset is not { } a) { OverviewGroups = []; return; }
        var groups = new List<OverviewGroup>();
        void Add(string title, params OverviewField[] fields)
        {
            var rows = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToArray();
            if (rows.Length > 0) groups.Add(new(title, rows));
        }
        Add("Identity", new("Hostname", a.Hostname, true), new("User", a.WindowsUserDisplay),
            new("Account", a.WindowsAccount, true), new("IP address", a.IpAddress, true), new("Domain", a.PcDomain));
        Add("Remote access", new("AnyDesk ID", a.HasAnyDesk ? a.AnyDeskId : "Not reported" , a.HasAnyDesk));
        Add("Hardware", new("Manufacturer", a.Manufacturer), new("Model", a.Model), new("Serial", a.SerialNumber),
            new("RAM", a.RamDisplay), new("CPU", a.Cpu), new("GPU", a.Gpu), new("Monitor", a.Hardware.MonitorModel),
            new("BIOS", a.System.BiosVersion), new("Disks", a.LocalDisks.Count > 0 ? a.LocalDiskSummary : "Not reported"));
        Add("Operating system", new("Version", a.OsVersion), new("Installed", a.Os.InstallDate),
            new("Last reboot", a.Os.LastRebootTime), new("Uptime", a.Os.SystemUptime),
            new("Activation", a.Os.Activation), new("Language", a.Os.Language));
        Add("Network", new("Mode", a.Network.NetworkMode), new("Gateway", a.Network.DefaultGateway),
            new("DNS", a.Network.DnsServers), new("MAC", a.MacAddress, true));
        Add("Software and protection", new("Antivirus", a.Antivirus), new("Office", a.OfficeVersion),
            new("Office status", a.Software.OfficeActivation), new("Adobe / Autodesk", a.Software.AdobeAutodesk),
            new("Printers", a.Software.InstalledPrinters.Count > 0 ? a.PrinterSummary : ""),
            new("Local accounts", a.Software.LocalUsers), new("BitLocker", a.BitLockerStatus.Count > 0 ? a.BitLockerSummary : ""));
        Add("Assignment", new("Company", a.Company), new("Assigned to", a.Person), new("Department", a.Department),
            new("Location", a.Location), new("Asset tag", a.AssetTag), new("Lifecycle", a.IsManaged ? a.Assignment.Lifecycle : ""),
            new("Notes", a.Assignment.Notes));
        OverviewGroups = groups;
    }
}

public sealed record OverviewField(string Label, string Value, bool CanCopy = false);
public sealed record OverviewGroup(string Title, IReadOnlyList<OverviewField> Fields);
