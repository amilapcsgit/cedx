using Cedx.Core.Parsing;
using Cedx.Core.Services;

var folder = args.Length > 0
    ? args[0]
    : AssetFolderLocator.FindDefaultFolder(Environment.CurrentDirectory, AppContext.BaseDirectory);

var parser = new AssetTextParser();
var repository = new FileAssetRepository(parser);
var records = await repository.LoadAsync(folder, CancellationToken.None);

Console.WriteLine($"Folder: {folder}");
Console.WriteLine($"Assets loaded: {records.Count}");
Console.WriteLine($"Parse warnings: {records.Sum(record => record.ParseWarnings.Count)}");

var withRam = records.Count(record => record.RamGb is not null);
var withCDrive = records.Count(record => record.CDriveFreeGb is not null);
var withCredentials = records.Count(record => record.StoredCredentials.Count > 0);
var withWinRm = records.Count(record => !string.IsNullOrWhiteSpace(record.WinRmCommand));

Console.WriteLine($"RAM parsed: {withRam}");
Console.WriteLine($"C: free parsed: {withCDrive}");
Console.WriteLine($"Stored credentials sections: {withCredentials}");
Console.WriteLine($"WinRM commands: {withWinRm}");

foreach (var record in records.Take(5))
{
    Console.WriteLine($"{record.Hostname} | {record.IpAddress} | RAM {record.RamDisplay} | C: {record.CDriveFreeDisplay}");
}

if (records.Count == 0)
{
    Console.Error.WriteLine("No asset records were loaded.");
    return 1;
}

if (withRam == 0 || withCDrive == 0)
{
    Console.Error.WriteLine("Expected RAM and local disk data were not parsed.");
    return 2;
}

return 0;
