using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Cedx.App.Common;
using Cedx.Core.Models;
using Cedx.Core.Parsing;
using Cedx.Core.Services;
using Cedx.Core.Storage;
using Microsoft.Win32;

namespace Cedx.App.ViewModels;

public sealed class AstraItem(AssetRecord asset, ReviewScan? source, bool stored, bool demo = false) : ObservableObject
{
    public AssetRecord Asset { get; } = asset;
    public ReviewScan? Source { get; } = source;
    public bool Stored { get; } = stored;
    public bool Demo { get; } = demo;
    public string Key => Stored ? Asset.AssetId : Asset.SourceFilePath;
    public string Person => string.IsNullOrWhiteSpace(Asset.OwnerDisplay) ? Asset.Hostname : Asset.OwnerDisplay;
    public string Emails => Asset.System.UserEmails;
    public string State { get; set; } = demo ? "DEMO / NOT STORED" : stored ? "CURRENT ASSET" : "AWAITING REVIEW";
    public bool Duplicate { get; set; }
    public string FileAge => $"TXT modified {Asset.LastModified:yyyy-MM-dd} · {Math.Max(0,(DateTimeOffset.Now-Asset.LastModified).Days)}d ago";
    public string SearchIndex => Asset.SearchIndex + "\n" + AssetDetails.RedactKeys(Asset.RawContent);
    private string _evidence = "";
    public string Evidence { get => _evidence; set => SetProperty(ref _evidence,value); }
}

public sealed class AstraViewModel : ObservableObject, IDisposable
{
    private readonly InventoryDatabase _database;
    private readonly ScanReviewService _review = new();
    private readonly string? _preferences;
    private IReadOnlyList<AstraItem> _stored = [], _staged = [], _demos = [], _results = [];
    private string _scope="Current assets", _search="", _folder, _status="Ready", _filename="", _fileErrors="";
    private AstraItem? _selected;
    private bool _busy, _remoteOnly, _duplicatesOnly, _showDetails=true, _pendingRefresh;
    private FileSystemWatcher? _watcher;
    private readonly DispatcherTimer _watchTimer = new() { Interval=TimeSpan.FromMilliseconds(900) };
    private RemovedScan? _removed;
    public MainViewModel Inspector { get; }
    public string[] Scopes { get; } = ["Current assets","Review TXT","Archived","Demo"];
    public IReadOnlyList<AstraItem> Results { get => _results; private set => SetProperty(ref _results,value); }
    public AstraItem? Selected
    {
        get => _selected;
        set { if(SetProperty(ref _selected,value)){Inspector.SelectedAsset=value?.Asset;Filename=value?.Asset.SourceFileName??"";OnPropertyChanged(nameof(SelectionHint));OnPropertyChanged(nameof(CanManage));OnPropertyChanged(nameof(CanModify));RaiseCommands();} }
    }
    public string Scope { get=>_scope; set { if(SetProperty(ref _scope,value)){DuplicatesOnly=false;Filter();OnPropertyChanged(nameof(IsReview));OnPropertyChanged(nameof(ScopeHint));OnPropertyChanged(nameof(CanModify));RaiseCommands();} } }
    public string Search { get=>_search; set {if(SetProperty(ref _search,value))Filter();} }
    public bool RemoteOnly { get=>_remoteOnly; set {if(SetProperty(ref _remoteOnly,value))Filter();} }
    public bool DuplicatesOnly { get=>_duplicatesOnly; set {if(SetProperty(ref _duplicatesOnly,value))Filter();} }
    public bool ShowDetails {get=>_showDetails;set=>SetProperty(ref _showDetails,value);}
    public bool IsReview=>Scope=="Review TXT";
    public bool IsBusy {get=>_busy;private set {if(SetProperty(ref _busy,value)){OnPropertyChanged(nameof(CanManage));OnPropertyChanged(nameof(CanModify));RaiseCommands();}}}
    public bool CanManage=>!IsBusy&&Selected?.Stored==true;
    public bool CanModify=>!IsBusy&&IsReview&&Selected?.Source is not null;
    public string SourceFolder {get=>_folder;set=>SetProperty(ref _folder,value);}
    public string Filename {get=>_filename;set=>SetProperty(ref _filename,value);}
    public string Status {get=>_status;private set=>SetProperty(ref _status,value);}
    public string FileErrors {get=>_fileErrors;private set=>SetProperty(ref _fileErrors,value);}
    public string DatabasePath=>_database.Path;
    public int CurrentCount=>_stored.Count(x=>x.Asset.Assignment.Lifecycle!="Retired");
    public int ReviewCount=>_staged.Count;
    public int ArchiveCount=>_stored.Count-CurrentCount;
    public int DuplicateCount=>_staged.Count(x=>x.Duplicate);
    public int ReadyCount=>Results.Count(x=>x.Asset.HasAnyDesk);
    public int OlderCount=>Results.Count(x=>(DateTimeOffset.Now-x.Asset.LastModified).TotalDays>90);
    public string ResultCount=>$"{Results.Count} results";
    public string ScopeHint=>Scope switch {"Review TXT"=>"Preview only. Approve one reviewed report to update the current inventory.","Archived"=>"Retired from the working inventory. Assignments and scan history are retained.","Demo"=>"Synthetic previews. These records are not stored in your inventory.",_=>"Find the person. Confirm the computer. Connect."};
    public string SelectionHint=>Selected is null?"Select a result":Selected.Stored?"Saved inventory snapshot":Selected.Demo?"Synthetic preview only":"Source preview. No database write until approval.";
    public AsyncRelayCommand RefreshCommand {get;}
    public AsyncRelayCommand ChooseFolderCommand {get;}
    public AsyncRelayCommand ApproveCommand {get;}
    public AsyncRelayCommand RenameCommand {get;}
    public AsyncRelayCommand RemoveCommand {get;}
    public AsyncRelayCommand UndoRemoveCommand {get;}
    public AsyncRelayCommand SaveAssignmentCommand {get;}
    public AsyncRelayCommand ArchiveCommand {get;}
    public AsyncRelayCommand RestoreCommand {get;}
    public RelayCommand ScopeCommand {get;}
    public RelayCommand OpenDetailsCommand {get;}
    public RelayCommand ClearCommand {get;}
    public RelayCommand ExportCommand {get;}
    public RelayCommand BackupCommand=>Inspector.BackupCommand;
    public event Action<ReviewScan>? EditRequested;
    public RelayCommand EditCommand {get;}

