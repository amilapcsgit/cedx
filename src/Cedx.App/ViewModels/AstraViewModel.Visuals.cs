using System.Globalization;
using System.Windows.Media;

namespace Cedx.App.ViewModels;

public sealed partial class AstraViewModel
{
    private double _cardWidth=240;
    public double CardWidth {get=>_cardWidth;set=>SetProperty(ref _cardWidth,Math.Clamp(value,220,360));}
    public IReadOnlyList<AstraOsSegment> OsSegments {get;private set;}=[];
    public int LowDiskCount=>Results.Count(x=>x.Asset.HasLowStorage);
    public string TotalMemory=>$"{Results.Sum(x=>x.Asset.RamGb??0):0.#} GB";
    public string MemoryCoverage=>$"RAM reported: {Results.Count(x=>x.Asset.RamGb is not null)} / {Results.Count}";
    public string RemoteRatio=>$"{ReadyCount}/{Results.Count}";
    public Geometry RemoteArc
    {
        get
        {
            if(ReadyCount==0||Results.Count==0)return Geometry.Empty;
            var angle=Math.PI*(1-(double)ReadyCount/Results.Count);
            var x=84+76*Math.Cos(angle);var y=84-76*Math.Sin(angle);
            return Geometry.Parse(FormattableString.Invariant($"M8,84 A76,76 0 0 1 {x:0.###},{y:0.###}"));
        }
    }
    private void UpdateVisuals()
    {
        OsSegments=Results.GroupBy(x=>string.IsNullOrWhiteSpace(x.Asset.OsShortDisplay)?"Not reported":x.Asset.OsShortDisplay)
            .OrderByDescending(g=>g.Count()).Select(g=>new AstraOsSegment(g.Key,g.Count(),Results.Count==0?0:100d*g.Count()/Results.Count)).ToArray();
        foreach(var name in new[]{nameof(OsSegments),nameof(LowDiskCount),nameof(TotalMemory),nameof(MemoryCoverage),nameof(RemoteRatio),nameof(RemoteArc)})OnPropertyChanged(name);
    }
}
public sealed record AstraOsSegment(string Label,int Count,double Percent);
