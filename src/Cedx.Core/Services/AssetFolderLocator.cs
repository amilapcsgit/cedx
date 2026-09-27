namespace Cedx.Core.Services;
public static class AssetFolderLocator
{
    public static string FindDefaultFolder(params string[] startDirectories)
    {
        foreach (var start in startDirectories.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            for(var current=new DirectoryInfo(start);current is not null;current=current.Parent)
                foreach(var name in new[]{"Database","assets"})
                {
                    var folder=Path.Combine(current.FullName,name);
                    if(Directory.Exists(folder)&&Directory.EnumerateFiles(folder,"*.txt",SearchOption.AllDirectories).Any())return folder;
                }
        }
        return Path.Combine(startDirectories.FirstOrDefault()??Environment.CurrentDirectory,"Database");
    }
}
