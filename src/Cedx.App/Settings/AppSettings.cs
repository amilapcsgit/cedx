namespace Cedx.App.Settings;

public sealed class AppSettings
{
    public string AssetsFolderPath { get; set; } = string.Empty;
    public string Theme { get; set; } = "Dark";
    public string NmapPath { get; set; } = "nmap";
    public bool EnableNetworkStatusScanOnRefresh { get; set; }
    public int ScanConcurrency { get; set; } = 8;
    public double LowStorageThresholdGb { get; set; } = 10d;
    public bool HideSensitiveDataByDefault { get; set; } = true;
}
