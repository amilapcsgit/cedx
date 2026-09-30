using System.Windows;
namespace Cedx.App;
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Window window=e.Args.Contains("--astra",StringComparer.OrdinalIgnoreCase)?new AstraWindow():new MainWindow();
        MainWindow=window;window.Show();
    }
}
