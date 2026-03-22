using System.Windows;
using System.Windows.Controls;
using TimecodeBridge.ViewModels;

namespace TimecodeBridge.Views;

public partial class RelayControlView : UserControl
{
    public RelayControlView()
    {
        InitializeComponent();
    }

    private void OnHostCheckChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RelayViewModel vm)
        {
            vm.UpdateHostSelectionsCommand.Execute(null);
        }
    }
}
