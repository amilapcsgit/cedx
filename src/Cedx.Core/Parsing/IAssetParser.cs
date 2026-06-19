using Cedx.Core.Models;

namespace Cedx.Core.Parsing;

public interface IAssetParser
{
    AssetRecord Parse(string content, string sourceFilePath, DateTimeOffset lastModified);
}
