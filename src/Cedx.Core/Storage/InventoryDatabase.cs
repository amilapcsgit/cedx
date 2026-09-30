using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cedx.Core.Models;
using Cedx.Core.Parsing;
using Cedx.Core.Services;
using Microsoft.Data.Sqlite;

namespace Cedx.Core.Storage;

/// <summary>One local SQLite database, parameterized SQL, transactional imports and online backups.</summary>
public sealed class InventoryDatabase
{
    public string Path { get; }
    private readonly AssetTextParser _parser = new();
    public InventoryDatabase(string path) { Path = System.IO.Path.GetFullPath(path); }
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, DefaultTimeout = 10 }.ToString());
        db.Open();
        using var c = db.CreateCommand();
        c.CommandText = """
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS assets (
              id TEXT PRIMARY KEY, identity TEXT NOT NULL UNIQUE, raw TEXT NOT NULL,
              source TEXT NOT NULL, scanned TEXT NOT NULL, hash TEXT NOT NULL,
              managed INTEGER NOT NULL DEFAULT 0, sample INTEGER NOT NULL DEFAULT 0,
              assignment TEXT NOT NULL DEFAULT '{}');
            CREATE TABLE IF NOT EXISTS revisions (
              asset_id TEXT NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
              hash TEXT NOT NULL, scanned TEXT NOT NULL, source TEXT NOT NULL, raw TEXT NOT NULL,
              PRIMARY KEY(asset_id, hash));
            PRAGMA user_version=1;
            """;
        c.ExecuteNonQuery();
        return db;
    }
    public IReadOnlyList<AssetRecord> Load()
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "SELECT a.id,a.identity,CAST(a.raw AS BLOB),a.source,a.scanned,a.hash,a.managed,a.sample,a.assignment, (SELECT COUNT(*) FROM revisions r WHERE r.asset_id=a.id) AS revision_count FROM assets a";
        using var reader = c.ExecuteReader(); var records = new List<AssetRecord>();
        while (reader.Read())
        {
            var record = _parser.Parse(ReadRaw(reader, 2), reader.GetString(3), DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture));
            record.AssetId = reader.GetString(0); record.IsManaged = reader.GetInt32(6) == 1; record.IsSample = reader.GetInt32(7) == 1;
            record.Assignment = JsonSerializer.Deserialize<AssetAssignment>(reader.GetString(8)) ?? new();
            record.RevisionCount = reader.GetInt32(9);
            record.SearchIndex += "\n" + string.Join('\n', record.Company, record.Person, record.Department, record.Location, record.AssetTag, record.Assignment.Notes);
            records.Add(record);
        }
        return records.OrderBy(x => x.Hostname, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public ImportBatch Import(IEnumerable<string> paths, bool sample = false)
    {
        var outcomes = new List<ImportItem>();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var text = FileAssetRepository.Decode(File.ReadAllBytes(path));
                if (!AssetTextParser.IsInventoryReport(text)) throw new InvalidDataException("No asset identity or inventory fields. File skipped.");
                var record = _parser.Parse(text, path, new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
                outcomes.Add(new(path, Upsert(record, sample), record.ParseWarnings.Count == 0 ? record.Hostname : string.Join("; ", record.ParseWarnings)));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException or ArgumentException or FormatException)
            { outcomes.Add(new(path, "Error", ex.Message)); }
        }
        return new(outcomes);
    }
    public string Upsert(AssetRecord record, bool sample = false)
    {
        var identity = Identity(record, sample);
        var rawBytes = Encoding.UTF8.GetBytes(record.RawContent);
        var hash = Convert.ToHexString(SHA256.HashData(rawBytes));
        using var db = Open(); using var tx = db.BeginTransaction();
        using var find = db.CreateCommand(); find.Transaction = tx;
        find.CommandText = "SELECT id,hash,scanned,CAST(raw AS BLOB) FROM assets WHERE identity=$identity"; find.Parameters.AddWithValue("$identity", identity);
        string? id = null, existingHash = null, scanned = null; byte[]? existingRaw = null;
        using (var r = find.ExecuteReader()) if (r.Read()) { id = r.GetString(0); existingHash = r.GetString(1); scanned = r.GetString(2); existingRaw = r.GetFieldValue<byte[]>(3); }
        // Older builds could persist only the prefix before a NUL while hashing the full report.
        // Compare the bytes too, so an unchanged source repairs that row without changing identity.
        if (existingHash == hash && existingRaw!.AsSpan().SequenceEqual(rawBytes)) { tx.Commit(); return "Unchanged"; }
        var isNew = id is null; id ??= Guid.NewGuid().ToString("N");
        var newer = isNew || existingHash == hash || record.LastModified >= DateTimeOffset.Parse(scanned!, CultureInfo.InvariantCulture);
        using var c = db.CreateCommand(); c.Transaction = tx;
        c.CommandText = isNew
            ? "INSERT INTO assets(id,identity,raw,source,scanned,hash,sample) VALUES($id,$identity,$raw,$source,$scanned,$hash,$sample)"
            : newer ? "UPDATE assets SET raw=$raw,source=$source,scanned=$scanned,hash=$hash WHERE id=$id" : "SELECT 1";
        c.Parameters.AddWithValue("$id", id); c.Parameters.AddWithValue("$identity", identity); c.Parameters.Add("$raw", SqliteType.Blob).Value = rawBytes;
        c.Parameters.AddWithValue("$source", record.SourceFilePath); c.Parameters.AddWithValue("$scanned", record.LastModified.ToString("O"));
        c.Parameters.AddWithValue("$hash", hash); c.Parameters.AddWithValue("$sample", sample ? 1 : 0); c.ExecuteNonQuery();
        using var history = db.CreateCommand(); history.Transaction = tx;
        history.CommandText = "INSERT INTO revisions(asset_id,hash,scanned,source,raw) VALUES($id,$hash,$scanned,$source,$raw) ON CONFLICT(asset_id,hash) DO UPDATE SET raw=excluded.raw";
        history.Parameters.AddWithValue("$id", id); history.Parameters.AddWithValue("$hash", hash); history.Parameters.AddWithValue("$scanned", record.LastModified.ToString("O"));
        history.Parameters.AddWithValue("$source", record.SourceFilePath); history.Parameters.Add("$raw", SqliteType.Blob).Value = rawBytes; history.ExecuteNonQuery();
        tx.Commit(); return isNew ? "Added" : newer ? "Updated" : "History only";
    }
    public static string Identity(AssetRecord a, bool sample)
    {
        // Host + domain, never IP: DHCP changes must not create a new asset. Different non-placeholder serials stay separate.
        var serial = a.SerialNumber.Trim();
        if (new[]{"", "unknown", "n/a", "none", "default string", "system serial number", "to be filled by o.e.m.", "0"}.Contains(serial, StringComparer.OrdinalIgnoreCase)) serial = "";
        return $"{sample}|{a.PcDomain.Trim().ToUpperInvariant()}|{a.Hostname.Trim().ToUpperInvariant()}|{serial.ToUpperInvariant()}";
    }
    public void RelinkSource(string originalPath, string newPath)
    {
        using var db = Open(); using var tx = db.BeginTransaction(); using var c = db.CreateCommand(); c.Transaction = tx;
        c.CommandText = "UPDATE assets SET source=$new WHERE source=$old; UPDATE revisions SET source=$new WHERE source=$old;";
        c.Parameters.AddWithValue("$old", originalPath); c.Parameters.AddWithValue("$new", newPath);
        c.ExecuteNonQuery(); tx.Commit();
    }
    public void SaveAssignment(string id, AssetAssignment assignment)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "UPDATE assets SET managed=1,assignment=$assignment WHERE id=$id";
        c.Parameters.AddWithValue("$id", id); c.Parameters.AddWithValue("$assignment", JsonSerializer.Serialize(assignment));
        if (c.ExecuteNonQuery() != 1) throw new InvalidOperationException("Asset not found.");
    }
    public IReadOnlyList<ScanRevision> History(string id)
    {
        using var db = Open(); using var c = db.CreateCommand();
        c.CommandText = "SELECT scanned,source,hash,CAST(raw AS BLOB) FROM revisions WHERE asset_id=$id ORDER BY scanned DESC"; c.Parameters.AddWithValue("$id", id);
        using var r = c.ExecuteReader(); var rows = new List<ScanRevision>();
        while (r.Read()) rows.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), ReadRaw(r, 3)));
        return rows;
    }
    // The collector can include U+0000 in EDID monitor names. GetString uses a
    // NUL-terminated native text path in some runtime paths. Explicit UTF-8 byte
    // storage and reads preserve new BLOB rows and complete legacy TEXT rows.
    private static string ReadRaw(SqliteDataReader reader, int ordinal) =>
        Encoding.UTF8.GetString(reader.GetFieldValue<byte[]>(ordinal));

    public void Backup(string destination)
    {
        if (System.IO.Path.GetFullPath(destination).Equals(Path, StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a different backup file.");
        using var db = Open(); using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination }.ToString());
        backup.Open(); db.BackupDatabase(backup);
    }
}
