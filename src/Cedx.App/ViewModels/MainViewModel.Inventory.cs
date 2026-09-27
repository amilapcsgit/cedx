using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Cedx.App.Common;
using Cedx.Core.Models;
using Cedx.Core.Storage;
using Microsoft.Win32;

namespace Cedx.App.ViewModels;

public sealed partial class MainViewModel
{
    private readonly InventoryDatabase _database;
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _watchTimer;
    private bool _reloadPending;
    private string _inventoryScope = "All assets", _companyFilter = "All", _departmentFilter = "All";
    private string _lastImportSummary = "Import TXT reports to build your inventory.";
    private bool _watchFolder = true;
    private bool _showInspector = true;
    private int _inspectorTab;
    private bool _loadingDraft;
    private readonly Dictionary<string, AssetAssignment> _drafts = new();
    private string _editCompany="", _editPerson="", _editDepartment="", _editLocation="", _editTag="", _editNotes="", _editLifecycle="In service";
    public string DatabasePath => _database.Path;
    public string[] InventoryScopes { get; } = ["All assets", "Managed assets", "Scan inbox", "Demo samples"];
    public string[] Lifecycles { get; } = ["In service", "Spare", "Repair", "Retired"];
    public ObservableCollection<string> CompanyOptions { get; } = ["All"];
    public ObservableCollection<string> DepartmentOptions { get; } = ["All"];
    public ObservableCollection<ImportItem> ImportResults { get; } = [];
    public ObservableCollection<ScanRevision> ScanHistory { get; } = [];
    public int ManagedCount => Assets.Count(a=>a.IsManaged && !a.IsSample);
    public int InboxCount => Assets.Count(a=>!a.IsManaged && !a.IsSample);
    public int DemoCount => Assets.Count(a=>a.IsSample);
    public string InventoryScope {get=>_inventoryScope;set{if(SetProperty(ref _inventoryScope,value))ApplyFilters();}}
    public string CompanyFilter {get=>_companyFilter;set{if(SetProperty(ref _companyFilter,value))ApplyFilters();}}
    public string DepartmentFilter {get=>_departmentFilter;set{if(SetProperty(ref _departmentFilter,value))ApplyFilters();}}
    public bool WatchFolder {get=>_watchFolder;set{if(SetProperty(ref _watchFolder,value))StartFolderWatcher();}}
    public bool ShowInspector {get=>_showInspector;set=>SetProperty(ref _showInspector,value);}
    public int InspectorTab {get=>_inspectorTab;set=>SetProperty(ref _inspectorTab,value);}
    public string LastImportSummary {get=>_lastImportSummary;private set=>SetProperty(ref _lastImportSummary,value);}
    public string EditCompany {get=>_editCompany;set{if(SetProperty(ref _editCompany,value))RememberDraft();}}
    public string EditPerson {get=>_editPerson;set{if(SetProperty(ref _editPerson,value))RememberDraft();}}
    public string EditDepartment {get=>_editDepartment;set{if(SetProperty(ref _editDepartment,value))RememberDraft();}}
    public string EditLocation {get=>_editLocation;set{if(SetProperty(ref _editLocation,value))RememberDraft();}}
    public string EditTag {get=>_editTag;set{if(SetProperty(ref _editTag,value))RememberDraft();}}
    public string EditNotes {get=>_editNotes;set{if(SetProperty(ref _editNotes,value))RememberDraft();}}
    public string EditLifecycle {get=>_editLifecycle;set{if(SetProperty(ref _editLifecycle,value))RememberDraft();}}
    public bool HasUnsavedChanges => _drafts.Count>0;
    public string DraftStatus => SelectedAsset is null ? "Select an asset" : _drafts.ContainsKey(SelectedAsset.AssetId) ? "Unsaved changes. Save to keep this assignment." : SelectedAsset.IsManaged ? "Assignment saved in local database" : "Scan only. Add assignment and save as a managed asset.";
    public string SaveAssetLabel => SelectedAsset?.IsManaged==true ? "Save changes" : "Save as managed asset";
    public AsyncRelayCommand ImportFilesCommand {get;private set;}=null!;
    public RelayCommand SaveAssetCommand {get;private set;}=null!;
    public RelayCommand DiscardDraftCommand {get;private set;}=null!;
    public RelayCommand BackupCommand {get;private set;}=null!;
    public RelayCommand ExportJsonCommand {get;private set;}=null!;
    public RelayCommand ExportReportCommand {get;private set;}=null!;
    public RelayCommand ScopeCommand {get;private set;}=null!;
    public RelayCommand ManageAssetCommand {get;private set;}=null!;
    public RelayCommand ToggleInspectorCommand {get;private set;}=null!;
    public RelayCommand LoadSamplesCommand {get;private set;}=null!;
    public RelayCommand OpenAssetCommand {get;private set;}=null!;
    public RelayCommand ViewHistoryCommand {get;private set;}=null!;
    private void InitializeInventory()
    {
        ImportFilesCommand = new AsyncRelayCommand(async _=>{
            var dialog=new OpenFileDialog{Filter="Asset scans (*.txt)|*.txt",Multiselect=true,Title="Import asset scans"};
            if(dialog.ShowDialog()==true)await ImportFilesAsync(dialog.FileNames);
        },_=>!IsBusy);
        SaveAssetCommand=new RelayCommand(_=>ExecuteFileAction(SaveSelectedAsset),_=>SelectedAsset is not null&&!IsBusy);
        DiscardDraftCommand=new RelayCommand(_=>{if(SelectedAsset is not null)_drafts.Remove(SelectedAsset.AssetId);LoadAssignmentDraft();});
        BackupCommand=new RelayCommand(_=>ExecuteFileAction(()=>{
            var d=new SaveFileDialog{Filter="SQLite backup (*.db)|*.db",FileName="cedx-backup.db"};
            if(d.ShowDialog()==true){_database.Backup(d.FileName);StatusMessage="Database backup saved.";}
        }),_=>!IsBusy);
        ExportJsonCommand=new RelayCommand(_=>ExecuteFileAction(ExportJson));
        ExportReportCommand=new RelayCommand(_=>ExecuteFileAction(()=>{
            if(SelectedAsset is null)return;var d=new SaveFileDialog{Filter="TXT (*.txt)|*.txt",FileName=SelectedAsset.SourceFileName};
            if(d.ShowDialog()==true){File.WriteAllText(d.FileName,Display(SelectedAsset.RawContent),new UTF8Encoding(true));StatusMessage="Snapshot exported.";}
        }),_=>SelectedAsset is not null);
        OpenAssetCommand=new RelayCommand(p=>{if(p is AssetRecord a)SelectedAsset=a;ShowInspector=true;InspectorTab=0;});
        ScopeCommand=new RelayCommand(p=>{InventoryScope=p as string??"All assets";SearchText="";});
        ManageAssetCommand=new RelayCommand(_=>{ShowInspector=true;InspectorTab=1;},_=>SelectedAsset is not null);
        ToggleInspectorCommand=new RelayCommand(_=>ShowInspector=!ShowInspector);
        LoadSamplesCommand=new RelayCommand(async _=>{
            var folder=Path.Combine(AppContext.BaseDirectory,"samples");
            if(Directory.Exists(folder)){await ImportFilesAsync(InventoryFiles(folder),true);InventoryScope="Demo samples";}
        },_=>!IsBusy);
        ViewHistoryCommand=new RelayCommand(p=>ExecuteFileAction(()=>{if(p is ScanRevision h){var path=Path.Combine(Path.GetTempPath(),"cedx-history.txt");File.WriteAllText(path,Display(h.RawText));System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe"){ArgumentList={path}});}}));
    }
    private void InventoryCommandsChanged()
    {ImportFilesCommand?.RaiseCanExecuteChanged();SaveAssetCommand?.RaiseCanExecuteChanged();BackupCommand?.RaiseCanExecuteChanged();LoadSamplesCommand?.RaiseCanExecuteChanged();}
    private static string[] InventoryFiles(string folder) => Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).Where(p=>Path.GetExtension(p).Equals(".txt",StringComparison.OrdinalIgnoreCase)).ToArray();
    public async Task ImportFilesAsync(IEnumerable<string> files, bool sample=false)
    {
        if(IsBusy)return;
        IsBusy=true;
        try{
            var paths=files.SelectMany(p=>Directory.Exists(p)?InventoryFiles(p):new[]{p}).ToArray();
            var result=await Task.Run(()=>_database.Import(paths,sample));ShowImportResult(result);
            var records=await Task.Run(()=>_database.Load());
            ResetFilters();ReplaceAssets(records,SelectedAsset?.AssetId);StatusMessage=result.Summary;
        }catch(Exception ex){StatusMessage="Import failed: "+ex.Message;}
        finally{IsBusy=false;if(_reloadPending){_reloadPending=false;await RefreshAsync();}}
    }
    private void ShowImportResult(ImportBatch result)
    {ImportResults.Clear();foreach(var item in result.Items)ImportResults.Add(item);LastImportSummary=result.Summary;}
    private bool MatchesInventoryFilters(AssetRecord a)
    {
        bool scope=InventoryScope switch{"Managed assets"=>a.IsManaged&&!a.IsSample,"Scan inbox"=>!a.IsManaged&&!a.IsSample,"Demo samples"=>a.IsSample,_=>!a.IsSample||Assets.All(x=>x.IsSample)};
        return scope&&(CompanyFilter=="All"||CompanyFilter==a.Company)&&(DepartmentFilter=="All"||DepartmentFilter==a.Department);
    }
    private void RebuildOrganizationFilters()
    {
        ReplaceOptions(CompanyOptions,Assets.Select(a=>a.Company).Where(s=>s.Length>0).Distinct().Order());
        ReplaceOptions(DepartmentOptions,Assets.Select(a=>a.Department).Where(s=>s.Length>0).Distinct().Order());
        if(!CompanyOptions.Contains(_companyFilter))CompanyFilter="All";
        if(!DepartmentOptions.Contains(_departmentFilter))DepartmentFilter="All";
    }
    private AssetAssignment CurrentAssignment()=>new(EditCompany,EditPerson,EditDepartment,EditLocation,EditTag,EditNotes,EditLifecycle);
    private void RememberDraft()
    {
        if(_loadingDraft||SelectedAsset is null)return;
        if(CurrentAssignment()==SelectedAsset.Assignment)_drafts.Remove(SelectedAsset.AssetId);else _drafts[SelectedAsset.AssetId]=CurrentAssignment();
        OnPropertyChanged(nameof(DraftStatus));OnPropertyChanged(nameof(HasUnsavedChanges));
    }
    private void LoadAssignmentDraft()
    {
        _loadingDraft=true;
        var a=SelectedAsset is null?new AssetAssignment():_drafts.GetValueOrDefault(SelectedAsset.AssetId)??SelectedAsset.Assignment;
        EditCompany=a.Company;EditPerson=a.Person;EditDepartment=a.Department;EditLocation=a.Location;EditTag=a.AssetTag;EditNotes=a.Notes;EditLifecycle=a.Lifecycle;
        _loadingDraft=false;ScanHistory.Clear();
        if(SelectedAsset is not null)foreach(var h in _database.History(SelectedAsset.AssetId))ScanHistory.Add(h);
        OnPropertyChanged(nameof(DraftStatus));OnPropertyChanged(nameof(SaveAssetLabel));OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveAssetCommand?.RaiseCanExecuteChanged();ManageAssetCommand?.RaiseCanExecuteChanged();ExportReportCommand?.RaiseCanExecuteChanged();
    }
    public void SaveSelectedAsset()
    {
        if(SelectedAsset is null)return;
        var id=SelectedAsset.AssetId;_database.SaveAssignment(id,CurrentAssignment());_drafts.Remove(id);
        // Keep the saved asset visible when converting out of the scan inbox.
        if(InventoryScope=="Scan inbox")_inventoryScope="Managed assets";
        _companyFilter="All";_departmentFilter="All";
        OnPropertyChanged(nameof(InventoryScope));OnPropertyChanged(nameof(CompanyFilter));OnPropertyChanged(nameof(DepartmentFilter));
        ReplaceAssets(_database.Load(),id);StatusMessage="Asset saved to local database.";
    }
    private void ExportJson()
    {
        var d=new SaveFileDialog{Filter="JSON (*.json)|*.json",FileName="cedx-inventory.json"};if(d.ShowDialog()!=true)return;
        var records=AssetsView.Cast<AssetRecord>().Select(a=>new{a.AssetId,a.Hostname,a.IpAddress,a.IsManaged,a.IsSample,a.Assignment,a.LastModified,a.RevisionCount,Report=a.AssetId==SelectedAsset?.AssetId&&RevealKeys?a.RawContent:Cedx.Core.Parsing.AssetDetails.RedactKeys(a.RawContent)});
        File.WriteAllText(d.FileName,JsonSerializer.Serialize(records,new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(true));StatusMessage="Visible inventory exported.";
    }
    private void StartFolderWatcher()
    {
        _watcher?.Dispose();_watcher=null;
        if(!WatchFolder||!Directory.Exists(AssetsFolderPath))return;
        _watchTimer??=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(900)};
        _watchTimer.Tick-=WatchTick;_watchTimer.Tick+=WatchTick;
        _watcher=new FileSystemWatcher(AssetsFolderPath){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size};
        FileSystemEventHandler change=(_,e)=>{if(Path.GetExtension(e.FullPath).Equals(".txt",StringComparison.OrdinalIgnoreCase))Application.Current?.Dispatcher.BeginInvoke(()=>{_watchTimer.Stop();_watchTimer.Start();});};
        _watcher.Created+=change;_watcher.Changed+=change;_watcher.Renamed+=(_,e)=>change(_,e);
        _watcher.Error+=(_,_)=>Application.Current?.Dispatcher.BeginInvoke(()=>StatusMessage="Folder watcher interrupted. Refresh to rescan.");
        _watcher.EnableRaisingEvents=true;
    }
    private async void WatchTick(object? sender,EventArgs e){_watchTimer?.Stop();await RefreshAsync();}
    public void DisposeWorkspace(){_watcher?.Dispose();_watchTimer?.Stop();}
}