    public AstraViewModel(InventoryDatabase? database=null,string? folder=null,bool preferences=true)
    {
        var home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"cedx");
        _database=database??new InventoryDatabase(Path.Combine(home,"astra-assets.db"));
        _preferences=preferences?Path.Combine(home,"astra-workspace.json"):null;
        _folder=folder??AssetFolderLocator.FindDefaultFolder(Environment.CurrentDirectory,AppContext.BaseDirectory);
        try { if(_preferences is not null&&File.Exists(_preferences))_folder=JsonSerializer.Deserialize<string>(File.ReadAllText(_preferences))??_folder; }
        catch(Exception ex) when(ex is IOException or JsonException){_status="Source preference could not be loaded: "+ex.Message;}
        Inspector=new MainViewModel(new FileAssetRepository(new AssetTextParser()),_database,false){WatchFolder=false};
        RefreshCommand=new(_=>RefreshAsync());
        ChooseFolderCommand=new(async _=>{var d=new OpenFolderDialog{Title="Review TXT scan folder",InitialDirectory=Directory.Exists(SourceFolder)?SourceFolder:Environment.CurrentDirectory};if(d.ShowDialog()==true){SourceFolder=d.FolderName;Scope="Review TXT";await RefreshAsync();}},_=>!IsBusy);
        ApproveCommand=new(_=>ApproveAsync(),_=>CanModify);
        RenameCommand=new(_=>RenameAsync(),_=>CanModify);
        RemoveCommand=new(async _=>{if(Selected?.Source is not {} scan)return;if(MessageBox.Show($"Remove this TXT from the scan folder?\n\n{scan.Asset.SourceFilePath}\n\nIt will be preserved as a .removed recovery file. Existing database records remain unchanged.","Remove source TXT",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes)await RemoveAsync();},_=>CanModify);
        UndoRemoveCommand=new(_=>UndoRemoveAsync(),_=>!IsBusy&&_removed is not null);
        SaveAssignmentCommand=new(_=>SaveAssignmentAsync(),_=>CanManage);
        ArchiveCommand=new(_=>SetArchivedAsync(true),_=>CanManage&&Scope!="Archived");
        RestoreCommand=new(_=>SetArchivedAsync(false),_=>CanManage&&Scope=="Archived");
        ScopeCommand=new(p=>Scope=(string)p!);
        OpenDetailsCommand=new(p=>{if(p is AstraItem item)Selected=item;ShowDetails=true;});
        ClearCommand=new(_=>{Search="";RemoteOnly=false;DuplicatesOnly=false;});
        EditCommand=new(_=>{if(Selected?.Source is {} scan)EditRequested?.Invoke(scan);},_=>CanModify);
        ExportCommand=new(_=>Export(),_=>!IsBusy&&Results.Count>0);
        _watchTimer.Tick+=async (_,_)=>{_watchTimer.Stop();await RefreshAsync();};
    }
    public async Task InitializeAsync()
    {
        await RefreshAsync();
        if(CurrentCount==0)Scope=ReviewCount>0?"Review TXT":"Demo";
    }
    public Task RefreshAsync()=>Run(async()=>{await ReloadAsync();Status=$"{CurrentCount} current assets · {ReviewCount} source files · {FileErrors.Split('\n',StringSplitOptions.RemoveEmptyEntries).Length} file issues";});
    private async Task Run(Func<Task> action)
    {
        if(IsBusy){_pendingRefresh=true;return;}
        IsBusy=true;
        try{await action();}catch(Exception ex){Status="Operation failed: "+ex.Message;}
        finally{IsBusy=false;if(_pendingRefresh){_pendingRefresh=false;await RefreshAsync();}}
    }
    private async Task ReloadAsync()
    {
        var records=await Task.Run(()=>_database.Load());
        var folder=await Task.Run(()=>_review.Scan(SourceFolder));
        _stored=records.Select(a=>new AstraItem(a,null,true){State=a.Assignment.Lifecycle=="Retired"?"ARCHIVED":"CURRENT ASSET"}).ToArray();
        var identities=folder.Scans.GroupBy(x=>x.Identity).ToDictionary(g=>g.Key,g=>g.OrderByDescending(x=>x.Asset.LastModified).ToArray());
        var hashes=folder.Scans.GroupBy(x=>x.ContentHash).ToDictionary(g=>g.Key,g=>g.Count());
        _staged=folder.Scans.Select(scan=>{
            var group=identities[scan.Identity];var duplicate=group.Length>1||hashes[scan.ContentHash]>1;
            var state=hashes[scan.ContentHash]>1?$"IDENTICAL CONTENT · {hashes[scan.ContentHash]} FILES":group.Length>1?$"{(ReferenceEquals(group[0],scan)?"NEWEST":"OLDER")} · {group.Length} REPORTS":"AWAITING REVIEW";
            return new AstraItem(scan.Asset,scan,false){Duplicate=duplicate,State=state};
        }).ToArray();
        if(_demos.Count==0){var samples=Path.Combine(AppContext.BaseDirectory,"samples");if(Directory.Exists(samples))_demos=(await Task.Run(()=>_review.Scan(samples))).Scans.Select(x=>new AstraItem(x.Asset,null,false,true)).ToArray();}
        FileErrors=string.Join('\n',folder.Errors);
        Filter();StartWatcher();
        foreach(var property in new[]{nameof(CurrentCount),nameof(ReviewCount),nameof(ArchiveCount),nameof(DuplicateCount)})OnPropertyChanged(property);
    }
    private void Filter()
    {
        var key=Selected?.Key;
        var pool=Scope switch{"Review TXT"=>_staged,"Archived"=>_stored.Where(x=>x.Asset.Assignment.Lifecycle=="Retired"),"Demo"=>_demos,_=>_stored.Where(x=>x.Asset.Assignment.Lifecycle!="Retired")};
        var terms=Regex.Matches(Search.Trim(),"\"([^\"]+)\"|(\\S+)").Select(m=>m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value).ToArray();
        Results=pool.Where(x=>(!RemoteOnly||x.Asset.HasAnyDesk)&&(!DuplicatesOnly||!IsReview||x.Duplicate)&&terms.All(t=>x.SearchIndex.Contains(t,StringComparison.OrdinalIgnoreCase))).OrderBy(x=>x.Person,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.Asset.Hostname,StringComparer.OrdinalIgnoreCase).ThenByDescending(x=>x.Asset.LastModified).ToArray();
        foreach(var row in Results){var lines=AssetDetails.RedactKeys(row.Asset.RawContent).Replace("\0","").Split('\n');var evidence=terms.Length>0?lines.FirstOrDefault(l=>terms.Any(t=>l.Contains(t,StringComparison.OrdinalIgnoreCase))):null;row.Evidence=evidence?.Trim()??row.Asset.OrganizationDisplay;}
        Selected=Results.FirstOrDefault(x=>x.Key==key)??Results.FirstOrDefault();
        OnPropertyChanged(nameof(ResultCount));OnPropertyChanged(nameof(ReadyCount));OnPropertyChanged(nameof(OlderCount));RaiseCommands();
    }
    public Task ApproveAsync()=>Run(async()=>{
        if(Selected?.Source is not {} source)return;
        var verified=await Task.Run(()=>_review.Verify(source));
        var result=await Task.Run(()=>_database.Upsert(verified.Asset));
        var stored=(await Task.Run(()=>_database.Load())).Single(x=>InventoryDatabase.Identity(x,false)==source.Identity);
        if(!stored.IsManaged)_database.SaveAssignment(stored.AssetId,stored.Assignment);
        await ReloadAsync();Scope="Current assets";Search="";RemoteOnly=false;Selected=Results.FirstOrDefault(x=>x.Asset.AssetId==stored.AssetId);
        Status=$"{result}: {verified.Asset.Hostname}. Only this reviewed report was submitted.";
    });
    public Task SaveAssignmentAsync()=>Run(async()=>{if(Selected?.Stored!=true)return;Inspector.SaveSelectedAsset();await ReloadAsync();Status="Assignment saved.";});
    public Task RenameAsync()=>Run(async()=>{
        if(Selected?.Source is not {} source)return;
        var original=source.Asset.SourceFilePath;var target=await Task.Run(()=>_review.Rename(source,Filename));
        _database.RelinkSource(original,target);await ReloadAsync();Selected=Results.FirstOrDefault(x=>x.Asset.SourceFilePath==target);Status="TXT renamed; stored source references updated.";
    });
    public Task SaveTextAsync(ReviewScan source,string text)=>Run(async()=>{var backup=await Task.Run(()=>_review.Save(source,text));await ReloadAsync();Status="TXT saved. Review and approve to update inventory. Backup: "+backup;});
    public Task RemoveAsync()=>Run(async()=>{if(Selected?.Source is not {} source)return;_removed=await Task.Run(()=>_review.Remove(source));await ReloadAsync();Status="TXT removed from review. Undo removal is available; saved inventory is unchanged.";});
    public Task UndoRemoveAsync()=>Run(async()=>{if(_removed is null)return;await Task.Run(()=>_review.Restore(_removed));_removed=null;await ReloadAsync();Status="Source TXT restored.";});
    public Task SetArchivedAsync(bool archive)=>Run(async()=>{if(Selected?.Stored!=true)return;_database.SaveAssignment(Selected.Asset.AssetId,Selected.Asset.Assignment with{Lifecycle=archive?"Retired":"In service"});await ReloadAsync();Status=archive?"Asset archived. History and source TXT retained.":"Asset restored to current inventory.";});
    private void StartWatcher()
    {
        _watcher?.Dispose();_watcher=null;
        if(!Directory.Exists(SourceFolder))return;
        _watcher=new(SourceFolder){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size};
        FileSystemEventHandler handler=(_,e)=>{if(Path.GetExtension(e.FullPath).Equals(".txt",StringComparison.OrdinalIgnoreCase))Application.Current?.Dispatcher.InvokeAsync(()=>{_watchTimer.Stop();_watchTimer.Start();});};
        _watcher.Created+=handler;_watcher.Changed+=handler;_watcher.Deleted+=handler;_watcher.Renamed+=(_,e)=>{handler(_,e);if(Path.GetExtension(e.OldFullPath).Equals(".txt",StringComparison.OrdinalIgnoreCase))Application.Current?.Dispatcher.InvokeAsync(()=>{_watchTimer.Stop();_watchTimer.Start();});};
        _watcher.Error+=(_,_)=>Application.Current?.Dispatcher.InvokeAsync(()=>Status="Folder watcher interrupted. Press F5 to rescan.");_watcher.EnableRaisingEvents=true;
    }
    private void RaiseCommands()
    {
        foreach(var c in new[]{ChooseFolderCommand,ApproveCommand,RenameCommand,RemoveCommand,UndoRemoveCommand,SaveAssignmentCommand,ArchiveCommand,RestoreCommand})c?.RaiseCanExecuteChanged();
        EditCommand?.RaiseCanExecuteChanged();ExportCommand?.RaiseCanExecuteChanged();
    }
    public string CreateCsv()
    {
        static string Cell(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
        var b=new StringBuilder("Hostname,Person,Account,Emails,IP,AnyDesk,Company,Department,Source,State\r\n");
        foreach(var x in Results)b.AppendLine(string.Join(',',new[]{x.Asset.Hostname,x.Person,x.Asset.WindowsAccount,x.Emails,x.Asset.IpAddress,x.Asset.AnyDeskId,x.Asset.Company,x.Asset.Department,x.Asset.SourceFilePath,x.State}.Select(Cell)));
        return b.ToString();
    }
    private void Export()
    {
        try{var d=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="astra-inventory.csv"};if(d.ShowDialog()==true){File.WriteAllText(d.FileName,CreateCsv(),new UTF8Encoding(true));Status="Visible results exported.";}}
        catch(Exception ex){Status="Export failed: "+ex.Message;}
    }
    public void Dispose()
    {
        _watcher?.Dispose();_watchTimer.Stop();Inspector.DisposeWorkspace();
        try{if(_preferences is not null){Directory.CreateDirectory(Path.GetDirectoryName(_preferences)!);File.WriteAllText(_preferences,JsonSerializer.Serialize(SourceFolder));}}catch(IOException){}
    }
}
