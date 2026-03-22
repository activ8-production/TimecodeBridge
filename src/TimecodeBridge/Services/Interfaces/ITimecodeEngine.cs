using TimecodeBridge.Models;

namespace TimecodeBridge.Services.Interfaces;

public interface ITimecodeEngine
{
    TimecodeValue CurrentRawTimecode { get; }
    TimecodeValue CurrentOffsetTimecode { get; }
    TimecodeOffset Offset { get; set; }
    FrameRate FrameRate { get; }
    TimecodeSourceType ActiveSource { get; }
    bool IsReceiving { get; }

    void StartLtc(string audioDeviceId, bool isLoopback = false);
    void StartGenerator(GeneratorSettings settings);
    void ResumeGenerator();
    void ResetGenerator();
    void StopGenerator();
    void Stop();

    event EventHandler<TimecodeUpdatedEventArgs> TimecodeUpdated;
    event EventHandler<TimecodeStatusChangedEventArgs> StatusChanged;
    event EventHandler<AudioSamplesEventArgs> AudioSamplesAvailable;
}
