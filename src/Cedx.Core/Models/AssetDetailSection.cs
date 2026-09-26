namespace Cedx.Core.Models;

public sealed record AssetDetailRow(string Record, string Field, string Value);

public sealed record AssetDetailSection(string Title, string Category, IReadOnlyList<AssetDetailRow> Rows, string RawContent)
{
    public override string ToString() => DisplayName;
    public string DisplayName => $"{Title} ({Rows.Count})";
}
