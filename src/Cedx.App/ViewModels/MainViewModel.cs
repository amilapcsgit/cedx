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
    private bool _isBusy;
    private string _assetsFolderPath;
    private string _lastRefreshText = "Not loaded";
    private string _statusMessage = "Ready";
    private int _loadedCount;
    private int _filteredCount;
    private int _onlineCount;
    private int _offlineCount;
    private int _errorsCount;

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

        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        ExportCsvCommand = new RelayCommand(_ => ExportFilteredCsv(), _ => Assets.Count > 0);
        OpenAssetsFolderCommand = new RelayCommand(_ => OpenAssetsFolder());
        CopyTextCommand = new RelayCommand(parameter => CopyText(parameter as string));
        LaunchAnyDeskCommand = new RelayCommand(parameter => LaunchAnyDesk(parameter as string), parameter => CanLaunchAnyDesk(parameter as string));
    }

    public ObservableCollection<AssetRecord> Assets { get; } = [];
    public ICollectionView AssetsView { get; }
    public ObservableCollection<string> OsOptions { get; } = [];
    public ObservableCollection<string> ManufacturerOptions { get; } = [];
    public ObservableCollection<string> StatusOptions { get; } = [];

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand OpenAssetsFolderCommand { get; }
    public RelayCommand CopyTextCommand { get; }
    public RelayCommand LaunchAnyDeskCommand { get; }

    public AssetRecord? SelectedAsset
    {
        get => _selectedAsset;
        set => SetProperty(ref _selectedAsset, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
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

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string AssetsFolderPath
    {
        get => _assetsFolderPath;
        set => SetProperty(ref _assetsFolderPath, value);
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

    public int ErrorsCount
    {
        get => _errorsCount;
        private set => SetProperty(ref _errorsCount, value);
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
            SelectedAsset = Assets.FirstOrDefault();
            LastRefreshText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            StatusMessage = $"Loaded {Assets.Count} assets";
            ApplyFilters();
            ExportCsvCommand.RaiseCanExecuteChanged();
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
               Contains(asset.PcDomain, query) ||
               Contains(asset.WindowsAccount, query) ||
               Contains(asset.AnyDeskId, query) ||
               Contains(asset.OsVersion, query) ||
               Contains(asset.Manufacturer, query) ||
               Contains(asset.Model, query) ||
               Contains(asset.SerialNumber, query) ||
               Contains(asset.Cpu, query) ||
               Contains(asset.Gpu, query) ||
               Contains(asset.Antivirus, query) ||
               Contains(asset.OfficeVersion, query) ||
               Contains(asset.Software.AdobeAutodesk, query) ||
               asset.Software.InstalledPrinters.Any(printer => Contains(printer.Name, query));
    }

    private static bool Contains(string? value, string query)
    {
        return value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void ApplyFilters()
    {
        AssetsView.Refresh();
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        LoadedCount = Assets.Count;
        FilteredCount = AssetsView.Cast<object>().Count();
        OnlineCount = Assets.Count(asset => asset.OnlineStatus == ScanStatus.Online);
        OfflineCount = Assets.Count(asset => asset.OnlineStatus == ScanStatus.Offline);
        ErrorsCount = Assets.Sum(asset => asset.ParseWarnings.Count);
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
        AppendCsvRow(builder, "Hostname", "IP Address", "Windows Account", "OS Version", "Manufacturer", "Model", "Serial Number", "CPU", "RAM GB", "C Free GB", "AnyDesk ID", "Antivirus", "BitLocker summary", "Online Status", "Source File");

        foreach (var asset in AssetsView.Cast<AssetRecord>())
        {
            AppendCsvRow(
                builder,
                asset.Hostname,
                asset.IpAddress,
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
                asset.OnlineStatus.ToString(),
                asset.SourceFilePath);
        }

        File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        StatusMessage = "Exported CSV";
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
