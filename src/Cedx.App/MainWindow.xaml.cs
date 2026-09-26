using System.Windows;
using System.Windows.Interop;
using Cedx.App.ViewModels;
using Cedx.Core.Parsing;
using Cedx.Core.Services;

namespace Cedx.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmSystemBackdropAcrylic = 3;

    public MainWindow() : this(true) { }

    public MainWindow(bool loadAssets)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new FileAssetRepository(new AssetTextParser()));
        DataContext = _viewModel;
        Width = Math.Min(_viewModel.SavedWindowWidth, SystemParameters.WorkArea.Width);
        Height = Math.Min(_viewModel.SavedWindowHeight, SystemParameters.WorkArea.Height);
        if (loadAssets) Loaded += MainWindow_Loaded;
        SizeChanged += (_, _) => ArrangeInspector();
        _viewModel.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(MainViewModel.ShowFilters) || e.PropertyName == nameof(MainViewModel.SavedDetailsWidth)) ArrangeInspector(true);
            if (e.PropertyName == nameof(MainViewModel.Accent)) ApplyAccent();
        };
        Closing += (_, _) => {
            _viewModel.SavedWindowWidth = RestoreBounds.Width;
            _viewModel.SavedWindowHeight = RestoreBounds.Height;
            _viewModel.SavePreferences();
        };
        ApplyAccent();
    }

    private bool? _sideInspector;
    private void ApplyAccent()
    {
        var hex = _viewModel.Accent switch { "Phosphor green" => "#6CFFA6", "Violet" => "#B79CFF", _ => "#42E8F5" };
        Application.Current.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }
    private void ArrangeInspector(bool force = false)
    {
        if (_viewModel is null || Workspace.ActualWidth <= 0) return;
        FilterColumn.Width = new GridLength(_viewModel.ShowFilters ? 220 : 0);
        Workspace.ColumnDefinitions[1].Width = new GridLength(_viewModel.ShowFilters ? 8 : 0);
        var side = Workspace.ActualWidth - (_viewModel.ShowFilters ? 228 : 0) >= 980;
        if (!force && _sideInspector == side) return;
        _sideInspector = side;
        InspectorColumn.Width = new GridLength(side ? Math.Min(_viewModel.SavedDetailsWidth, (Workspace.ActualWidth - FilterColumn.ActualWidth) * .52) : 0);
        InspectorGapColumn.Width = new GridLength(side ? 8 : 0);
        InspectorRow.Height = new GridLength(side ? 0 : Math.Min(_viewModel.SavedDetailsHeight, Math.Max(200, Workspace.ActualHeight * .52)));
        InspectorGapRow.Height = new GridLength(side ? 0 : 8);
        System.Windows.Controls.Grid.SetColumn(InspectorPanel, side ? 4 : 2);
        System.Windows.Controls.Grid.SetRow(InspectorPanel, side ? 0 : 2);
        System.Windows.Controls.Grid.SetColumn(InspectorDivider, side ? 3 : 2);
        System.Windows.Controls.Grid.SetRow(InspectorDivider, side ? 0 : 1);
        InspectorDivider.Width = side ? 8 : double.NaN;
        InspectorDivider.Height = side ? double.NaN : 8;
        InspectorDivider.HorizontalAlignment = HorizontalAlignment.Stretch;
        InspectorDivider.VerticalAlignment = VerticalAlignment.Stretch;
        InspectorDivider.ResizeDirection = side ? System.Windows.Controls.GridResizeDirection.Columns : System.Windows.Controls.GridResizeDirection.Rows;
    }
    private void InspectorDivider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_sideInspector == true) _viewModel.SavedDetailsWidth = Math.Clamp(InspectorColumn.ActualWidth, 340, 1000);
        else _viewModel.SavedDetailsHeight = Math.Clamp(InspectorRow.ActualHeight, 220, 700);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        TryEnableWindowsBackdrop();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await _viewModel.RefreshAsync().ConfigureAwait(true);
    }

    private void FindCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void TryEnableWindowsBackdrop()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var darkMode = 1;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

            var backdrop = DwmSystemBackdropAcrylic;
            _ = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        }
        catch
        {
            // Older Windows builds simply use the XAML glass theme.
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}

