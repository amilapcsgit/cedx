using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Cedx.App.ViewModels;
using Cedx.Core.Services;
using Cedx.Core.Storage;

namespace Cedx.App;

public partial class AstraWindow : Window
{
    public AstraViewModel ViewModel { get; }
    private bool? _narrow;
    public AstraWindow() : this(true) { }
    public AstraWindow(bool initialize, InventoryDatabase? database=null,string? folder=null,bool preferences=true)
    {
        InitializeComponent();ViewModel=new(database,folder,preferences);DataContext=ViewModel;
        AstraWorkspace.SizeChanged+=(_,_)=>Arrange();
        ViewModel.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(AstraViewModel.ShowDetails))Arrange(true);};
        ViewModel.Inspector.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(MainViewModel.Accent))ApplyAccent();};
        ApplyAccent();
        ViewModel.EditRequested+=Edit;
        if(initialize)Loaded+=async (_,_)=>{await ViewModel.InitializeAsync();AstraSearch.Focus();};
        Closing+=ClosingWindow;
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.F&&Keyboard.Modifiers==ModifierKeys.Control){if(_narrow==true)ViewModel.ShowDetails=false;AstraSearch.Focus();AstraSearch.SelectAll();e.Handled=true;}};
    }
    private void ApplyAccent()
    {
        var color=ViewModel.Inspector.Accent switch{"Violet"=>"#B69CFA","Phosphor green"=>"#86E897",_=>"#4DFFD2"};
        Resources["AccentBrush"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        Resources["PrimaryButtonBrush"]=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(color),Color.FromRgb(40,168,255),0);
    }
    private void Arrange(bool force=false)
    {
        if(ViewModel is null||AstraWorkspace.ActualWidth<=0)return;
        var narrow=AstraWorkspace.ActualWidth<1280;var changed=_narrow!=narrow;_narrow=narrow;
        if(changed&&narrow)ViewModel.ShowDetails=false;
        if(!changed&&!force)return;
        var show=ViewModel.ShowDetails;
        DetailsColumn.Width=new GridLength(!narrow&&show?440:0);DetailsGap.Width=new GridLength(!narrow&&show?16:12);
        Grid.SetColumn(AstraInspector,narrow?2:4);
        AstraInspector.Visibility=show?Visibility.Visible:Visibility.Collapsed;
        ResultWorkspace.Visibility=narrow&&show?Visibility.Collapsed:Visibility.Visible;
    }
    private void CloseDetails_Click(object sender,RoutedEventArgs e)=>ViewModel.ShowDetails=false;
    private void Search_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.None){AstraResults.Focus();if(AstraResults.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem first)first.Focus();e.Handled=true;}}
    private void Edit(ReviewScan source)
    {
        var editor=new Window{Owner=this,Title="Edit source TXT · original backup retained",Width=900,Height=700,MinWidth=600,MinHeight=450,WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)FindResource(typeof(Window))};
        var grid=new Grid{Margin=new Thickness(18)};grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new());grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var note=new TextBlock{Text="Full original report, including license fields. Save creates a .bak file. Approve separately to update inventory.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
        var text=new TextBox{Text=source.Asset.RawContent.Replace("\0",""),AcceptsReturn=true,AcceptsTab=true,FontFamily=new FontFamily("Consolas"),FontSize=13,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
        var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        var cancel=new Button{Content="Cancel",MinWidth=100,Margin=new Thickness(0,0,8,0),IsCancel=true};var save=new Button{Content="Save TXT + backup",MinWidth=180,Style=(Style)FindResource("Primary")};
        save.Click+=async (_,_)=>{save.IsEnabled=false;var saved=await ViewModel.SaveTextAsync(source,text.Text);save.IsEnabled=true;if(saved)editor.DialogResult=true;else note.Text=ViewModel.Status;};controls.Children.Add(cancel);controls.Children.Add(save);grid.Children.Add(note);Grid.SetRow(text,1);grid.Children.Add(text);Grid.SetRow(controls,2);grid.Children.Add(controls);editor.Content=grid;
        editor.ShowDialog();
    }
    private void ClosingWindow(object? sender,CancelEventArgs e)
    {
        if(ViewModel.Inspector.HasUnsavedChanges&&MessageBox.Show(this,"Discard unsaved assignment edits and close?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes){e.Cancel=true;return;}
        ViewModel.Dispose();
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);try{int enabled=1;DwmSetWindowAttribute(new WindowInteropHelper(this).Handle,20,ref enabled,sizeof(int));}catch(DllNotFoundException){}
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
}
