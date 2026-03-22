using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using TimecodeBridge.Models;
using TimecodeBridge.Services.Interfaces;

namespace TimecodeBridge.Services;

public class TimecodeEngine : ITimecodeEngine, IDisposable
{
    private readonly Channel<TimecodeValue> _channel;
    private readonly CancellationTokenSource _cts;
    private readonly Thread _workerThread;
    private readonly object _lock = new();

    // Signal loss detection
    private readonly Timer _signalLossTimer;
    private const int SignalLossTimeoutMs = 500;

    // LTC capture
    private WasapiCapture? _wasapiCapture;
    private LtcDecoder? _ltcDecoder;
    private readonly ManualResetEventSlim _captureStoppedEvent = new(true);

    // Generator
    private TimecodeGenerator? _generator;
    private LtcEncoder? _ltcEncoder;
    private WasapiOut? _wasapiOut;

    // Thread-safe state
    private volatile bool _isReceiving;
    private volatile bool _disposed;
    private TimecodeValue _currentRawTimecode;
    private TimecodeValue _currentOffsetTimecode;
    private TimecodeOffset _offset;
    private TimecodeSourceType _activeSource;

    public TimecodeValue CurrentRawTimecode
    {
        get { lock (_lock) return _currentRawTimecode; }
    }

    public TimecodeValue CurrentOffsetTimecode
    {
        get { lock (_lock) return _currentOffsetTimecode; }
    }

    public TimecodeOffset Offset
    {
        get { lock (_lock) return _offset; }
        set { lock (_lock) _offset = value; }
    }

    public FrameRate FrameRate { get; }

    public TimecodeSourceType ActiveSource
    {
        get { lock (_lock) return _activeSource; }
        private set { lock (_lock) _activeSource = value; }
    }

    public bool IsReceiving => _isReceiving;

    public event EventHandler<TimecodeUpdatedEventArgs>? TimecodeUpdated;
    public event EventHandler<TimecodeStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<AudioSamplesEventArgs>? AudioSamplesAvailable;

    public TimecodeEngine(FrameRate frameRate)
    {
        FrameRate = frameRate;
        _offset = TimecodeOffset.Zero(frameRate);

        _channel = Channel.CreateUnbounded<TimecodeValue>(
            new UnboundedChannelOptions { SingleWriter = true });

        _cts = new CancellationTokenSource();
        _signalLossTimer = new Timer(OnSignalLossTimeout, null, Timeout.Infinite, Timeout.Infinite);

        _workerThread = new Thread(WorkerLoop)
        {
            Name = "TimecodeEngine-Worker",
            IsBackground = true,
        };
        _workerThread.Start();
    }

    /// <summary>
    /// Writes a timecode frame into the channel pipeline.
    /// Called from capture threads (LTC decoder).
    /// </summary>
    internal void WriteFrame(TimecodeValue frame)
    {
        if (_disposed) return;
        _channel.Writer.TryWrite(frame);
    }

    public void StartLtc(string audioDeviceId, bool isLoopback = false)
    {
        StopLtcCapture();

        ActiveSource = TimecodeSourceType.Ltc;

        _ltcDecoder = new LtcDecoder();

        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(audioDeviceId);
        _wasapiCapture = isLoopback
            ? new WasapiLoopbackCapture(device)
            : new WasapiCapture(device);

        var waveFormat = _wasapiCapture.WaveFormat;
        int fps = FrameRate.FramesPerSecond();
        _ltcDecoder.Initialize(waveFormat.SampleRate, fps);

        int channels = waveFormat.Channels;
        int bitsPerSample = waveFormat.BitsPerSample;
        int sampleRate = waveFormat.SampleRate;

        _ltcDecoder.FrameDecoded += (_, timecodeValue) => WriteFrame(timecodeValue);

        _wasapiCapture.DataAvailable += (_, e) =>
        {
            try
            {
                _ltcDecoder?.ProcessSamples(e.Buffer, e.BytesRecorded, sampleRate, bitsPerSample, channels);

                // Extract mono float samples for waveform display
                if (AudioSamplesAvailable != null)
                {
                    var samples = ExtractMonoSamples(e.Buffer, e.BytesRecorded, bitsPerSample, channels);
                    AudioSamplesAvailable.Invoke(this, new AudioSamplesEventArgs(samples));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LTC error: {ex.Message}");
            }
        };

        _wasapiCapture.RecordingStopped += (_, _) =>
        {
            _captureStoppedEvent.Set();
        };

        _captureStoppedEvent.Reset();
        _wasapiCapture.StartRecording();
    }

    public void StartGenerator(GeneratorSettings settings)
    {
        Stop();

        ActiveSource = TimecodeSourceType.Generator;

        _generator = new TimecodeGenerator();
        _ltcEncoder = new LtcEncoder();

        // Initialize LTC encoder and audio output
        const int sampleRate = 48000;
        _ltcEncoder.Initialize(sampleRate, settings.FrameRate);
        _ltcEncoder.VolumeLevel = settings.VolumeLevel;

        _generator.FrameGenerated += (_, tc) =>
        {
            WriteFrame(tc);
            _ltcEncoder?.EnqueueFrame(tc);
        };

        // Try to initialize audio output device
        if (!string.IsNullOrEmpty(settings.OutputDeviceId))
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(settings.OutputDeviceId);
                _wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
                _wasapiOut.Init(_ltcEncoder);
                _wasapiOut.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LTC output device error: {ex.Message}");
                _wasapiOut?.Dispose();
                _wasapiOut = null;
                // Graceful degradation: generator continues without LTC output
            }
        }

