using NAudio.Wave;
using TimecodeBridge.Models;

namespace TimecodeBridge.Services.Interfaces;

public interface ILtcEncoder : IWaveProvider
{
    float VolumeLevel { get; set; }

    void Initialize(int sampleRate, FrameRate frameRate);
    void EnqueueFrame(TimecodeValue frame);
    void Reset();
}
