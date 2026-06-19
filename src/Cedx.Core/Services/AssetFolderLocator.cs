namespace Cedx.Core.Services;

public static class AssetFolderLocator
{
    public static string FindDefaultFolder(params string[] startDirectories)
    {
        foreach (var startDirectory in startDirectories.Where(directory => !string.IsNullOrWhiteSpace(directory)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = new DirectoryInfo(startDirectory);
            while (current is not null)
            {
                var assets = Path.Combine(current.FullName, "assets");
                if (Directory.Exists(assets))
                {
                    return assets;
                }

                var database = Path.Combine(current.FullName, "Database");
                if (Directory.Exists(database))
                {
                    return database;
                }

                current = current.Parent;
            }
        }

        var fallbackRoot = startDirectories.FirstOrDefault(directory => !string.IsNullOrWhiteSpace(directory)) ?? Environment.CurrentDirectory;
        return Path.Combine(fallbackRoot, "assets");
    }
}
