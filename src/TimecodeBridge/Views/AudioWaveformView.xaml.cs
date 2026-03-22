using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TimecodeBridge.ViewModels;

namespace TimecodeBridge.Views;

public partial class AudioWaveformView : UserControl
{
    public AudioWaveformView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        WaveformCanvas.SizeChanged += (_, _) => Redraw();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is INotifyPropertyChanged newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioWaveformViewModel.WaveformPoints) ||
            e.PropertyName == nameof(AudioWaveformViewModel.PeakLevel))
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        if (DataContext is not AudioWaveformViewModel vm) return;

        double w = WaveformCanvas.ActualWidth;
        double h = WaveformCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Update waveform polyline
        var points = vm.WaveformPoints;
        var scaled = new PointCollection(points.Count);
        foreach (var pt in points)
        {
            scaled.Add(new Point(pt.X * w, pt.Y * h));
        }
        WaveformLine.Points = scaled;

        // Update peak bar height
        double peakHeight = Math.Clamp(vm.PeakLevel, 0, 1) * 50;
        PeakBar.Height = peakHeight;

        // Color: green < 0.6, yellow < 0.85, red >= 0.85
        PeakBar.Background = vm.PeakLevel switch
        {
            >= 0.85 => new SolidColorBrush(Color.FromRgb(0xE0, 0x52, 0x52)),
            >= 0.6 => new SolidColorBrush(Color.FromRgb(0xE0, 0xA0, 0x40)),
            _ => new SolidColorBrush(Color.FromRgb(0x60, 0xC0, 0x60)),
        };
    }
}

/// <summary>
/// Returns half of the input value. Used for center line positioning.
/// </summary>
public class HalfConverter : IValueConverter
{
    public static readonly HalfConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return d / 2.0;
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
