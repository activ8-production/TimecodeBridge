using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using TimecodeBridge.ViewModels;

namespace TimecodeBridge;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string FileFilter = "TimecodeBridge プロジェクト (*.json)|*.json|すべてのファイル (*.*)|*.*";

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
    }

    private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = FileFilter,
            Title = "プロジェクトを開く",
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.OpenProjectCommand.Execute(dialog.FileName);
        }
    }

    private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = FileFilter,
            Title = "名前を付けて保存",
            DefaultExt = ".json",
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.SaveProjectAsCommand.Execute(dialog.FileName);
        }
    }

    private void SetBackgroundMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "画像ファイル (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル (*.*)|*.*",
            Title = "背景画像を選択",
        };

        if (dialog.ShowDialog() == true && DataContext is MainViewModel vm)
        {
            vm.SetBackgroundImageCommand.Execute(dialog.FileName);
            LoadBackgroundImage(dialog.FileName);
        }
    }

    private void LoadBackgroundImage(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            BackgroundImage.Source = null;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            BackgroundImage.Source = bitmap;
        }
        catch
        {
            BackgroundImage.Source = null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel mainVm)
        {
            // Load background image from saved settings
            LoadBackgroundImage(mainVm.BackgroundImagePath);

            // Watch for background image changes
            mainVm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.BackgroundImagePath))
                    LoadBackgroundImage(mainVm.BackgroundImagePath);
            };

            // Wire child views to their ViewModels via DI
            TimecodeDisplay.DataContext = App.Services.GetRequiredService<TimecodeViewModel>();
            AudioWaveform.DataContext = App.Services.GetRequiredService<AudioWaveformViewModel>();
            CueList.DataContext = App.Services.GetRequiredService<CueListViewModel>();

            // Wire log panel
            var logViewModel = App.Services.GetRequiredService<LogViewModel>();
            LogListView.ItemsSource = logViewModel.Logs;
            ClearLogButton.Command = logViewModel.ClearLogsCommand;

            // Wire HostManager and RelayControl views
            HostManager.DataContext = App.Services.GetRequiredService<HostManagerViewModel>();
            RelayControl.DataContext = App.Services.GetRequiredService<RelayViewModel>();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel mainViewModel)
            return;

        if (!mainViewModel.HasUnsavedChanges)
            return;

        var result = MessageBox.Show(
            "プロジェクトに未保存の変更があります。保存しますか？",
            "確認",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (result)
        {
            case MessageBoxResult.Yes:
                mainViewModel.SaveProjectCommand.Execute(null);
                break;
            case MessageBoxResult.Cancel:
                e.Cancel = true;
                break;
            // MessageBoxResult.No => close without saving
        }
    }
}
