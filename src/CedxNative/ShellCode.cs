using System.Windows;

namespace CedxNative;

public partial class Shell : Window
{
    public Shell()
    {
        InitializeComponent();
        DataContext = new CedxNative.Vm.MainVm();
    }
}
