using System.Windows.Controls;
using System.Windows.Input;
using TimecodeBridge.ViewModels;

namespace TimecodeBridge.Views;

/// <summary>
/// Interaction logic for CueListView.xaml
/// </summary>
public partial class CueListView : UserControl
{
    public CueListView()
    {
        InitializeComponent();
    }

    private void CueItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item
            && item.DataContext is CueItemViewModel cueItem
            && DataContext is CueListViewModel viewModel)
        {
            viewModel.EditCueCommand.Execute(cueItem.Id);
            e.Handled = true;
        }
    }
}
