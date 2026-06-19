using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using Cedx.App.Common;
using Cedx.Core.Models;
using Cedx.Core.Services;
using Microsoft.Win32;

namespace Cedx.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IAssetRepository _repository;
    private CancellationTokenSource? _loadCts;
    private AssetRecord? _selectedAsset;
    private string _searchText = string.Empty;
    private string _selectedOsFilter = AllFilter;
    private string _selectedManufacturerFilter = AllFilter;
    private string _selectedStatusFilter = AllFilter;
    private bool _lowStorageOnly;
    private bool _hasAnyDeskOnly;
    private bool _hasBitLockerOffOnly;
    private bool _hasStoredCredentialsOnly;
    private double _lowStorageThresholdGb = 10d;
    private double _minRamFilterGb;
    private double _maxRamFilterGb = 512d;
    private double _minStorageFilterGb;
    private double _maxStorageFilterGb = 5000d;
    private string _anyDeskFilter = string.Empty;
    private bool _isBusy;
    private bool _isNetworkScanBusy;
    private string _assetsFolderPath;
    private string _nmapPath = "nmap";
    private string _selectedNmapScanType = "Quick Scan";
    private string _lastRefreshText = "Not loaded";
    private string _statusMessage = "Ready";
    private int _loadedCount;
    private int _filteredCount;
    private int _onlineCount;
    private int _offlineCount;
    private int _unknownCount;
    private int _errorsCount;
    private int _lowStorageCount;
    private int _anyDeskReadyCount;
    private int _bitLockerOffCount;
    private int _storedCredentialAssetCount;
    private string _totalRamDisplay = "0 GB";
    private string _totalStorageDisplay = "0 GB";
    private string _osDistributionText = "OS: no data";
    private string _manufacturerDistributionText = "Makers: no data";
    private string _statusDistributionText = "Status: no data";
    private string _storageHealthText = "Storage: no data";

    private const string AllFilter = "All";

    public MainViewModel(IAssetRepository repository)
    {
        _repository = repository;
        _assetsFolderPath = AssetFolderLocator.FindDefaultFolder(Environment.CurrentDirectory, AppContext.BaseDirectory);

        AssetsView = CollectionViewSource.GetDefaultView(Assets);
        AssetsView.Filter = FilterAsset;
        AssetsView.SortDescriptions.Add(new SortDescription(nameof(AssetRecord.Hostname), ListSortDirection.Ascending));

        if (AssetsView is INotifyCollectionChanged notifyCollectionChanged)
        {
            notifyCollectionChanged.CollectionChanged += (_, _) => UpdateCounts();
        }

        OsOptions.Add(AllFilter);
        ManufacturerOptions.Add(AllFilter);
        StatusOptions.Add(AllFilter);
        foreach (var value in Enum.GetNames<ScanStatus>())
        {
            StatusOptions.Add(value);
        }
        ScanTypeOptions.Add("Quick Scan");
        ScanTypeOptions.Add("Full Scan");

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        ScanNetworkCommand = new AsyncRelayCommand(_ => ScanNetworkStatusAsync(), _ => Assets.Count > 0 && !IsBusy && !IsNetworkScanBusy);
        ExportCsvCommand = new RelayCommand(_ => ExportFilteredCsv(), _ => Assets.Count > 0);
        ExportSoftwareCsvCommand = new RelayCommand(_ => ExportInstalledProgramsCsv(), _ => Assets.Count > 0);
        OpenAssetsFolderCommand = new RelayCommand(_ => OpenAssetsFolder());
        CopyTextCommand = new RelayCommand(parameter => CopyText(parameter as string));
        LaunchAnyDeskCommand = new RelayCommand(parameter => LaunchAnyDesk(parameter as string), parameter => CanLaunchAnyDesk(parameter as string));
        ConnectSelectedAnyDeskCommand = new RelayCommand(_ => LaunchAnyDesk(SelectedAsset?.AnyDeskId), _ => CanLaunchAnyDesk(SelectedAsset?.AnyDeskId));
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty, _ => !string.IsNullOrWhiteSpace(SearchText));
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
    }

    public ObservableCollection<AssetRecord> Assets { get; } = [];
    public ICollectionView AssetsView { get; }
    public ObservableCollection<string> OsOptions { get; } = [];
    public ObservableCollection<string> ManufacturerOptions { get; } = [];
    public ObservableCollection<string> StatusOptions { get; } = [];
    public ObservableCollection<string> ScanTypeOptions { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ScanNetworkCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ExportSoftwareCsvCommand { get; }
    public RelayCommand OpenAssetsFolderCommand { get; }
    public RelayCommand CopyTextCommand { get; }
    public RelayCommand LaunchAnyDeskCommand { get; }
    public RelayCommand ConnectSelectedAnyDeskCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }

    public AssetRecord? SelectedAsset
    {
        get => _selectedAsset;
        set
        {
            if (SetProperty(ref _selectedAsset, value))
            {
                ConnectSelectedAnyDeskCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
                ClearSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedOsFilter
    {
        get => _selectedOsFilter;
        set
        {
            if (SetProperty(ref _selectedOsFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedManufacturerFilter
    {
        get => _selectedManufacturerFilter;
        set
        {
            if (SetProperty(ref _selectedManufacturerFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool LowStorageOnly
    {
        get => _lowStorageOnly;
        set
        {
            if (SetProperty(ref _lowStorageOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool HasAnyDeskOnly
    {
        get => _hasAnyDeskOnly;
        set
        {
            if (SetProperty(ref _hasAnyDeskOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool HasBitLockerOffOnly
    {
        get => _hasBitLockerOffOnly;
        set
        {
            if (SetProperty(ref _hasBitLockerOffOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool HasStoredCredentialsOnly
    {
        get => _hasStoredCredentialsOnly;
        set
        {
            if (SetProperty(ref _hasStoredCredentialsOnly, value))
            {
                ApplyFilters();
            }
        }
    }

    public double LowStorageThresholdGb
    {
        get => _lowStorageThresholdGb;
        set
        {
            if (SetProperty(ref _lowStorageThresholdGb, value))
            {
                ApplyFilters();
            }
        }
    }

    public double MinRamFilterGb
    {
        get => _minRamFilterGb;
        set
        {
            if (SetProperty(ref _minRamFilterGb, Math.Max(0d, value)))
            {
                ApplyFilters();
            }
        }
    }

    public double MaxRamFilterGb
    {
        get => _maxRamFilterGb;
        set
        {
            if (SetProperty(ref _maxRamFilterGb, Math.Max(0d, value)))
            {
                ApplyFilters();
            }
        }
    }

    public double MinStorageFilterGb
    {
        get => _minStorageFilterGb;
        set
        {
            if (SetProperty(ref _minStorageFilterGb, Math.Max(0d, value)))
            {
                ApplyFilters();
            }
        }
    }

    public double MaxStorageFilterGb
    {
        get => _maxStorageFilterGb;
        set
        {
            if (SetProperty(ref _maxStorageFilterGb, Math.Max(0d, value)))
            {
                ApplyFilters();
            }
        }
    }

    public string AnyDeskFilter
    {
        get => _anyDeskFilter;
        set
        {
            if (SetProperty(ref _anyDeskFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ScanNetworkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNetworkScanBusy
    {
        get => _isNetworkScanBusy;
        private set
        {
            if (SetProperty(ref _isNetworkScanBusy, value))
            {
                ScanNetworkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AssetsFolderPath
    {
        get => _assetsFolderPath;
        set => SetProperty(ref _assetsFolderPath, value);
    }

    public string NmapPath
    {
        get => _nmapPath;
        set => SetProperty(ref _nmapPath, string.IsNullOrWhiteSpace(value) ? "nmap" : value);
    }

    public string SelectedNmapScanType
    {
        get => _selectedNmapScanType;
        set => SetProperty(ref _selectedNmapScanType, string.IsNullOrWhiteSpace(value) ? "Quick Scan" : value);
    }

    public string LastRefreshText
    {
        get => _lastRefreshText;
        private set => SetProperty(ref _lastRefreshText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int LoadedCount
    {
        get => _loadedCount;
        private set => SetProperty(ref _loadedCount, value);
    }

    public int FilteredCount
    {
        get => _filteredCount;
        private set => SetProperty(ref _filteredCount, value);
    }

    public int OnlineCount
    {
        get => _onlineCount;
        private set => SetProperty(ref _onlineCount, value);
    }

    public int OfflineCount
    {
        get => _offlineCount;
        private set => SetProperty(ref _offlineCount, value);
    }

    public int UnknownCount
    {
        get => _unknownCount;
        private set => SetProperty(ref _unknownCount, value);
    }

    public int ErrorsCount
    {
        get => _errorsCount;
        private set => SetProperty(ref _errorsCount, value);
    }

    public int LowStorageCount
    {
        get => _lowStorageCount;
        private set => SetProperty(ref _lowStorageCount, value);
    }

    public int AnyDeskReadyCount
    {
        get => _anyDeskReadyCount;
        private set => SetProperty(ref _anyDeskReadyCount, value);
    }

    public int BitLockerOffCount
    {
        get => _bitLockerOffCount;
        private set => SetProperty(ref _bitLockerOffCount, value);
    }

    public int StoredCredentialAssetCount
    {
        get => _storedCredentialAssetCount;
        private set => SetProperty(ref _storedCredentialAssetCount, value);
    }

    public string TotalRamDisplay
    {
        get => _totalRamDisplay;
        private set => SetProperty(ref _totalRamDisplay, value);
    }

    public string TotalStorageDisplay
    {
        get => _totalStorageDisplay;
        private set => SetProperty(ref _totalStorageDisplay, value);
    }

    public string OsDistributionText
    {
        get => _osDistributionText;
        private set => SetProperty(ref _osDistributionText, value);
    }

    public string ManufacturerDistributionText
    {
        get => _manufacturerDistributionText;
        private set => SetProperty(ref _manufacturerDistributionText, value);
    }

    public string StatusDistributionText
    {
        get => _statusDistributionText;
        private set => SetProperty(ref _statusDistributionText, value);
    }

    public string StorageHealthText
    {
        get => _storageHealthText;
        private set => SetProperty(ref _storageHealthText, value);
    }

    public async Task RefreshAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;

        IsBusy = true;
        StatusMessage = "Loading assets";
        try
        {
            var records = await Task.Run(async () => await _repository.LoadAsync(AssetsFolderPath, cancellationToken).ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(true);

            Assets.Clear();
            foreach (var record in records)
            {
                Assets.Add(record);
            }

            RebuildFilterOptions();
            SetRangeDefaultsFromAssets();
            SelectedAsset = Assets.FirstOrDefault();
            LastRefreshText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            StatusMessage = $"Loaded {Assets.Count} assets";
            ApplyFilters();
            ExportCsvCommand.RaiseCanExecuteChanged();
            ExportSoftwareCsvCommand.RaiseCanExecuteChanged();
            ScanNetworkCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Load canceled";
        }
        catch (Exception ex)
        {
            StatusMessage = "Load failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ScanNetworkStatusAsync()
    {
        var assetsToScan = Assets.Where(asset => !string.IsNullOrWhiteSpace(asset.IpAddress)).ToArray();
        if (assetsToScan.Length == 0)
        {
            StatusMessage = "No IP addresses to scan";
            return;
        }

        IsNetworkScanBusy = true;
        StatusMessage = $"Scanning {assetsToScan.Length} assets with Nmap";
        foreach (var asset in assetsToScan)
        {
            asset.OnlineStatus = ScanStatus.Scanning;
            asset.Network.NmapScanOutput = "Scan queued.";
            asset.Network.NmapLastScanned = string.Empty;
        }

        AssetsView.Refresh();
        UpdateCounts();

        try
        {
            using var semaphore = new SemaphoreSlim(12);
            var tasks = assetsToScan.Select(async asset =>
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var result = await RunNmapScanAsync(asset.IpAddress).ConfigureAwait(false);
                    asset.OnlineStatus = result.Status;
                    asset.Network.NmapScanOutput = result.Output;
                    asset.Network.NmapLastScanned = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(true);
            StatusMessage = "Network scan complete";
        }
        catch (FileNotFoundException)
        {
            foreach (var asset in assetsToScan)
            {
                asset.OnlineStatus = ScanStatus.Unknown;
            }

            StatusMessage = $"Nmap was not found: {NmapPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Network scan failed: " + ex.Message;
        }
        finally
        {
            IsNetworkScanBusy = false;
            AssetsView.Refresh();
            OnPropertyChanged(nameof(SelectedAsset));
            UpdateCounts();
        }
    }

    private async Task<NetworkScanResult> RunNmapScanAsync(string ipAddress)
    {
        var arguments = SelectedNmapScanType.Equals("Full Scan", StringComparison.OrdinalIgnoreCase)
            ? $"-T4 -A -v -Pn {ipAddress}"
            : $"-sn -T4 {ipAddress}";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = NmapPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new FileNotFoundException("Nmap executable was not found.", NmapPath, ex);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var waitForExitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(waitForExitTask, Task.Delay(TimeSpan.FromSeconds(120))).ConfigureAwait(false);
        if (completed != waitForExitTask)
        {
            TryKill(process);
            return new NetworkScanResult(ScanStatus.Error, "Nmap scan timed out after 120 seconds.");
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var rawOutput = BuildNmapOutput(output, error);

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
        {
            return new NetworkScanResult(ScanStatus.Error, rawOutput);
        }

        if (output.Contains("Host is up", StringComparison.OrdinalIgnoreCase) ||
            (SelectedNmapScanType.Equals("Full Scan", StringComparison.OrdinalIgnoreCase) &&
             output.Contains("/open/", StringComparison.OrdinalIgnoreCase)))
        {
            return new NetworkScanResult(ScanStatus.Online, rawOutput);
        }

        if (output.Contains("Host seems down", StringComparison.OrdinalIgnoreCase))
        {
            return new NetworkScanResult(ScanStatus.Offline, rawOutput);
        }

        return new NetworkScanResult(ScanStatus.Offline, rawOutput);
    }

    private static string BuildNmapOutput(string output, string error)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(output))
        {
            builder.Append(output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append("stderr: ");
            builder.Append(error.Trim());
        }

        if (builder.Length == 0)
        {
            return "Nmap returned no output.";
        }

        var text = builder.ToString();
        return text.Length <= 4000 ? text : text[..4000] + Environment.NewLine + "... output truncated in UI";
    }

    private readonly record struct NetworkScanResult(ScanStatus Status, string Output);

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private bool FilterAsset(object item)
    {
        if (item is not AssetRecord asset)
        {
            return false;
        }

        if (!IsAll(SelectedOsFilter) && NormalizeOs(asset.OsVersion) != SelectedOsFilter)
        {
            return false;
        }

        if (!IsAll(SelectedManufacturerFilter) && !asset.Manufacturer.Equals(SelectedManufacturerFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsAll(SelectedStatusFilter) && !asset.OnlineStatus.ToString().Equals(SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (LowStorageOnly && (asset.CDriveFreeGb is not double freeGb || freeGb >= LowStorageThresholdGb))
        {
            return false;
        }

        if (asset.RamGb is double ramGb && (ramGb < MinRamFilterGb || ramGb > MaxRamFilterGb))
        {
            return false;
        }

        if (asset.CDriveFreeGb is double cFreeGb && (cFreeGb < MinStorageFilterGb || cFreeGb > MaxStorageFilterGb))
        {
            return false;
        }

        var anyDeskQuery = AnyDeskFilter.Trim();
        if (anyDeskQuery.Length > 0 && !Contains(asset.AnyDeskId, anyDeskQuery))
        {
            return false;
        }

        if (HasAnyDeskOnly && !asset.HasAnyDesk)
        {
            return false;
        }

        if (HasBitLockerOffOnly && !asset.HasBitLockerOff)
        {
            return false;
        }

        if (HasStoredCredentialsOnly && !asset.HasStoredCredentials)
        {
            return false;
        }

        var query = SearchText.Trim();
        return query.Length == 0 || MatchesSearch(asset, query);
    }

    private static bool MatchesSearch(AssetRecord asset, string query)
    {
        return Contains(asset.Hostname, query) ||
               Contains(asset.IpAddress, query) ||
               Contains(asset.MacAddress, query) ||
               Contains(asset.PcDomain, query) ||
               Contains(asset.WindowsAccount, query) ||
               Contains(asset.WindowsUserDisplay, query) ||
               Contains(asset.AnyDeskId, query) ||
               Contains(asset.OsVersion, query) ||
               Contains(asset.System.UserEmails, query) ||
               Contains(asset.Manufacturer, query) ||
               Contains(asset.Model, query) ||
               Contains(asset.SerialNumber, query) ||
               Contains(asset.Cpu, query) ||
               Contains(asset.Gpu, query) ||
               Contains(asset.Antivirus, query) ||
               Contains(asset.OfficeVersion, query) ||
               Contains(asset.Software.AdobeAutodesk, query) ||
               Contains(asset.Software.LocalUsers, query) ||
               Contains(asset.Network.NmapScanOutput, query) ||
               Contains(asset.SourceFileName, query) ||
               asset.SharedFolders.Any(folder => Contains(folder, query)) ||
               asset.StoredCredentials.Any(entry => Contains(entry.Target, query) || Contains(entry.User, query) || Contains(entry.Raw, query)) ||
               asset.SmbCredentials.Any(entry => Contains(entry.NasIp, query) || Contains(entry.StoredUser, query) || Contains(entry.ActiveConnection, query) || Contains(entry.Raw, query)) ||
               asset.BitLockerStatus.Any(volume => Contains(volume.Volume, query) || Contains(volume.Protection, query) || Contains(volume.Encryption, query) || Contains(volume.Raw, query)) ||
               asset.LocalDisks.Any(disk => Contains(disk.DriveLetter, query) || Contains(disk.DriveType, query) || Contains(disk.Raw, query)) ||
               asset.Software.InstalledPrinters.Any(printer => Contains(printer.Name, query)) ||
               asset.Software.InstalledPrograms.Any(program => Contains(program, query));
    }

    private static bool Contains(string? value, string query)
    {
        return value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ApplyFilters()
    {
        AssetsView.Refresh();
        UpdateCounts();
        EnsureVisibleSelection();
    }

    private void EnsureVisibleSelection()
    {
        var visibleAssets = AssetsView.Cast<AssetRecord>().ToArray();
        if (SelectedAsset is not null && visibleAssets.Contains(SelectedAsset))
        {
            ConnectSelectedAnyDeskCommand.RaiseCanExecuteChanged();
            return;
        }

        SelectedAsset = visibleAssets.FirstOrDefault();
    }

    private void UpdateCounts()
    {
        var visibleAssets = AssetsView.Cast<AssetRecord>().ToArray();
        LoadedCount = Assets.Count;
        FilteredCount = visibleAssets.Length;
        OnlineCount = visibleAssets.Count(asset => asset.OnlineStatus == ScanStatus.Online);
        OfflineCount = visibleAssets.Count(asset => asset.OnlineStatus == ScanStatus.Offline);
        UnknownCount = visibleAssets.Count(asset => asset.OnlineStatus == ScanStatus.Unknown);
        ErrorsCount = visibleAssets.Sum(asset => asset.ParseWarnings.Count);
        LowStorageCount = visibleAssets.Count(asset => asset.CDriveFreeGb is double freeGb && freeGb < LowStorageThresholdGb);
        AnyDeskReadyCount = visibleAssets.Count(asset => asset.HasAnyDesk);
        BitLockerOffCount = visibleAssets.Count(asset => asset.HasBitLockerOff);
        StoredCredentialAssetCount = visibleAssets.Count(asset => asset.HasStoredCredentials);
        TotalRamDisplay = FormatGb(visibleAssets.Sum(asset => asset.RamGb ?? 0d));
        TotalStorageDisplay = FormatGb(visibleAssets.SelectMany(asset => asset.LocalDisks).Sum(disk => disk.TotalGb ?? 0d));
        OsDistributionText = "OS: " + FormatDistribution(visibleAssets.Select(asset => NormalizeOs(asset.OsVersion)).Where(value => value.Length > 0));
        ManufacturerDistributionText = "Makers: " + FormatDistribution(visibleAssets.Select(asset => asset.Manufacturer).Where(value => value.Length > 0));
        StatusDistributionText = $"Status: {OnlineCount} online / {OfflineCount} offline / {UnknownCount} unknown";
        StorageHealthText = LowStorageCount == 0 ? "Storage: no low C: alerts" : $"Storage: {LowStorageCount} below {LowStorageThresholdGb:0.#} GB";
    }

    private void RebuildFilterOptions()
    {
        ReplaceOptions(OsOptions, Assets.Select(asset => NormalizeOs(asset.OsVersion)).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        ReplaceOptions(ManufacturerOptions, Assets.Select(asset => asset.Manufacturer).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

        if (!OsOptions.Contains(SelectedOsFilter))
        {
            SelectedOsFilter = AllFilter;
        }

        if (!ManufacturerOptions.Contains(SelectedManufacturerFilter))
        {
            SelectedManufacturerFilter = AllFilter;
        }
    }

    private void SetRangeDefaultsFromAssets()
    {
        var maxRam = Assets.Select(asset => asset.RamGb ?? 0d).DefaultIfEmpty(0d).Max();
        var maxStorage = Assets.Select(asset => asset.CDriveFreeGb ?? 0d).DefaultIfEmpty(0d).Max();

        _minRamFilterGb = 0d;
        _maxRamFilterGb = Math.Max(16d, Math.Ceiling(maxRam));
        _minStorageFilterGb = 0d;
        _maxStorageFilterGb = Math.Max(100d, Math.Ceiling(maxStorage));

        OnPropertyChanged(nameof(MinRamFilterGb));
        OnPropertyChanged(nameof(MaxRamFilterGb));
        OnPropertyChanged(nameof(MinStorageFilterGb));
        OnPropertyChanged(nameof(MaxStorageFilterGb));
    }

    private static void ReplaceOptions(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        target.Add(AllFilter);
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static string NormalizeOs(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Contains("Windows 11", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows 11";
        }

        if (value.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows 10";
        }

        if (value.Contains("Windows 8", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows 8";
        }

        if (value.Contains("Windows 7", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows 7";
        }

        if (value.Contains("Windows Server", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows Server";
        }

        return value;
    }

    private static bool IsAll(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Equals(AllFilter, StringComparison.OrdinalIgnoreCase);
    }

    private void ResetFilters()
    {
        _searchText = string.Empty;
        _anyDeskFilter = string.Empty;
        _selectedOsFilter = AllFilter;
        _selectedManufacturerFilter = AllFilter;
        _selectedStatusFilter = AllFilter;
        _lowStorageOnly = false;
        _hasAnyDeskOnly = false;
        _hasBitLockerOffOnly = false;
        _hasStoredCredentialsOnly = false;
        SetRangeDefaultsFromAssets();

        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(AnyDeskFilter));
        OnPropertyChanged(nameof(SelectedOsFilter));
        OnPropertyChanged(nameof(SelectedManufacturerFilter));
        OnPropertyChanged(nameof(SelectedStatusFilter));
        OnPropertyChanged(nameof(LowStorageOnly));
        OnPropertyChanged(nameof(HasAnyDeskOnly));
        OnPropertyChanged(nameof(HasBitLockerOffOnly));
        OnPropertyChanged(nameof(HasStoredCredentialsOnly));

        ApplyFilters();
        ClearSearchCommand.RaiseCanExecuteChanged();
    }

    private static string FormatGb(double value)
    {
        return value <= 0d ? "0 GB" : value.ToString("0.#", CultureInfo.InvariantCulture) + " GB";
    }

    private static string FormatDistribution(IEnumerable<string> values)
    {
        var groups = values.Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(group => $"{group.Key} {group.Count()}")
            .ToArray();

        return groups.Length == 0 ? "no data" : string.Join(" | ", groups);
    }

    private void OpenAssetsFolder()
    {
        Directory.CreateDirectory(AssetsFolderPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = AssetsFolderPath,
            UseShellExecute = true
        });
    }

    private void ExportFilteredCsv()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "cedx_asset_inventory_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var builder = new StringBuilder();
        AppendCsvRow(builder, "Hostname", "IP Address", "MAC Address", "Windows Account", "OS Version", "Manufacturer", "Model", "Serial Number", "CPU", "RAM GB", "C Free GB", "AnyDesk ID", "Antivirus", "BitLocker summary", "Network Mode", "Gateway", "DNS", "Online Status", "Source File");

        foreach (var asset in AssetsView.Cast<AssetRecord>())
        {
            AppendCsvRow(
                builder,
                asset.Hostname,
                asset.IpAddress,
                asset.MacAddress,
                asset.WindowsAccount,
                asset.OsVersion,
                asset.Manufacturer,
                asset.Model,
                asset.SerialNumber,
                asset.Cpu,
                asset.RamGb?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
                asset.CDriveFreeGb?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
                asset.AnyDeskId,
                asset.Antivirus,
                string.Join("; ", asset.BitLockerStatus.Select(volume => volume.Raw)),
                asset.Network.NetworkMode,
                asset.Network.DefaultGateway,
                asset.Network.DnsServers,
                asset.OnlineStatus.ToString(),
                asset.SourceFilePath);
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        StatusMessage = "Exported CSV";
    }

    private void ExportInstalledProgramsCsv()
    {
        var rows = AssetsView.Cast<AssetRecord>()
            .SelectMany(asset => asset.Software.InstalledPrograms.Select(program => new
            {
                asset.Hostname,
                asset.IpAddress,
                asset.WindowsAccount,
                asset.Manufacturer,
                asset.Model,
                Program = program,
                asset.SourceFilePath
            }))
            .ToArray();

        if (rows.Length == 0)
        {
            StatusMessage = "No installed-program lists to export";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = "cedx_installed_programs_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var builder = new StringBuilder();
        AppendCsvRow(builder, "Hostname", "IP Address", "Windows Account", "Manufacturer", "Model", "Program", "Source File");
        foreach (var row in rows)
        {
            AppendCsvRow(builder, row.Hostname, row.IpAddress, row.WindowsAccount, row.Manufacturer, row.Model, row.Program, row.SourceFilePath);
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        StatusMessage = $"Exported {rows.Length} software rows";
    }

    private static void AppendCsvRow(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return escaped.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + escaped + "\"" : escaped;
    }

    private void CopyText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Clipboard.SetText(text);
        StatusMessage = "Copied";
    }

    private void LaunchAnyDesk(string? anyDeskId)
    {
        if (!CanLaunchAnyDesk(anyDeskId))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "anydesk:" + anyDeskId,
            UseShellExecute = true
        });
    }

    private static bool CanLaunchAnyDesk(string? anyDeskId)
    {
        return !string.IsNullOrWhiteSpace(anyDeskId) &&
               !anyDeskId.Equals("N/A", StringComparison.OrdinalIgnoreCase) &&
               !anyDeskId.Equals("Not Found", StringComparison.OrdinalIgnoreCase);
    }
}
