using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using Cedx.App.ViewModels;
using Cedx.Core.Parsing;
using Cedx.Core.Services;
using Cedx.Core.Storage;

namespace Cedx.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool? _narrow;
    public MainWindow() : this(true) { }
    public MainWindow(bool loadAssets, InventoryDatabase? database = null, bool loadPreferences = true)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new FileAssetRepository(new AssetTextParser()), database, loadPreferences);
        DataContext = _viewModel;
        Width = Math.Max(MinWidth, Math.Min(_viewModel.SavedWindowWidth, SystemParameters.WorkArea.Width));
        Height = Math.Max(MinHeight, Math.Min(_viewModel.SavedWindowHeight, SystemParameters.WorkArea.Height));
        Workspace.SizeChanged += (_, _) => ArrangeWorkspace();
        FleetPanel.SizeChanged += (_, _) => FleetMetrics.Columns = FleetPanel.ActualWidth < 760 ? 2 : 4;
        _viewModel.PropertyChanged += (_, e) => {
            if (e.PropertyName is nameof(MainViewModel.ShowInspector) or nameof(MainViewModel.SavedDetailsWidth)) ArrangeWorkspace(true);
            if (e.PropertyName == nameof(MainViewModel.Accent)) ApplyAccent();
        };
        if (loadAssets) Loaded += async (_, _) => await _viewModel.RefreshAsync();
        Closing += Window_Closing;
        ApplyAccent();
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.HasUnsavedChanges && MessageBox.Show(this,"There are unsaved asset assignments. Close and discard these edits?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes){e.Cancel=true;return;}
        _viewModel.SavedWindowWidth=RestoreBounds.Width;_viewModel.SavedWindowHeight=RestoreBounds.Height;
        _viewModel.SavePreferences();_viewModel.DisposeWorkspace();
    }
    private void ApplyAccent()
    {
        var hex=_viewModel.Accent switch{"Phosphor green"=>"#86E897","Violet"=>"#B69CFA",_=>"#4DFFD2"};
        Application.Current.Resources["AccentBrush"]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        var start = _viewModel.Accent switch { "Phosphor green" => "#86E897", "Violet" => "#B69CFA", _ => "#00DDA8" };
        Application.Current.Resources["PrimaryButtonBrush"] = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(start), (Color)ColorConverter.ConvertFromString("#28A8FF"), new Point(0,0), new Point(1,1));
    }
    private void ArrangeWorkspace(bool force=false)
    {
        if (_viewModel is null || Workspace.ActualWidth<=0)return;
        var narrow=Workspace.ActualWidth<1240;
        var changed=_narrow!=narrow;_narrow=narrow;
        if(changed && narrow) _viewModel.ShowInspector=false;
        var show=_viewModel.ShowInspector;
        if(!changed&&!force)return;
        InspectorColumn.Width=new GridLength(!narrow&&show?Math.Clamp(_viewModel.SavedDetailsWidth,390,Math.Max(390,Workspace.ActualWidth*.38)):0);
        InspectorGapColumn.Width=new GridLength(!narrow&&show?14:0);
        InspectorDivider.Visibility=!narrow&&show?Visibility.Visible:Visibility.Collapsed;
        InspectorPanel.Visibility=show?Visibility.Visible:Visibility.Collapsed;
        Grid.SetColumn(InspectorPanel,narrow?2:4);
        FleetPanel.Visibility=narrow&&show?Visibility.Collapsed:Visibility.Visible;
    }
    private void TilesList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin) return;
        for (var node = origin; node is not null && node != TilesList; node = VisualTreeHelper.GetParent(node))
            if (node is Button) return;
        if (ItemsControl.ContainerFromElement(TilesList, origin) is ListBoxItem { Content: Cedx.Core.Models.AssetRecord asset })
            _viewModel.OpenAssetCommand.Execute(asset);
    }
    private void TilesList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && e.OriginalSource is ListBoxItem && _viewModel.SelectedAsset is not null)
        { _viewModel.OpenAssetCommand.Execute(_viewModel.SelectedAsset); e.Handled = true; }
    }
    private void InspectorDivider_DragCompleted(object sender,DragCompletedEventArgs e)
    {_viewModel.SavedDetailsWidth=Math.Clamp(InspectorColumn.ActualWidth,390,800);}
    private void FindCommand_Executed(object sender,System.Windows.Input.ExecutedRoutedEventArgs e)
    {if(_narrow==true)_viewModel.ShowInspector=false;SearchBox.Focus();SearchBox.SelectAll();}
    private async void Window_Drop(object sender,DragEventArgs e)
    {if(e.Data.GetData(DataFormats.FileDrop) is string[] files)await _viewModel.ImportFilesAsync(files.Where(p=>Directory.Exists(p)||Path.GetExtension(p).Equals(".txt",StringComparison.OrdinalIgnoreCase)));}
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try{var enabled=1;DwmSetWindowAttribute(new WindowInteropHelper(this).Handle,20,ref enabled,sizeof(int));}catch(DllNotFoundException){}
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
}
