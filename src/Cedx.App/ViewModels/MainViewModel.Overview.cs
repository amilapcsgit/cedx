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
            var rows = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value))
                .Select(f => f with { Value = Display(f.Value) }).ToArray();
            if (rows.Length > 0) groups.Add(new(title, rows));
        }
        Add("Identity", new OverviewField("Hostname", a.Hostname, true), new OverviewField("User", a.WindowsUserDisplay),
            new OverviewField("Account", a.WindowsAccount, true), new OverviewField("IP address", a.IpAddress, true), new OverviewField("Domain", a.PcDomain));
        Add("Remote access", new OverviewField("AnyDesk ID", a.HasAnyDesk ? a.AnyDeskId : "Not reported" , a.HasAnyDesk));
        Add("Hardware", new OverviewField("Manufacturer", a.Manufacturer), new OverviewField("Model", a.Model), new OverviewField("Serial", a.SerialNumber),
            new OverviewField("RAM", a.RamDisplay), new OverviewField("CPU", a.Cpu), new OverviewField("GPU", a.Gpu), new OverviewField("Monitor", a.Hardware.MonitorModel),
            new OverviewField("BIOS", a.System.BiosVersion), new OverviewField("Disks", a.LocalDisks.Count > 0 ? a.LocalDiskSummary : "Not reported"));
        Add("Operating system", new OverviewField("Version", a.OsVersion), new OverviewField("Installed", a.Os.InstallDate),
            new OverviewField("Last reboot", a.Os.LastRebootTime), new OverviewField("Uptime", a.Os.SystemUptime),
            new OverviewField("Activation", a.Os.Activation), new OverviewField("Language", a.Os.Language));
        Add("Network", new OverviewField("Mode", a.Network.NetworkMode), new OverviewField("Gateway", a.Network.DefaultGateway),
            new OverviewField("DNS", a.Network.DnsServers), new OverviewField("MAC", a.MacAddress, true));
        Add("Software and protection", new OverviewField("Antivirus", a.Antivirus), new OverviewField("Office", a.OfficeVersion),
            new OverviewField("Office status", a.Software.OfficeActivation), new OverviewField("Adobe / Autodesk", a.Software.AdobeAutodesk),
            new OverviewField("Printers", a.Software.InstalledPrinters.Count > 0 ? a.PrinterSummary : ""),
            new OverviewField("Local accounts", a.Software.LocalUsers), new OverviewField("BitLocker", a.BitLockerStatus.Count > 0 ? a.BitLockerSummary : ""));
        Add("Assignment", new OverviewField("Company", a.Company), new OverviewField("Assigned to", a.Person), new OverviewField("Department", a.Department),
            new OverviewField("Location", a.Location), new OverviewField("Asset tag", a.AssetTag), new OverviewField("Lifecycle", a.IsManaged ? a.Assignment.Lifecycle : ""),
            new OverviewField("Notes", a.Assignment.Notes));
        OverviewGroups = groups;
    }
}

public sealed record OverviewField(string Label, string Value, bool CanCopy = false);
public sealed record OverviewGroup(string Title, IReadOnlyList<OverviewField> Fields);
