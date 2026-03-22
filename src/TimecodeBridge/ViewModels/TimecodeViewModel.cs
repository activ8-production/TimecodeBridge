using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.CoreAudioApi;
using TimecodeBridge.Models;
using TimecodeBridge.Services;
using TimecodeBridge.Services.Interfaces;

namespace TimecodeBridge.ViewModels;

public partial class TimecodeViewModel : DispatcherViewModel
{
    private readonly ITimecodeEngine _timecodeEngine;
    private bool _hasEverReceived;
    private bool _generatorInitialized;

    [ObservableProperty] private string _rawTimecodeDisplay = "";
    [ObservableProperty] private string _offsetTimecodeDisplay = "";
    [ObservableProperty] private bool _isReceiving;
    [ObservableProperty] private string _statusText = "停止";
    [ObservableProperty] private AudioDeviceInfo? _selectedDevice;

    // Generator properties
    [ObservableProperty] private TimecodeSourceType _selectedSource = TimecodeSourceType.Ltc;
    [ObservableProperty] private bool _isGeneratorRunning;
    [ObservableProperty] private string _generatorStartTime = "00:00:00:00";
    [ObservableProperty] private FrameRate _generatorFrameRate = FrameRate.Fps30;
    [ObservableProperty] private AudioDeviceInfo? _selectedOutputDevice;
    [ObservableProperty] private float _outputVolumeLevel = 0.8f;
    [ObservableProperty] private bool _isLtcOutputActive;

    public bool IsGeneratorMode
    {
        get => SelectedSource == TimecodeSourceType.Generator;
        set { if (value) SelectedSource = TimecodeSourceType.Generator; }
    }

    public bool IsLtcMode
    {
        get => SelectedSource == TimecodeSourceType.Ltc;
        set { if (value) SelectedSource = TimecodeSourceType.Ltc; }
    }

    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();

    public IReadOnlyList<FrameRate> AvailableFrameRates { get; } =
        [FrameRate.Fps24, FrameRate.Fps25, FrameRate.Fps2997Drop, FrameRate.Fps30];

    private TimecodeOffset _offset;

    public TimecodeOffset Offset
    {
        get => _offset;
        set
        {
            if (SetProperty(ref _offset, value))
            {
                _timecodeEngine.Offset = value;
                OnPropertyChanged(nameof(OffsetText));
            }
        }
    }

    public string OffsetText
    {
        get => _offset.ToString();
        set
        {
            if (TimecodeOffset.TryParse(value, _timecodeEngine.FrameRate, out var parsed))
            {
                Offset = parsed;
            }
        }
    }

    public TimecodeViewModel(ITimecodeEngine timecodeEngine)
    {
        _timecodeEngine = timecodeEngine;
        _offset = timecodeEngine.Offset;

        _timecodeEngine.TimecodeUpdated += OnTimecodeUpdated;
        _timecodeEngine.StatusChanged += OnStatusChanged;

        RefreshAudioDevices();
    }

