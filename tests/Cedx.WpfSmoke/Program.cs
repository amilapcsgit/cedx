using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cedx.App.ViewModels;
using Cedx.Core.Parsing;
using System.IO;
internal static class Program
{
 [STAThread] static int Main(){
  var app=new Application();app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Cedx.App;component/Themes/GraphiteTheme.xaml",UriKind.Relative)});
  var window=new Cedx.App.MainWindow(false);var vm=(MainViewModel)window.DataContext;
  var parser=new AssetTextParser();
  for(var i=0;i<18;i++)vm.Assets.Add(parser.Parse($"Hostname: LAB-{i:00}\nWindows account: LAB\\operator{i}\nIP Address: 192.0.2.{i+1}\n=== Local account details ===\nName : operator{i}\nEnabled : True",$"lab{i}.txt",DateTimeOffset.UtcNow));
  vm.SearchText="LAB";window.Show();Pump(window);
  Check(vm.FilteredCount==18,"Initial count");
  vm.SearchText="LAB-03";Pump(window);Check(vm.FilteredCount==1&&vm.SelectedAsset?.Hostname=="LAB-03","Filtered selection");
  vm.SearchText="nonexistent";Pump(window);Check(vm.FilteredCount==0&&vm.SelectedAsset==null&&vm.DetailRows.Count==0,"Empty selection");
  vm.SearchText="";vm.DetailCategory="Security";Pump(window);Check(vm.DetailSections.Any(s=>s.Title=="Local account details"),"Details navigation");
  vm.SelectedDetailSection=vm.DetailSections.Single(s=>s.Title=="Local account details");
  Directory.CreateDirectory("artifacts/ui-smoke");
  foreach(var width in new[]{1000d,1600d}){
   window.Width=width;window.Height=900;Pump(window);
   var inspector=(Border)window.FindName("InspectorPanel");
   Check(inspector.ActualWidth>250&&inspector.ActualHeight>180,"Inspector usable size");
   Check(((DataGrid)window.FindName("DetailGrid")).ActualHeight>=70,"Visible detail table");
   var workspace=(Grid)window.FindName("Workspace");
   Console.WriteLine($"Layout: requested={width}, workspace={workspace.ActualWidth}, inspector row={Grid.GetRow(inspector)}");
   Check(Grid.GetRow(inspector)==(workspace.ActualWidth-228<980?2:0),"Responsive inspector placement");
   var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create($"artifacts/ui-smoke/{width}.png");encoder.Save(output);
  }
  vm.ShowFilters=false;vm.Accent="Violet";vm.TileMinimumWidth=360;Pump(window);
  Check(((ColumnDefinition)window.FindName("FilterColumn")).ActualWidth==0,"Hide filters");
  window.Close();Console.WriteLine("PASS: WPF selection, details, responsive layout, customization");return 0;
 }
 static void Pump(Window w){w.UpdateLayout();w.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);w.UpdateLayout();}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
