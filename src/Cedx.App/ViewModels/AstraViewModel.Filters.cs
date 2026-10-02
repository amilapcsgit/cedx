using System.Collections.ObjectModel;
using Cedx.App.Common;
using Cedx.Core.Models;
using Cedx.Core.Parsing;

namespace Cedx.App.ViewModels;

public sealed class AstraQuickFilter(string field, string value, bool visible, Action changed) : ObservableObject
{
    private bool _visible = visible, _active;
    public string Field { get; } = field;
    public string Value { get; } = value;
    public string Label => $"{Field}: {Value}";
    public string Id => $"{Field}\u001f{Value}";
    public bool IsVisible { get => _visible; set { if (SetProperty(ref _visible, value)) changed(); } }
    public bool IsActive { get => _active; set { if (SetProperty(ref _active, value)) changed(); } }
}

public sealed partial class AstraViewModel
{
    private readonly List<(string Field, string Value)> _customFilters = [];
    private HashSet<string> _savedVisibleFilters = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasSavedFilterSettings;
    private string _newFilterField = "Any field", _newFilterValue = "";
    public string[] FilterFields { get; } = ["Any field", "Company", "Department", "Location", "Operating system", "Software", "Manufacturer", "Model"];
    public ObservableCollection<AstraQuickFilter> FilterChoices { get; } = [];
    public IReadOnlyList<AstraQuickFilter> VisibleFilters => FilterChoices.Where(x => x.IsVisible).ToArray();
    public IReadOnlyList<string> CompanySuggestions { get; private set; } = [];
    public IReadOnlyList<string> DepartmentSuggestions { get; private set; } = [];
    public IReadOnlyList<string> LocationSuggestions { get; private set; } = [];
    public string NewFilterField { get => _newFilterField; set => SetProperty(ref _newFilterField, value); }
    public string NewFilterValue { get => _newFilterValue; set => SetProperty(ref _newFilterValue, value); }
    public RelayCommand ToggleQuickFilterCommand { get; private set; } = null!;
    public RelayCommand AddQuickFilterCommand { get; private set; } = null!;
    public RelayCommand UseCompanyCommand { get; private set; } = null!;
    public RelayCommand UseDepartmentCommand { get; private set; } = null!;
    public RelayCommand UseLocationCommand { get; private set; } = null!;

    private void InitializeFilterCommands()
    {
        ToggleQuickFilterCommand = new(p => { if (p is AstraQuickFilter filter) filter.IsActive = !filter.IsActive; });
        AddQuickFilterCommand = new(_ =>
        {
            var value = NewFilterValue.Trim(); if (value.Length == 0) return;
            if (!FilterChoices.Any(x => x.Field.Equals(NewFilterField, StringComparison.OrdinalIgnoreCase) && x.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
                _customFilters.Add((NewFilterField, value));
            RebuildFilterChoices(NewFilterField + "\u001f" + value); NewFilterValue = "";
        });
        UseCompanyCommand = new(p => Inspector.EditCompany = p?.ToString() ?? "");
        UseDepartmentCommand = new(p => Inspector.EditDepartment = p?.ToString() ?? "");
        UseLocationCommand = new(p => Inspector.EditLocation = p?.ToString() ?? "");
    }

    private void RebuildFilterChoices(string? makeVisible = null)
    {
        var previous = FilterChoices.ToDictionary(x => x.Id, x => (x.IsVisible, x.IsActive), StringComparer.OrdinalIgnoreCase);
        var values = new List<(string Field, string Value)>();
        void Add(string field, IEnumerable<string> items) => values.AddRange(items.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => (field, x.Trim())));
        Add("Company", _stored.Select(x => x.Asset.Company)); Add("Department", _stored.Select(x => x.Asset.Department));
        Add("Location", _stored.Select(x => x.Asset.Location)); Add("Operating system", _stored.Concat(_staged).Select(x => x.Asset.OsShortDisplay));
        values.AddRange(_customFilters);
        FilterChoices.Clear();
        foreach (var item in values.Distinct().OrderBy(x => x.Field).ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase))
        {
            var id = item.Field + "\u001f" + item.Value; var state = previous.GetValueOrDefault(id);
            var choice = new AstraQuickFilter(item.Field, item.Value, id == makeVisible || state.IsVisible || _savedVisibleFilters.Contains(id) || (!_hasSavedFilterSettings && item.Field is "Company" or "Department" or "Operating system"), FilterChanged);
            choice.IsActive = state.IsActive; FilterChoices.Add(choice);
        }
        FilterChanged();
        CompanySuggestions = Suggestions(x => x.Asset.Company); DepartmentSuggestions = Suggestions(x => x.Asset.Department); LocationSuggestions = Suggestions(x => x.Asset.Location);
        OnPropertyChanged(nameof(CompanySuggestions)); OnPropertyChanged(nameof(DepartmentSuggestions)); OnPropertyChanged(nameof(LocationSuggestions));
    }

    private IReadOnlyList<string> Suggestions(Func<AstraItem, string> select) => _stored.Select(select).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    private void FilterChanged() { OnPropertyChanged(nameof(VisibleFilters)); Filter(); }
    private bool MatchesQuickFilters(AssetRecord asset) => FilterChoices.Where(x => x.IsActive).All(filter => FilterValue(asset, filter.Field).Contains(filter.Value, StringComparison.OrdinalIgnoreCase));
    private static string FilterValue(AssetRecord asset, string field) => field switch
    {
        "Company" => asset.Company, "Department" => asset.Department, "Location" => asset.Location,
        "Operating system" => asset.OsShortDisplay, "Software" => string.Join('\n', asset.Software.InstalledPrograms.Append(asset.Software.AdobeAutodesk).Append(asset.OfficeVersion)),
        "Manufacturer" => asset.Manufacturer, "Model" => asset.Model, _ => asset.SearchIndex + "\n" + AssetDetails.RedactKeys(asset.RawContent)
    };
}
