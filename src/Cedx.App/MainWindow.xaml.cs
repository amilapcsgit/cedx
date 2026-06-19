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

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new FileAssetRepository(new AssetTextParser()));
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
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
