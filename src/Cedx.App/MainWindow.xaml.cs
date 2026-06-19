using System.Windows;
using Cedx.App.ViewModels;
using Cedx.Core.Parsing;
using Cedx.Core.Services;

namespace Cedx.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new FileAssetRepository(new AssetTextParser()));
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
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
}
