using CedxNative.Models;

namespace CedxNative.Services;

public sealed class AssetRepository
{
    private readonly AssetParserService _parser = new();

    public async Task<IReadOnlyList<AssetRecord>> LoadAsync(string assetsFolder, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(assetsFolder);

        var files = Directory.EnumerateFiles(assetsFolder, "*.txt", SearchOption.TopDirectoryOnly).OrderBy(x => x).ToArray();
        var results = new List<AssetRecord>(files.Length);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var asset = await _parser.ParseFileAsync(file, cancellationToken);
            if (asset is not null)
            {
                results.Add(asset);
            }
        }

        return results;
    }
}
