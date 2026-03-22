namespace TimecodeBridge.Services;

/// <summary>
/// Event arguments for timecode status change notifications.
/// </summary>
public class TimecodeStatusChangedEventArgs : EventArgs
{
    public bool IsReceiving { get; }

    public TimecodeStatusChangedEventArgs(bool isReceiving)
    {
        IsReceiving = isReceiving;
    }
}
