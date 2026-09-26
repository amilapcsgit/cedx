using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Cedx.App.Common;
using Cedx.Core.Models;
using Cedx.Core.Parsing;
using Microsoft.Win32;

namespace Cedx.App.ViewModels;

public sealed partial class MainViewModel
{
    private bool _rangesInitialized;
    private bool _includeUnknownMeasurements=true;
    private string _filterValidation="";
    private string _detailCategory="All";
    private string _detailSearch="";
    private AssetDetailSection? _selectedDetailSection;
    private bool _revealKeys;
    private bool _showFilters=true;
    private double _tileMinimumWidth=280;
    private string _accent="Ion cyan";
    private string _sortMode="Hostname";
    private static readonly string PreferencesPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"cedx","workspace.json");
    public string[] DetailCategories {get;}=["All","Overview","Hardware","Network","Security","Software","Licenses","Diagnostics"];
    public string[] AccentOptions {get;}=["Ion cyan","Phosphor green","Violet"];
    public string[] SortOptions {get;}=["Hostname","Windows account","C: free ascending","RAM descending"];
    public ObservableCollection<AssetDetailSection> DetailSections {get;}=[];
    public ObservableCollection<AssetDetailRow> DetailRows {get;}=[];
    public RelayCommand ChooseFolderCommand {get;private set;}=null!;
    public RelayCommand OpenSourceCommand {get;private set;}=null!;
    public RelayCommand CopyDetailRowsCommand {get;private set;}=null!;
    public RelayCommand ExportDetailRowsCommand {get;private set;}=null!;
    public RelayCommand ResetLayoutCommand {get;private set;}=null!;
    public string FilterValidation {get=>_filterValidation;private set=>SetProperty(ref _filterValidation,value);}
    public bool IncludeUnknownMeasurements {get=>_includeUnknownMeasurements;set{if(SetProperty(ref _includeUnknownMeasurements,value))ApplyFilters();}}
    public bool HasSelection=>SelectedAsset is not null;
    public bool HasNoResults=>FilteredCount==0;
    public string DetailRowCount=>$"{DetailRows.Count} / {SelectedDetailSection?.Rows.Count ?? 0} fields";
    public string DetailSummary=>SelectedAsset is null?"Select an asset":$"{SelectedAsset.DetailSections.Count} sections · {SelectedAsset.Software.InstalledPrograms.Count} programs · {SelectedAsset.LocalDisks.Count} volumes";
    public string DetailCategory {get=>_detailCategory;set{if(SetProperty(ref _detailCategory,value))RefreshInspector();}}
    public string DetailSearch {get=>_detailSearch;set{if(SetProperty(ref _detailSearch,value))RefreshDetailRows();}}
    public AssetDetailSection? SelectedDetailSection {get=>_selectedDetailSection;set{if(SetProperty(ref _selectedDetailSection,value))RefreshDetailRows();}}
    public bool RevealKeys {get=>_revealKeys;set{if(SetProperty(ref _revealKeys,value))RefreshDetailRows();}}
    public bool ShowFilters {get=>_showFilters;set=>SetProperty(ref _showFilters,value);}
    public double TileMinimumWidth {get=>_tileMinimumWidth;set=>SetProperty(ref _tileMinimumWidth,Math.Clamp(value,220,440));}
    public string Accent {get=>_accent;set=>SetProperty(ref _accent,value);}
    public string SortMode {get=>_sortMode;set{if(SetProperty(ref _sortMode,value))ApplySort();}}
    public string RawDetail=>Display(SelectedDetailSection?.RawContent ?? "");
    public string RawAsset=>Display(SelectedAsset?.RawContent ?? "");
    public double SavedDetailsWidth {get;set;}=520;
    public double SavedDetailsHeight {get;set;}=420;
    public double SavedWindowWidth {get;set;}=1540;
    public double SavedWindowHeight {get;set;}=920;

    private void InitializeInspector()
    {
        ChooseFolderCommand=new RelayCommand(_=>{
            var dialog=new OpenFolderDialog {Title="Select the folder containing asset TXT reports",InitialDirectory=Directory.Exists(AssetsFolderPath)?AssetsFolderPath:Environment.CurrentDirectory};
            if(dialog.ShowDialog()==true){AssetsFolderPath=dialog.FolderName;RefreshCommand.Execute(null);}
        });
        OpenSourceCommand=new RelayCommand(_=>{
            if(SelectedAsset is null)return;
            try{var p=new ProcessStartInfo("notepad.exe"){UseShellExecute=false};p.ArgumentList.Add(SelectedAsset.SourceFilePath);Process.Start(p);}
            catch(Exception ex){StatusMessage="Open report failed: "+ex.Message;}
        },_=>SelectedAsset is not null);
        CopyDetailRowsCommand=new RelayCommand(_=>CopyText(string.Join(Environment.NewLine,DetailRows.Select(r=>$"{r.Record}\t{r.Field}\t{r.Value}"))),_=>DetailRows.Count>0);
        ExportDetailRowsCommand=new RelayCommand(_=>ExportDetailRows(),_=>DetailRows.Count>0);
        ResetLayoutCommand=new RelayCommand(_=>{TileMinimumWidth=280;ShowFilters=true;Accent="Ion cyan";SavedDetailsWidth=520;SavedDetailsHeight=420;OnPropertyChanged(nameof(SavedDetailsWidth));});
        try{
            if(File.Exists(PreferencesPath)){
                var p=JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(PreferencesPath));
                if(p is not null){
                    if(Directory.Exists(p.AssetsFolderPath))AssetsFolderPath=p.AssetsFolderPath;
                    NmapPath=p.NmapPath;TileMinimumWidth=p.TileMinimumWidth;ShowFilters=p.ShowFilters;
                    Accent=AccentOptions.Contains(p.Accent)?p.Accent:AccentOptions[0];
                    SavedDetailsWidth=Math.Clamp(p.DetailsWidth,340,1000);SavedDetailsHeight=Math.Clamp(p.DetailsHeight,220,700);
                    SavedWindowWidth=Math.Clamp(p.WindowWidth,900,3000);SavedWindowHeight=Math.Clamp(p.WindowHeight,620,1800);
                }
            }
        }catch(Exception ex){StatusMessage="Preferences reset: "+ex.Message;}
        PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(FilteredCount))OnPropertyChanged(nameof(HasNoResults));};
    }
    private void OnSelectedAssetChanged()
    {
        _revealKeys=false;_detailSearch="";
        OnPropertyChanged(nameof(RevealKeys));OnPropertyChanged(nameof(DetailSearch));OnPropertyChanged(nameof(HasSelection));
        RefreshInspector();OpenSourceCommand?.RaiseCanExecuteChanged();
    }
    private void RefreshInspector()
    {
        var previous=SelectedDetailSection?.Title;
        DetailSections.Clear();
        if(SelectedAsset is not null){
            foreach(var s in SelectedAsset.DetailSections.Where(s=>DetailCategory=="All"||s.Category==DetailCategory))DetailSections.Add(s);
            if(DetailCategory is "All" or "Network")DetailSections.Add(new("Live scan results","Network",
                [new("Nmap","Last scanned",SelectedAsset.Network.NmapLastScanned),new("Nmap","Output",SelectedAsset.Network.NmapScanOutput)],SelectedAsset.Network.NmapScanOutput));
        }
        SelectedDetailSection=DetailSections.FirstOrDefault(s=>s.Title==previous)??DetailSections.FirstOrDefault();
        RefreshDetailRows();OnPropertyChanged(nameof(DetailSummary));OnPropertyChanged(nameof(RawAsset));
    }
    private string Display(string value)=>RevealKeys?value:AssetDetails.RedactKeys(value);
    private void RefreshDetailRows()
    {
        DetailRows.Clear();
        foreach(var r in SelectedDetailSection?.Rows ?? []){
            var shown=r with {Value=Display(r.Value)};
            if(string.IsNullOrWhiteSpace(DetailSearch)||$"{shown.Record}\n{shown.Field}\n{shown.Value}".Contains(DetailSearch.Trim(),StringComparison.OrdinalIgnoreCase))DetailRows.Add(shown);
        }
        OnPropertyChanged(nameof(RawDetail));OnPropertyChanged(nameof(RawAsset));OnPropertyChanged(nameof(DetailRowCount));
        CopyDetailRowsCommand?.RaiseCanExecuteChanged();ExportDetailRowsCommand?.RaiseCanExecuteChanged();
    }
    private void ApplySort()
    {
        var (property,descending)=SortMode switch {
            "Windows account"=>(nameof(AssetRecord.WindowsAccount),false),"C: free ascending"=>(nameof(AssetRecord.CDriveFreeGb),false),
            "RAM descending"=>(nameof(AssetRecord.RamGb),true),_=>(nameof(AssetRecord.Hostname),false)};
        using(AssetsView.DeferRefresh()){
            AssetsView.SortDescriptions.Clear();AssetsView.SortDescriptions.Add(new(property,descending?System.ComponentModel.ListSortDirection.Descending:System.ComponentModel.ListSortDirection.Ascending));
            if(property!=nameof(AssetRecord.Hostname))AssetsView.SortDescriptions.Add(new(nameof(AssetRecord.Hostname),System.ComponentModel.ListSortDirection.Ascending));
        }
        EnsureVisibleSelection();UpdateCounts();
    }
    private void ExportDetailRows()
    {
        var dialog=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="cedx_details.csv"};if(dialog.ShowDialog()!=true)return;
        try{
            var b=new StringBuilder();AppendCsvRow(b,"Hostname","Section","Record","Field","Value");
            foreach(var r in DetailRows)AppendCsvRow(b,SelectedAsset?.Hostname??"",SelectedDetailSection?.Title??"",r.Record,r.Field,r.Value);
            File.WriteAllText(dialog.FileName,b.ToString(),new UTF8Encoding(true));StatusMessage=$"Exported {DetailRows.Count} visible fields";
        }catch(Exception ex){StatusMessage="Export failed: "+ex.Message;}
    }
    public void SavePreferences()
    {
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            var p=new WorkspacePreferences(AssetsFolderPath,NmapPath,TileMinimumWidth,ShowFilters,Accent,SavedDetailsWidth,SavedDetailsHeight,SavedWindowWidth,SavedWindowHeight);
            var temp=PreferencesPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(p,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,PreferencesPath,true);
        }catch(Exception ex){StatusMessage="Preferences not saved: "+ex.Message;}
    }
    private sealed record WorkspacePreferences(string AssetsFolderPath,string NmapPath,double TileMinimumWidth,bool ShowFilters,string Accent,double DetailsWidth,double DetailsHeight,double WindowWidth,double WindowHeight);
}