        _generator.Start(settings.StartTime, settings.FrameRate);
    }

    public void ResumeGenerator()
    {
        if (_generator == null) return;

        // Re-initialize LTC output if needed
        if (_ltcEncoder != null && _wasapiOut != null)
        {
            try { _wasapiOut.Play(); } catch { /* ignore */ }
        }

        _generator.Resume();
    }

    public void StopGenerator()
    {
        // Pause: stop the timer but keep the generator and its position
        _generator?.Stop();

        if (_wasapiOut != null)
        {
            try { _wasapiOut.Pause(); } catch { /* ignore */ }
        }

        if (_isReceiving)
        {
            _isReceiving = false;
            _signalLossTimer.Change(Timeout.Infinite, Timeout.Infinite);
            StatusChanged?.Invoke(this, new TimecodeStatusChangedEventArgs(false));
        }
    }

    public void ResetGenerator()
    {
        _generator?.Reset();
    }

    public void Stop()
    {
        DisposeGenerator();
        StopLtcCapture();

        if (_isReceiving)
        {
            _isReceiving = false;
            _signalLossTimer.Change(Timeout.Infinite, Timeout.Infinite);
            StatusChanged?.Invoke(this, new TimecodeStatusChangedEventArgs(false));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisposeGenerator();
        StopLtcCapture();

        _signalLossTimer.Dispose();
        _channel.Writer.TryComplete();
        _cts.Cancel();

        // Wait for the worker thread to finish (with timeout)
        _workerThread.Join(1000);

        _cts.Dispose();
    }

    private void DisposeGenerator()
    {
        if (_generator != null)
        {
            _generator.Stop();
            _generator.Dispose();
            _generator = null;
        }

        if (_wasapiOut != null)
        {
            try { _wasapiOut.Stop(); } catch { /* ignore */ }
            _wasapiOut.Dispose();
            _wasapiOut = null;
        }

        if (_ltcEncoder != null)
        {
            _ltcEncoder.Reset();
            _ltcEncoder = null;
        }
    }

    private void StopLtcCapture()
    {
        if (_wasapiCapture != null)
        {
            try { _wasapiCapture.StopRecording(); } catch { /* ignore if already stopped */ }

            // Wait for capture thread to fully stop before disposing decoder
            _captureStoppedEvent.Wait(2000);

            _wasapiCapture.Dispose();
            _wasapiCapture = null;
        }

        if (_ltcDecoder != null)
        {
            _ltcDecoder.Dispose();
            _ltcDecoder = null;
        }
    }

    private void WorkerLoop()
    {
        var reader = _channel.Reader;
        var token = _cts.Token;

        try
        {
            // Use synchronous blocking read pattern for the dedicated worker thread
            while (!token.IsCancellationRequested)
            {
                // WaitToReadAsync returns a ValueTask; we block on it here
                var waitTask = reader.WaitToReadAsync(token);
                if (!waitTask.IsCompleted)
                {
                    // Asynchronously wait, but block the thread
                    if (!waitTask.AsTask().GetAwaiter().GetResult())
                        break;
                }
                else if (!waitTask.Result)
                {
                    break;
                }

                while (reader.TryRead(out var frame))
                {
                    ProcessFrame(frame);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (ChannelClosedException)
        {
            // Channel completed
        }
    }

    private void ProcessFrame(TimecodeValue rawFrame)
    {
        // Normalize to engine's FrameRate to avoid fluctuations from LTC decoder inference
        rawFrame = new TimecodeValue(rawFrame.Hours, rawFrame.Minutes, rawFrame.Seconds, rawFrame.Frames, FrameRate);

        TimecodeOffset currentOffset;
        lock (_lock)
        {
            currentOffset = _offset;
        }

        var offsetFrame = rawFrame.Add(currentOffset);

        lock (_lock)
        {
            _currentRawTimecode = rawFrame;
            _currentOffsetTimecode = offsetFrame;
        }

        // Reset signal loss timer
        _signalLossTimer.Change(SignalLossTimeoutMs, Timeout.Infinite);

        // Transition to receiving state
        if (!_isReceiving)
        {
            _isReceiving = true;
            StatusChanged?.Invoke(this, new TimecodeStatusChangedEventArgs(true));
        }

        // Fire timecode updated event
        TimecodeUpdated?.Invoke(this, new TimecodeUpdatedEventArgs(rawFrame, offsetFrame));
    }

    private static float[] ExtractMonoSamples(byte[] buffer, int bytesRecorded, int bitsPerSample, int channels)
    {
        int bytesPerSample = bitsPerSample / 8;
        int bytesPerFrame = bytesPerSample * channels;
        int frameCount = bytesRecorded / bytesPerFrame;
        var samples = new float[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            int offset = i * bytesPerFrame;
            samples[i] = bitsPerSample switch
            {
                32 => BitConverter.ToSingle(buffer, offset),
                16 => BitConverter.ToInt16(buffer, offset) / 32768f,
                _ => 0f,
            };
        }

        return samples;
    }

    private void OnSignalLossTimeout(object? state)
    {
        if (_disposed) return;

        if (_isReceiving)
        {
            _isReceiving = false;
            StatusChanged?.Invoke(this, new TimecodeStatusChangedEventArgs(false));
        }
    }
}
