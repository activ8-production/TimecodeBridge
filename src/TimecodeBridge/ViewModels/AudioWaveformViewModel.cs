using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TimecodeBridge.Services;
using TimecodeBridge.Services.Interfaces;

namespace TimecodeBridge.ViewModels;

public partial class AudioWaveformViewModel : DispatcherViewModel
{
    private const int DisplaySampleCount = 512;

    private readonly ITimecodeEngine _timecodeEngine;
    private readonly float[] _displayBuffer = new float[DisplaySampleCount];
    private int _writePos;

    [ObservableProperty] private PointCollection _waveformPoints = new();
    [ObservableProperty] private double _peakLevel;

    public AudioWaveformViewModel(ITimecodeEngine timecodeEngine)
    {
        _timecodeEngine = timecodeEngine;
        _timecodeEngine.AudioSamplesAvailable += OnAudioSamplesAvailable;
    }

    private void OnAudioSamplesAvailable(object? sender, AudioSamplesEventArgs e)
    {
        var samples = e.Samples;

        // Downsample: pick every Nth sample to fill the display buffer
        int step = Math.Max(1, samples.Length / 64);
        for (int i = 0; i < samples.Length; i += step)
        {
            _displayBuffer[_writePos] = samples[i];
            _writePos = (_writePos + 1) % DisplaySampleCount;
        }

        // Calculate peak level
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Math.Abs(samples[i]);
            if (abs > peak) peak = abs;
        }

        RunOnUiThread(() => UpdateWaveform(peak));
    }

    private void UpdateWaveform(float peak)
    {
        PeakLevel = peak;

        var points = new PointCollection(DisplaySampleCount);
        for (int i = 0; i < DisplaySampleCount; i++)
        {
            int idx = (_writePos + i) % DisplaySampleCount;
            double x = (double)i / (DisplaySampleCount - 1);
            double y = 0.5 - _displayBuffer[idx] * 0.5; // map [-1,1] to [1,0]
            points.Add(new Point(x, y));
        }

        WaveformPoints = points;
    }
}
