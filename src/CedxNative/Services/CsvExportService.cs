using System.Globalization;
using System.Text;
using CedxNative.Models;

namespace CedxNative.Services;

public sealed class CsvExportService
{
    public async Task ExportAsync(IEnumerable<AssetRecord> assets, string filePath, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Hostname,IP Address,Windows Account,OS Version,Manufacturer,Model,Serial Number,CPU,RAM GB,C Free GB,AnyDesk ID,Antivirus,BitLocker summary,Online Status,Source File");

        foreach (var asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AppendJoin(',',
                Escape(asset.Hostname),
                Escape(asset.IpAddress),
                Escape(asset.WindowsAccount),
                Escape(asset.OsVersion),
                Escape(asset.Manufacturer),
                Escape(asset.Model),
                Escape(asset.SerialNumber),
                Escape(asset.Cpu),
                Escape(asset.RamGb?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(asset.CDriveFreeGb?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty),
                Escape(asset.AnyDeskId),
                Escape(asset.Antivirus),
                Escape(asset.BitLockerSummary),
                Escape(asset.OnlineStatus),
                Escape(asset.SourceFilePath));
            builder.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
