using Cedx.Core.Models;

namespace Cedx.App.ViewModels;

public sealed partial class MainViewModel
{
    private IReadOnlyList<InventorySegment> _osSegments = [];
    public IReadOnlyList<InventorySegment> OsSegments
    {
        get => _osSegments;
        private set => SetProperty(ref _osSegments, value);
    }
    public string VisibleHardwareCoverage { get; private set; } = "No measurements";
    public string VisibleStorageCoverage { get; private set; } = "No measurements";

    private void UpdateVisualSummary(AssetRecord[] visible)
    {
        OsSegments = visible.GroupBy(a => string.IsNullOrWhiteSpace(a.OsShortDisplay) ? "Unknown" : a.OsShortDisplay)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => new InventorySegment(g.Key, g.Count(), visible.Length == 0 ? 0 : 100d * g.Count() / visible.Length)).ToArray();
        VisibleHardwareCoverage = $"RAM reported: {visible.Count(a => a.RamGb.HasValue)} / {visible.Length}";
        VisibleStorageCoverage = $"C: reported: {visible.Count(a => a.CDriveFreeGb.HasValue)} / {visible.Length}";
        OnPropertyChanged(nameof(VisibleHardwareCoverage));
        OnPropertyChanged(nameof(VisibleStorageCoverage));
    }
}

public sealed record InventorySegment(string Label, int Count, double Percent);
