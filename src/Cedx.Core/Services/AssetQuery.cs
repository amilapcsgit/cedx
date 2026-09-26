using System.Text.RegularExpressions;
using Cedx.Core.Models;

namespace Cedx.Core.Services;

public sealed class AssetQuery
{
    public string Search { get; init; } = "";
    public string Os { get; init; } = "All";
    public string Manufacturer { get; init; } = "All";
    public string Status { get; init; } = "All";
    public string AnyDesk { get; init; } = "";
    public double MinRam { get; init; }
    public double MaxRam { get; init; } = double.MaxValue;
    public double MinFree { get; init; }
    public double MaxFree { get; init; } = double.MaxValue;
    public double LowThreshold { get; init; } = 10;
    public bool IncludeUnknown { get; init; } = true;
    public bool LowOnly { get; init; }
    public bool AnyDeskOnly { get; init; }
    public bool BitLockerOffOnly { get; init; }
    public bool CredentialsOnly { get; init; }
    public string Validation => new[]{MinRam,MaxRam,MinFree,MaxFree,LowThreshold}.Any(v=>!double.IsFinite(v)||v<0)
        ? "Use finite, non-negative numbers." : MinRam>MaxRam || MinFree>MaxFree ? "Minimum must not exceed maximum." : "";

    public bool Matches(AssetRecord a)
    {
        bool Selected(string f,string v)=>string.IsNullOrWhiteSpace(f)||f=="All"||f.Equals(v,StringComparison.OrdinalIgnoreCase);
        bool Range(double? n,double min,double max)=>n is double value?value>=min&&value<=max:IncludeUnknown;
        if(Validation.Length>0 || !Selected(Os,a.OsShortDisplay)||!Selected(Manufacturer,a.Manufacturer)||!Selected(Status,a.OnlineStatus.ToString()))return false;
        if(!Range(a.RamGb,MinRam,MaxRam)||!Range(a.CDriveFreeGb,MinFree,MaxFree))return false;
        if(LowOnly && (a.CDriveFreeGb is not double free || free>=LowThreshold))return false;
        if(AnyDeskOnly&&!a.HasAnyDesk || BitLockerOffOnly&&!a.HasBitLockerOff || CredentialsOnly&&!a.HasStoredCredentials)return false;
        if(!a.AnyDeskId.Contains(AnyDesk.Trim(),StringComparison.OrdinalIgnoreCase))return false;
        // All terms must match; quoted phrases stay together. No user input is evaluated as regex.
        var terms=Regex.Matches(Search.Trim(),"\"([^\"]+)\"|(\\S+)").Select(m=>m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value);
        return terms.All(t=>a.SearchIndex.Contains(t,StringComparison.OrdinalIgnoreCase));
    }
}
