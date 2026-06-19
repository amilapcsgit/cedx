using Cedx.Core.Models;

namespace Cedx.Core.Services;

public interface IAssetRepository
{
    Task<IReadOnlyList<AssetRecord>> LoadAsync(string folderPath, CancellationToken cancellationToken);
}
