namespace Cedx.Core.Storage;

public sealed record AssetAssignment(string Company = "", string Person = "", string Department = "", string Location = "", string AssetTag = "", string Notes = "", string Lifecycle = "In service");
public sealed record ImportItem(string File, string Result, string Message);
public sealed record ImportBatch(IReadOnlyList<ImportItem> Items)
{
    public int Added => Items.Count(x => x.Result == "Added");
    public int Updated => Items.Count(x => x.Result == "Updated");
    public int Unchanged => Items.Count(x => x.Result == "Unchanged");
    public int Errors => Items.Count(x => x.Result == "Error");
    public string Summary => $"{Items.Count} files: {Added} new, {Updated} updated, {Unchanged} unchanged, {Errors} errors";
}
public sealed record ScanRevision(string Date, string Source, string Hash, string RawText);
