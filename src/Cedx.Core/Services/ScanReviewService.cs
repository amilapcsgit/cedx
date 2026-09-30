using System.Security.Cryptography;
using System.Text;
using Cedx.Core.Models;
using Cedx.Core.Parsing;
using Cedx.Core.Storage;

namespace Cedx.Core.Services;

public sealed record ReviewScan(AssetRecord Asset, string FileHash, string ContentHash)
{
    public string Identity => InventoryDatabase.Identity(Asset, false);
}
public sealed record RemovedScan(string OriginalPath, string RecoveryPath);
public sealed record ReviewFolder(IReadOnlyList<ReviewScan> Scans, IReadOnlyList<string> Errors);

/// <summary>Read-only staging and optimistic file operations. Reading never imports into SQLite.</summary>
public sealed class ScanReviewService
{
    private readonly AssetTextParser _parser = new();
    public ReviewScan Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var raw = FileAssetRepository.Decode(bytes);
        if (!AssetTextParser.IsInventoryReport(raw)) throw new InvalidDataException("This TXT is not an inventory report.");
        var a = _parser.Parse(raw, Path.GetFullPath(path), new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
        if (a.Hostname.StartsWith('$')) throw new InvalidDataException("Unexpanded collector template, not a scanned computer.");
        return new(a, Hash(bytes), Hash(Encoding.UTF8.GetBytes(raw)));
    }
    public ReviewFolder Scan(string folder)
    {
        var rows = new List<ReviewScan>(); var errors = new List<string>();
        if (!Directory.Exists(folder)) return new(rows, ["Folder unavailable: " + folder]);
        // Reparse-point directories are not followed. Recovery and backup files do not end in .txt.
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = false, AttributesToSkip = FileAttributes.ReparsePoint };
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*", options).Where(p => Path.GetExtension(p).Equals(".txt", StringComparison.OrdinalIgnoreCase)))
                try { rows.Add(Read(path)); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                { errors.Add($"{Path.GetFileName(path)}: {ex.Message}"); }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { errors.Add(ex.Message); }
        return new(rows.OrderByDescending(x => x.Asset.LastModified).ToArray(), errors);
    }
    public ReviewScan Verify(ReviewScan scan)
    {
        var current = Read(scan.Asset.SourceFilePath);
        if (current.FileHash != scan.FileHash) throw new IOException("The TXT changed since preview. Refresh and review it again.");
        return current;
    }
    public string Rename(ReviewScan scan, string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename != Path.GetFileName(filename) || filename.IndexOfAny("<>:\"/\\|?*".ToCharArray()) >= 0 || filename.Any(char.IsControl) || !filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || filename.Trim() != filename)
            throw new ArgumentException("Enter a filename ending in .txt, without a path or reserved characters.");
        Verify(scan);
        var target = Path.Combine(Path.GetDirectoryName(scan.Asset.SourceFilePath)!, filename);
        if (target.Equals(scan.Asset.SourceFilePath, StringComparison.Ordinal)) return target;
        File.Move(scan.Asset.SourceFilePath, target, false);
        return target;
    }
    public string Save(ReviewScan scan, string text)
    {
        if (!AssetTextParser.IsInventoryReport(text)) throw new InvalidDataException("The edited text must remain a valid inventory report.");
        var parsed = _parser.Parse(text, scan.Asset.SourceFilePath, DateTimeOffset.Now);
        if (parsed.Hostname.StartsWith('$')) throw new InvalidDataException("An unexpanded template cannot be saved as a scan.");
        Verify(scan);
        var path = scan.Asset.SourceFilePath;
        var backup = path + "." + Guid.NewGuid().ToString("N") + ".bak";
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(true));
            Verify(scan);
            File.Replace(temporary, path, backup);
            return backup;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public RemovedScan Remove(ReviewScan scan)
    {
        Verify(scan);
        var recovery = scan.Asset.SourceFilePath + "." + Guid.NewGuid().ToString("N") + ".removed";
        File.Move(scan.Asset.SourceFilePath, recovery, false);
        return new(scan.Asset.SourceFilePath, recovery);
    }
    public void Restore(RemovedScan removed) => File.Move(removed.RecoveryPath, removed.OriginalPath, false);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
