using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cedx.App.ViewModels;
using Cedx.Core.Storage;
using System.Diagnostics;
using System.Globalization;
using Cedx.Core.Models;
using Cedx.App.Common;

internal static class Program
{
 [STAThread] static int Main()
 {
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Cedx.App;component/Themes/GraphiteTheme.xaml",UriKind.Relative)});
  var temp=Path.Combine(Path.GetTempPath(),"cedx-wpf-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  var db=new InventoryDatabase(Path.Combine(temp,"inventory.db"));
  var window=new Cedx.App.MainWindow(false,db,false);var vm=(MainViewModel)window.DataContext;
  var source=Path.Combine(temp,"imports");Directory.CreateDirectory(source);
  // Exercise the real refresh path after the selectors are attached. This is the former DeferRefresh regression.
  var originals=Directory.GetFiles("assets","*.txt");
  foreach(var f in originals)File.Copy(f,Path.Combine(source,Path.GetFileName(f)));
  vm.AssetsFolderPath=source;window.Show();Wait(vm.RefreshAsync());Pump();
  Check(window.Background is not null && window.Background.ToString()!="#FFFFFFFF","Window receives the dark theme explicitly");
  Check(((FrameworkElement)window.Content).Margin==new Thickness(0),"Client content has no unused outer margin");
  window.WindowState=WindowState.Maximized;Pump();
  var client=(FrameworkElement)window.Content;
  Check(client.TranslatePoint(new Point(0,0),window).X<1,"Maximized content reaches the client edge");
  window.WindowState=WindowState.Normal;Pump();
  Check(vm.LoadedCount==originals.Length&&vm.FilteredCount==originals.Length,"Every repository example loads through bound UI");
  Check(!vm.StatusMessage.StartsWith("Load failed"),"No deferred refresh exception");
  var template=File.ReadAllText("samples/DEMO-SERVER.txt").Replace("System Manufacturer:","Monitor Model: Sample display\0\0\0\0\nSystem Manufacturer:");
  var nested=Path.Combine(source,"nested");Directory.CreateDirectory(nested);
  File.WriteAllText(Path.Combine(nested,"extra.TXT"),template.Replace("DEMO-SERVER","EXTRA-TEST").Replace("128 GB","1024 GB"));
  WaitUntil(()=>vm.LoadedCount==originals.Length+1,TimeSpan.FromSeconds(15));
  Check(vm.FilteredCount==originals.Length+1,"Watcher imports nested additions including larger RAM");
  vm.SearchText="EXTRA-TEST";Pump();Check(vm.FilteredCount==1&&vm.SelectedAsset?.Hostname=="EXTRA-TEST","Search and selection");
  Check(vm.OsSegments.Sum(s=>s.Count)==1 && Math.Abs(vm.OsSegments.Sum(s=>s.Percent)-100)<0.01,"OS chart tracks filtered inventory");
  var overview=vm.OverviewGroups.SelectMany(g=>g.Fields).ToArray();
  var sourceText=File.ReadAllText(Path.Combine(nested,"extra.TXT"));
  var direct=new Cedx.Core.Parsing.AssetTextParser().Parse(sourceText,"extra.TXT",DateTimeOffset.Now);
  var reloaded=db.Load().Single(a=>a.Hostname=="EXTRA-TEST");
  using(var connection=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+db.Path)){
   connection.Open();using var command=connection.CreateCommand();command.CommandText="SELECT CAST(raw AS BLOB) FROM assets WHERE id=$id";command.Parameters.AddWithValue("$id",reloaded.AssetId);
   var stored=System.Text.Encoding.UTF8.GetString((byte[])command.ExecuteScalar()!);
   Console.WriteLine($"Synthetic NUL diagnostics: source={sourceText.Length}, direct={direct.RawContent.Length}/{direct.Manufacturer}, stored={stored.Length}, reloaded={reloaded.RawContent.Length}/{reloaded.Manufacturer}, selected={vm.SelectedAsset!.RawContent.Length}/{vm.SelectedAsset.Manufacturer}, core={typeof(InventoryDatabase).Assembly.Location}");
   Check(stored==sourceText,"Complete synthetic report stored");
  }
  Check(reloaded.RawContent==sourceText&&vm.SelectedAsset!.RawContent==sourceText,"Complete synthetic report reaches selection");
  Check(overview.Any(f=>f.Label=="Manufacturer"&&f.Value=="Demo manufacturer")&&overview.Any(f=>f.Label=="Disks"&&f.Value.Contains("GB free")),"Overview shows hardware and disks after embedded NUL");
  Check(overview.Any(f=>f.Label=="Gateway"&&f.Value=="192.0.2.254")&&overview.Any(f=>f.Label=="Antivirus"&&f.Value=="Microsoft Defender"),"Overview exposes network and protection without changing tabs");
  Check(!vm.OverviewGroups.Any(g=>g.Title=="Assignment")&&overview.All(f=>!string.IsNullOrWhiteSpace(f.Value)),"Unassigned assets have no empty assignment rows");
  vm.EditCompany="Synthetic company";vm.EditPerson="Sample operator";vm.EditDepartment="Engineering";vm.EditLocation="Room 2";vm.EditTag="LAB-001";vm.SaveSelectedAsset();
  Check(vm.SelectedAsset?.IsManaged==true&&vm.SelectedAsset.Company=="Synthetic company","Promote scan to managed asset");
  Check(vm.OverviewGroups.Single(g=>g.Title=="Assignment").Fields.Any(f=>f.Value=="Synthetic company"),"Saved assignment appears in compact overview");
  Check(vm.CreateInventoryCsv().Contains("Synthetic company,Sample operator,Engineering,Room 2,LAB-001"),"Filtered CSV includes saved assignment");
  File.AppendAllText(Path.Combine(nested,"extra.TXT"),"\n=== Update ===\nTest : New scan\n");
  Wait(vm.RefreshAsync());Check(vm.SelectedAsset?.Person=="Sample operator","Refresh preserves assignment and selection");
  vm.SearchText="does-not-exist";Check(vm.SelectedAsset==null&&vm.DetailRows.Count==0,"Empty result clears inspector");
  vm.ResetFiltersCommand.Execute(null);Check(vm.FilteredCount==originals.Length+1,"Reset reveals all assets");
  vm.WatchFolder=false;window.Close();
  var reopened=new InventoryDatabase(db.Path).Load();Check(reopened.Any(a=>a.Person=="Sample operator"),"Assignment persists after window closes");

  // Use only synthetic data in published screenshots. Render an unshown WPF root
  // to avoid the hosted runner's 1024px desktop clipping wide screenshots.
  var demo=new InventoryDatabase(Path.Combine(temp,"demo.db"));demo.Import(Directory.GetFiles("samples","*.txt"),true);
  foreach(var a in demo.Load())demo.SaveAssignment(a.AssetId,new("Demo organization","Sample operator","Engineering","Room 2","DEMO","Synthetic sample for interface preview."));
  var preview=new Cedx.App.MainWindow(false,demo,false);var p=(MainViewModel)preview.DataContext;p.AssetsFolderPath=Path.Combine(temp,"empty");p.WatchFolder=false;Wait(p.RefreshAsync());
  p.AssetsFolderPath="samples";
  var root=(FrameworkElement)preview.Content;root.DataContext=p;
  Directory.CreateDirectory("artifacts/ui-smoke");
  Layout(root,1920,1080);p.ShowInspector=true;Layout(root,1920,1080);
  Check(p.FilteredCount==6,"Six sample cards");
  Check(p.OsSegments.Count==3 && p.OsSegments.Sum(s=>s.Count)==6,"OS distribution covers every sample");
  Check(((System.Windows.Controls.Primitives.UniformGrid)preview.FindName("FleetMetrics")).Columns==4,"Full HD metric panels use four columns");
  var meter=new DiskMeterConverter();
  Check((double)meter.Convert(new StorageDevice{TotalGb=100,FreeGb=25},typeof(double),"",CultureInfo.InvariantCulture)==25,"Disk meter uses actual free and total capacity");
  Check((Visibility)meter.Convert(new StorageDevice{TotalGb=0,FreeGb=25},typeof(Visibility),"Visibility",CultureInfo.InvariantCulture)==Visibility.Collapsed,"Unknown capacity does not produce a chart");
  p.SearchText="DEMO-SHOP";Layout(root,1920,1080);
  Check(p.LowStorageCount==1 && p.OsSegments.Single().Label=="Windows 10","Metrics follow search and real low disk data");
  p.ResetFiltersCommand.Execute(null);Layout(root,1920,1080);
  Check(Grid.GetColumn((Border)preview.FindName("InspectorPanel"))==4,"Wide inspector docking");
  var card=((ListBox)preview.FindName("TilesList")).ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
  Check(card is not null&&card.ActualHeight<285,"Compact tile retains useful actions below 285 DIP");
  Console.WriteLine($"Tile height: {card!.ActualHeight:0.#} DIP");
  Capture(root,"01-inventory");
  p.ManageAssetCommand.Execute(null);Layout(root,1920,1080);Capture(root,"02-manage-asset");
  p.InspectorTab=2;p.DetailCategory="Security";p.SelectedDetailSection=p.DetailSections.First(s=>s.Title=="Local account details");Layout(root,1920,1080);Capture(root,"03-scan-details");
  Check(((DataGrid)preview.FindName("DetailGrid")).ActualHeight>150,"Scan detail table visible");
  Layout(root,1040,800);Check(((Grid)preview.FindName("FleetPanel")).Visibility==Visibility.Visible,"Compact cards remain visible");
  p.OpenAssetCommand.Execute(p.SelectedAsset);Layout(root,1040,800);Check(((Border)preview.FindName("InspectorPanel")).ActualWidth>500,"Compact inspector has usable width");Capture(root,"04-compact");
  p.Accent="Violet";p.TileMinimumWidth=320;p.ShowInspector=false;Layout(root,1920,1080);
  p.Accent="Ion cyan";p.TileMinimumWidth=280;Layout(root,1536,864);p.ShowInspector=true;Layout(root,1536,864);
  Check(((Grid)preview.FindName("FleetPanel")).ActualWidth>700,"Full HD at 125 percent retains usable cards");
  Capture(root,"05-scale-125",120);
  Console.WriteLine("PASS: bound refresh, all example TXT files, watched folder additions, large RAM, managed assignment, persistence, empty selection and responsive WPF screens");
  p.DisposeWorkspace();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(temp,true);return 0;
 }
 static void Layout(FrameworkElement root,double w,double h){root.Width=w;root.Height=h;root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();Pump();root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();}
 static void Capture(FrameworkElement root,string name,double dpi=96){var bitmap=new RenderTargetBitmap((int)Math.Round(root.ActualWidth*dpi/96),(int)Math.Round(root.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create($"artifacts/ui-smoke/{name}.png");encoder.Save(file);}
 static void Pump(){Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
 static void Wait(Task task){WaitUntil(()=>task.IsCompleted,TimeSpan.FromSeconds(30));task.GetAwaiter().GetResult();}
 static void WaitUntil(Func<bool> done,TimeSpan timeout){var watch=Stopwatch.StartNew();while(!done()){if(watch.Elapsed>timeout)throw new TimeoutException("UI operation timed out");Pump();Thread.Sleep(10);}Pump();}
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS: "+message);}
}
