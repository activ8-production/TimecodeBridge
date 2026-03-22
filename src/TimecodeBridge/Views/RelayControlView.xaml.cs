using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

/// <summary>
/// Converts a bool to a toggle label string ("ON" / "OFF").
/// </summary>
public sealed class BoolToToggleLabelConverter : IValueConverter
{
    public static readonly BoolToToggleLabelConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "ON" : "OFF";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a ComboBox SelectedIndex to bool (index 1 = Custom = true).
/// </summary>
public sealed class IndexToBoolConverter : IValueConverter
{
    public static readonly IndexToBoolConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int index && index == 1;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
