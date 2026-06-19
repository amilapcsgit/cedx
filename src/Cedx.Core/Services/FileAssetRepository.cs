using System.Text;
using Cedx.Core.Models;
using Cedx.Core.Parsing;

namespace Cedx.Core.Services;

public sealed class FileAssetRepository(IAssetParser parser) : IAssetRepository
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<IReadOnlyList<AssetRecord>> LoadAsync(string folderPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var gate = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount));
        var tasks = files.Select(file => LoadFileAsync(file, gate, cancellationToken)).ToArray();
        var records = await Task.WhenAll(tasks).ConfigureAwait(false);

        return records
            .Where(record => record is not null)
            .Select(record => record!)
            .OrderBy(record => record.Hostname, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<AssetRecord?> LoadFileAsync(string filePath, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var content = Decode(bytes);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var lastModified = File.GetLastWriteTimeUtc(filePath);
            return parser.Parse(content, filePath, new DateTimeOffset(lastModified, TimeSpan.Zero).ToLocalTime());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AssetRecord
            {
                SourceFilePath = filePath,
                SourceFileName = Path.GetFileName(filePath),
                LastModified = File.Exists(filePath)
                    ? new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero).ToLocalTime()
                    : DateTimeOffset.MinValue,
                System = new Cedx.Core.Models.SystemInfo { Hostname = Path.GetFileNameWithoutExtension(filePath) },
                ParseWarnings = [$"File could not be parsed: {ex.Message}"]
            };
        }
        finally
        {
            gate.Release();
        }
    }

    private static string Decode(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
