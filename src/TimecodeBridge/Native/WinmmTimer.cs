using System.Runtime.InteropServices;

namespace TimecodeBridge.Native;

/// <summary>
/// P/Invoke wrapper for winmm.dll timeBeginPeriod / timeEndPeriod.
/// Used to raise the Windows system timer resolution so that PeriodicTimer,
/// Thread.Sleep, and Task.Delay achieve ~1 ms granularity (vs. the default
/// 15.625 ms) — required for stable frame-rate timecode generation and relay.
/// </summary>
internal static class WinmmTimer
{
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", ExactSpelling = true)]
    private static extern uint TimeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", ExactSpelling = true)]
    private static extern uint TimeEndPeriod(uint uPeriod);

    private const uint TIMERR_NOERROR = 0;

    private static uint _activePeriodMs;

    public static bool Begin(uint periodMs = 1)
    {
        if (_activePeriodMs != 0) return false;
        if (TimeBeginPeriod(periodMs) != TIMERR_NOERROR) return false;
        _activePeriodMs = periodMs;
        return true;
    }

    public static void End()
    {
        if (_activePeriodMs == 0) return;
        TimeEndPeriod(_activePeriodMs);
        _activePeriodMs = 0;
    }
}