    partial void OnSelectedSourceChanged(TimecodeSourceType value)
    {
        OnPropertyChanged(nameof(IsGeneratorMode));
        OnPropertyChanged(nameof(IsLtcMode));

        _timecodeEngine.Stop();
        _hasEverReceived = false;
        _generatorInitialized = false;
        StatusText = "停止";
        IsReceiving = false;
        IsGeneratorRunning = false;
        IsLtcOutputActive = false;

        // If switching to LTC and a device is selected, auto-start
        if (value == TimecodeSourceType.Ltc && SelectedDevice is not null)
        {
            try
            {
                _timecodeEngine.StartLtc(SelectedDevice.Id, SelectedDevice.IsLoopback);
            }
            catch (Exception ex)
            {
                StatusText = "エラー";
                System.Diagnostics.Debug.WriteLine($"Device start failed: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void RefreshAudioDevices()
    {
        AudioDevices.Clear();
        OutputDevices.Clear();
        using var enumerator = new MMDeviceEnumerator();

        // Capture devices (input)
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        foreach (var device in devices)
        {
            AudioDevices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, IsLoopback: false));
        }

        // Render devices (for loopback capture and LTC output)
        var renderDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in renderDevices)
        {
            AudioDevices.Add(new AudioDeviceInfo(device.ID, $"{device.FriendlyName} (Loopback)", IsLoopback: true));
            OutputDevices.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, IsLoopback: false));
        }
    }

    partial void OnSelectedDeviceChanged(AudioDeviceInfo? value)
    {
        if (value is null || SelectedSource != TimecodeSourceType.Ltc) return;

        _timecodeEngine.Stop();

        try
        {
            _timecodeEngine.StartLtc(value.Id, value.IsLoopback);
        }
        catch (Exception ex)
        {
            StatusText = "エラー";
            System.Diagnostics.Debug.WriteLine($"Device start failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void StartGenerator()
    {
        if (SelectedSource != TimecodeSourceType.Generator) return;

        try
        {
            if (_generatorInitialized)
            {
                // Resume from paused position
                _timecodeEngine.ResumeGenerator();
            }
            else
            {
                // First start: initialize with settings
                var startTime = ParseTimecodeInput(GeneratorStartTime, GeneratorFrameRate);
                var settings = new GeneratorSettings
                {
                    FrameRate = GeneratorFrameRate,
                    StartTime = startTime,
                    OutputDeviceId = SelectedOutputDevice?.Id ?? string.Empty,
                    VolumeLevel = OutputVolumeLevel,
                };
                _timecodeEngine.StartGenerator(settings);
                _generatorInitialized = true;
            }

            IsGeneratorRunning = true;
            IsLtcOutputActive = SelectedOutputDevice is not null;
            StatusText = "生成中";
        }
        catch (Exception ex)
        {
            StatusText = "エラー";
            System.Diagnostics.Debug.WriteLine($"Generator start failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void StopGenerator()
    {
        _timecodeEngine.StopGenerator();
        IsGeneratorRunning = false;
        StatusText = "一時停止";
    }

    [RelayCommand]
    private void ResetGenerator()
    {
        _timecodeEngine.ResetGenerator();
    }

    private void OnTimecodeUpdated(object? sender, TimecodeUpdatedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            RawTimecodeDisplay = e.RawTimecode.ToString();
            OffsetTimecodeDisplay = e.OffsetTimecode.ToString();
        });
    }

    private void OnStatusChanged(object? sender, TimecodeStatusChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            IsReceiving = e.IsReceiving;

            if (SelectedSource == TimecodeSourceType.Generator)
            {
                if (e.IsReceiving)
                {
                    _hasEverReceived = true;
                    StatusText = "生成中";
                }
                else
                {
                    IsGeneratorRunning = false;
                    IsLtcOutputActive = false;
                    StatusText = _hasEverReceived ? "停止" : "停止";
                }
            }
            else
            {
                if (e.IsReceiving)
                {
                    _hasEverReceived = true;
                    StatusText = "受信中";
                }
                else
                {
                    StatusText = _hasEverReceived ? "信号喪失" : "停止";
                }
            }
        });
    }

    public TimecodeSourceSettings GetSourceSettings()
    {
        return new TimecodeSourceSettings
        {
            SourceType = SelectedSource,
            DeviceId = SelectedDevice?.Id ?? string.Empty,
            GeneratorSettings = new GeneratorSettings
            {
                FrameRate = GeneratorFrameRate,
                StartTime = ParseTimecodeInput(GeneratorStartTime, GeneratorFrameRate),
                OutputDeviceId = SelectedOutputDevice?.Id ?? string.Empty,
                VolumeLevel = OutputVolumeLevel,
            },
        };
    }

    public void RestoreSourceSettings(TimecodeSourceSettings settings)
    {
        // Stop current engine before switching
        _timecodeEngine.Stop();
        _hasEverReceived = false;
        _generatorInitialized = false;
        IsGeneratorRunning = false;
        IsLtcOutputActive = false;

        // Restore generator settings
        GeneratorFrameRate = settings.GeneratorSettings.FrameRate;
        GeneratorStartTime = settings.GeneratorSettings.StartTime.ToString();
        OutputVolumeLevel = settings.GeneratorSettings.VolumeLevel;

        // Restore output device selection
        SelectedOutputDevice = OutputDevices.FirstOrDefault(d => d.Id == settings.GeneratorSettings.OutputDeviceId);

        // Restore input device selection
        SelectedDevice = AudioDevices.FirstOrDefault(d => d.Id == settings.DeviceId);

        // Restore source type (this triggers OnSelectedSourceChanged which may auto-start LTC)
        SelectedSource = settings.SourceType;

        StatusText = "停止";
    }

    private static TimecodeValue ParseTimecodeInput(string input, FrameRate frameRate)
    {
        var parts = input.Replace(";", ":").Split(':');
        if (parts.Length == 4 &&
            int.TryParse(parts[0], out int h) &&
            int.TryParse(parts[1], out int m) &&
            int.TryParse(parts[2], out int s) &&
            int.TryParse(parts[3], out int f))
        {
            return new TimecodeValue(h, m, s, f, frameRate);
        }
        return new TimecodeValue(0, 0, 0, 0, frameRate);
    }
}

public record AudioDeviceInfo(string Id, string DisplayName, bool IsLoopback)
{
    public override string ToString() => DisplayName;
}
