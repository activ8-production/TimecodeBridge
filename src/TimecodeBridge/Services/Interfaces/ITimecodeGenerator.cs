using TimecodeBridge.Models;

namespace TimecodeBridge.Services.Interfaces;

public interface ITimecodeGenerator
{
    TimecodeValue CurrentTimecode { get; }
    bool IsRunning { get; }

    void Start(TimecodeValue startTime, FrameRate frameRate);
    void Resume();
    void Stop();
    void Reset();

    event EventHandler<TimecodeValue> FrameGenerated;
}
