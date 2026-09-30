using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cedx.App;
using Cedx.Core.Storage;

internal static class AstraSmoke
{
    public static void Run(string temp)
    {
        var folder=Path.Combine(temp,"astra-source");Directory.CreateDirectory(folder);
        var cad=File.ReadAllText("samples/DEMO-CAD.txt")+"\nUser Email(s): operator@example.invalid\nEscalation contact: example specialist\n";
        File.WriteAllText(Path.Combine(folder,"cad.txt"),cad);
        File.WriteAllText(Path.Combine(folder,"copy.txt"),cad);
        File.WriteAllText(Path.Combine(folder,"server.txt"),File.ReadAllText("samples/DEMO-SERVER.txt"));
        File.WriteAllText(Path.Combine(folder,"notes.txt"),"This is documentation, not a scanned asset.");
        var db=new InventoryDatabase(Path.Combine(temp,"astra.db"));
        var window=new AstraWindow(false,db,folder,false);var vm=window.ViewModel;
        window.Show();Wait(vm.InitializeAsync());
        Check(vm.Scope=="Review TXT"&&vm.ReviewCount==3&&db.Load().Count==0,"Astra previews TXT without automatic database import");
        Check(vm.FileErrors.Contains("notes.txt"),"Invalid TXT is reported without hiding valid scans");
        Check(vm.DuplicateCount==2,"Identical TXT copies flagged for review");
        vm.Search="operator@example.invalid";Check(vm.Results.Count==2,"Find computer by email");
        vm.Search="example specialist";Check(vm.Results.Count==2,"Find any name in unstructured scan text");
        vm.Search="DEMO\\user6";Check(vm.Results.Count==2,"Find computer by Windows account");
        vm.Search="DEMO-CAD";Check(vm.Results.Count==2,"Find computer by hostname");
        Check(vm.Inspector.LaunchAnyDeskCommand.CanExecute("123456789")&&!vm.Inspector.LaunchAnyDeskCommand.CanExecute("Not installed"),"Astra reuses validated AnyDesk launch command without contacting endpoints");
        Wait(vm.ApproveAsync());Check(db.Load().Count==1&&vm.CurrentCount==1&&vm.ReviewCount==3,"Only selected reviewed report approved");
        vm.Inspector.EditCompany="Demo organization";vm.Inspector.EditPerson="Sample operator";vm.Inspector.EditDepartment="Engineering";Wait(vm.SaveAssignmentAsync());
        var id=vm.Selected!.Asset.AssetId;
        vm.Search="Sample operator";Check(vm.Results.Count==1&&vm.CreateCsv().Contains("Demo organization"),"Assignment is searchable and exportable");
        vm.Scope="Review TXT";vm.Search="";vm.Selected=vm.Results.Single(x=>x.Asset.SourceFileName=="cad.txt");vm.Filename="reviewed.txt";Wait(vm.RenameAsync());
        Check(File.Exists(Path.Combine(folder,"reviewed.txt"))&&!File.Exists(Path.Combine(folder,"cad.txt")),"Astra renames source TXT");
        var source=vm.Selected!.Source!;Wait(vm.SaveTextAsync(source,source.Asset.RawContent.Replace("RAM: 64 GB","RAM: 96 GB")));
        Check(db.Load().Single().RamGb==64,"TXT edits do not change approved inventory before review");
        Wait(vm.ApproveAsync());var updated=db.Load().Single();
        Check(updated.RamGb==96&&updated.AssetId==id&&updated.Person=="Sample operator","Approved update preserves identity and assignment");
        Wait(vm.SetArchivedAsync(true));Check(vm.CurrentCount==0&&vm.ArchiveCount==1&&db.History(id).Count==2,"Archive retains scan history");
        vm.Scope="Archived";Wait(vm.SetArchivedAsync(false));Check(vm.CurrentCount==1&&vm.ArchiveCount==0,"Archived asset restores to current inventory");
        vm.Scope="Review TXT";vm.Selected=vm.Results.Single(x=>x.Asset.SourceFileName=="copy.txt");Wait(vm.RemoveAsync());
        Check(vm.ReviewCount==2&&db.Load().Count==1,"Removing duplicate TXT leaves approved asset intact");
        Wait(vm.UndoRemoveAsync());Check(vm.ReviewCount==3,"Undo restores removed TXT");
        vm.Scope="Review TXT";vm.Search="nothing-will-match";Check(vm.Selected is null&&vm.Inspector.OverviewGroups.Count==0,"No matches clears selected computer and inspector");
        vm.Search="";File.WriteAllText(Path.Combine(folder,"watched.txt"),File.ReadAllText("samples/DEMO-MOBILE.txt"));
        Until(()=>vm.ReviewCount==4&&!vm.IsBusy);Check(db.Load().Count==1,"Watcher updates preview without approving new sources");
        window.Close();

        // Publish only synthetic data. Build a populated current fleet through explicit approvals.
        var screenFolder=Path.Combine(temp,"astra-preview");Directory.CreateDirectory(screenFolder);
        foreach(var file in Directory.GetFiles("samples","*.txt"))File.Copy(file,Path.Combine(screenFolder,Path.GetFileName(file)));
        var preview=new AstraWindow(false,new InventoryDatabase(Path.Combine(temp,"astra-preview.db")),screenFolder,false);var p=preview.ViewModel;
        Wait(p.InitializeAsync());
        foreach(var filename in Directory.GetFiles(screenFolder,"*.txt").Select(Path.GetFileName).ToArray()){
            p.Scope="Review TXT";p.Selected=p.Results.Single(x=>x.Asset.SourceFileName==filename);Wait(p.ApproveAsync());
        }
        p.Selected=p.Results.Single(x=>x.Asset.Hostname=="DEMO-CAD");p.Inspector.EditPerson="Sample operator";p.Inspector.EditCompany="Demo organization";p.Inspector.EditDepartment="Engineering";Wait(p.SaveAssignmentAsync());
        var root=(FrameworkElement)preview.Content;root.DataContext=p;
        Directory.CreateDirectory("artifacts/ui-smoke");Layout(root,1920,1080);p.ShowDetails=true;Layout(root,1920,1080);
        Check(p.Results.Count==6&&Grid.GetColumn((Border)preview.FindName("AstraInspector"))==4,"Astra Full HD shows six current assets and docked inspector");
        Check(p.OsSegments.Sum(s=>s.Count)==6&&p.OsSegments.Count==3,"Astra OS chart uses visible records");
        Check(p.ReadyCount==0&&p.RemoteArc.IsEmpty(),"Missing remote IDs do not create a false gauge");
        var tile=((ListBox)preview.FindName("AstraResults")).ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
        Check(tile is not null&&tile.ActualHeight<310,"Astra cards keep readable controls in compact height");
        Console.WriteLine($"Astra tile height: {tile!.ActualHeight:0.#} DIP");
        Capture(root,"06-astra-mission-control");
        p.Scope="Review TXT";p.Selected=p.Results.Single(x=>x.Asset.Hostname=="DEMO-CAD");((TabControl)preview.FindName("AstraTabs")).SelectedIndex=2;Layout(root,1920,1080);Capture(root,"07-astra-review");
        p.Scope="Current assets";p.Search="Sample operator";((TabControl)preview.FindName("AstraTabs")).SelectedIndex=1;Layout(root,1920,1080);Capture(root,"08-astra-assignment");
        Layout(root,1040,800);Check(((Grid)preview.FindName("ResultWorkspace")).Visibility==Visibility.Visible,"Compact Astra keeps lookup visible");
        p.ShowDetails=true;Layout(root,1040,800);Check(((Border)preview.FindName("AstraInspector")).ActualWidth>600,"Compact Astra gives inspector usable width");Capture(root,"09-astra-compact");
        p.Search="";((TabControl)preview.FindName("AstraTabs")).SelectedIndex=0;Layout(root,1536,864);p.ShowDetails=true;Layout(root,1536,864);Capture(root,"10-astra-125",120);
        p.Dispose();Console.WriteLine("PASS: Astra review, search, approval, rename, edit, archive, restore, watcher, export and responsive layout");
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
    static void Until(Func<bool> done){var w=Stopwatch.StartNew();while(!done()){if(w.Elapsed>TimeSpan.FromSeconds(40))throw new TimeoutException("Astra operation timed out");Pump();Thread.Sleep(10);}Pump();}
    static void Wait(Task task){Until(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
    static void Layout(FrameworkElement root,double w,double h){root.Width=w;root.Height=h;root.Measure(new(w,h));root.Arrange(new(0,0,w,h));root.UpdateLayout();Pump();root.Measure(new(w,h));root.Arrange(new(0,0,w,h));root.UpdateLayout();}
    static void Capture(FrameworkElement root,string name,double dpi=96){var bitmap=new RenderTargetBitmap((int)Math.Round(root.ActualWidth*dpi/96),(int)Math.Round(root.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var f=File.Create($"artifacts/ui-smoke/{name}.png");encoder.Save(f);}
}
