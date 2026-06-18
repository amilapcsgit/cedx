using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using CedxNative.Models;
using CedxNative.Services;
using CedxNative.ViewModels;

namespace CedxNative.Vm;

public sealed class MainVm : ViewModelBase
{
    private readonly AssetRepository _repository = new();
    private string _searchText = string.Empty;
    private AssetRecord? _selectedAsset;
    private string _statusText = "Ready";
    private bool _isBusy;

    public MainVm()
    {
        AssetsView = CollectionViewSource.GetDefaultView(Assets);
        AssetsView.Filter = FilterAsset;
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync(), _ => !IsBusy);
        ClearFiltersCommand = new RelayCommand(_ => SearchText = string.Empty);
    }

    public ObservableCollection<AssetRecord> Assets { get; } = [];
    public ICollectionView AssetsView { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public string AssetsFolder { get; } = ResolveAssetsFolder();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                AssetsView.Refresh();
                UpdateStatusText();
            }
        }
    }

    public AssetRecord? SelectedAsset
    {
        get => _selectedAsset;
        set => SetProperty(ref _selectedAsset, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = "Loading assets...";
        try
        {
            var loaded = await _repository.LoadAsync(AssetsFolder);
            Assets.Clear();
            foreach (var asset in loaded)
            {
                Assets.Add(asset);
            }
            AssetsView.Refresh();
            UpdateStatusText();
        }
        catch (Exception ex)
        {
            StatusText = "Load failed: " + ex.Message;
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
        return string.IsNullOrWhiteSpace(SearchText) || asset.SearchBlob.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateStatusText()
    {
        var showing = AssetsView.Cast<AssetRecord>().Count();
        StatusText = $"Loaded {Assets.Count} | Showing {showing} | Assets folder: {AssetsFolder}";
    }

    private static string ResolveAssetsFolder()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "assets");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }
        return Path.Combine(Environment.CurrentDirectory, "assets");
    }
}
