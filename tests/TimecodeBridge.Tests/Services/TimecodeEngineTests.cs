namespace TimecodeBridge.Tests.Services;

using TimecodeBridge.Models;
using TimecodeBridge.Services;

public class TimecodeEngineTests : IDisposable
{
    private readonly TimecodeEngine _engine;

    public TimecodeEngineTests()
    {
        _engine = new TimecodeEngine(FrameRate.Fps24);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }

    [Fact]
    public async Task WriteFrame_RaisesTimecodeUpdatedEvent()
    {
        var tcs = new TaskCompletionSource<TimecodeUpdatedEventArgs>();
        _engine.TimecodeUpdated += (_, e) => tcs.TrySetResult(e);

        var frame = new TimecodeValue(1, 0, 0, 0, FrameRate.Fps24);
        _engine.WriteFrame(frame);

        var result = await WithTimeout(tcs.Task);

        Assert.Equal(frame, result.RawTimecode);
        Assert.Equal(frame, result.OffsetTimecode);
    }

    [Fact]
    public async Task WriteFrame_AppliesOffsetCorrectly()
    {
        var offset = new TimecodeOffset(false, 0, 0, 5, 0, FrameRate.Fps24);
        _engine.Offset = offset;

        var tcs = new TaskCompletionSource<TimecodeUpdatedEventArgs>();
        _engine.TimecodeUpdated += (_, e) => tcs.TrySetResult(e);

        var frame = new TimecodeValue(1, 0, 0, 0, FrameRate.Fps24);
        _engine.WriteFrame(frame);

        var result = await WithTimeout(tcs.Task);

        Assert.Equal(frame, result.RawTimecode);
        var expected = frame.Add(offset);
        Assert.Equal(expected, result.OffsetTimecode);
    }

    [Fact]
    public async Task WriteFrame_AppliesNegativeOffsetCorrectly()
    {
        var offset = new TimecodeOffset(true, 0, 0, 10, 0, FrameRate.Fps24);
        _engine.Offset = offset;

        var tcs = new TaskCompletionSource<TimecodeUpdatedEventArgs>();
        _engine.TimecodeUpdated += (_, e) => tcs.TrySetResult(e);

        var frame = new TimecodeValue(1, 0, 0, 0, FrameRate.Fps24);
        _engine.WriteFrame(frame);

        var result = await WithTimeout(tcs.Task);

        Assert.Equal(frame, result.RawTimecode);
        var expected = frame.Add(offset);
        Assert.Equal(expected, result.OffsetTimecode);
    }

    [Fact]
    public async Task WriteFrame_UpdatesIsReceivingToTrue()
    {
        Assert.False(_engine.IsReceiving);

        var tcs = new TaskCompletionSource<TimecodeStatusChangedEventArgs>();
        _engine.StatusChanged += (_, e) => tcs.TrySetResult(e);

        var frame = new TimecodeValue(0, 0, 1, 0, FrameRate.Fps24);
        _engine.WriteFrame(frame);

        var result = await WithTimeout(tcs.Task);

        Assert.True(result.IsReceiving);
        Assert.True(_engine.IsReceiving);
    }

    [Fact]
    public async Task SignalLossDetected_After500ms()
    {
        // First, start receiving
        var receivingTcs = new TaskCompletionSource<TimecodeStatusChangedEventArgs>();
        _engine.StatusChanged += (_, e) =>
        {
            if (e.IsReceiving)
                receivingTcs.TrySetResult(e);
        };

        var frame = new TimecodeValue(0, 0, 1, 0, FrameRate.Fps24);
        _engine.WriteFrame(frame);
        await WithTimeout(receivingTcs.Task);

        // Now wait for signal loss
        var lostTcs = new TaskCompletionSource<TimecodeStatusChangedEventArgs>();
        _engine.StatusChanged += (_, e) =>
        {
            if (!e.IsReceiving)
                lostTcs.TrySetResult(e);
        };

        // Wait for signal loss (should happen after ~500ms)
        var result = await WithTimeout(lostTcs.Task, 2000);

        Assert.False(result.IsReceiving);
        Assert.False(_engine.IsReceiving);
    }

    [Fact]
    public async Task CurrentRawTimecode_ReturnsLatestValue()
    {
        var tcs = new TaskCompletionSource<TimecodeUpdatedEventArgs>();
        _engine.TimecodeUpdated += (_, e) => tcs.TrySetResult(e);

        var frame = new TimecodeValue(2, 30, 15, 10, FrameRate.Fps24);
        _engine.WriteFrame(frame);

        await WithTimeout(tcs.Task);

        Assert.Equal(frame, _engine.CurrentRawTimecode);
    }

    [Fact]
    public async Task CurrentOffsetTimecode_ReturnsOffsetAppliedValue()
    {
        var offset = new TimecodeOffset(false, 0, 1, 0, 0, FrameRate.Fps24);
        _engine.Offset = offset;

        var tcs = new TaskCompletionSource<TimecodeUpdatedEventArgs>();
        _engine.TimecodeUpdated += (_, e) => tcs.TrySetResult(e);

        var frame = new TimecodeValue(1, 0, 0, 0, FrameRate.Fps24);
        _engine.WriteFrame(frame);

        await WithTimeout(tcs.Task);

        Assert.Equal(frame, _engine.CurrentRawTimecode);
        Assert.Equal(frame.Add(offset), _engine.CurrentOffsetTimecode);
    }

    [Fact]
    public async Task MultipleFrames_LastValueWins()
    {
        var count = 0;
        var tcs = new TaskCompletionSource<TimecodeUpdatedEventArgs>();
        _engine.TimecodeUpdated += (_, e) =>
        {
            if (Interlocked.Increment(ref count) >= 3)
                tcs.TrySetResult(e);
        };

        _engine.WriteFrame(new TimecodeValue(0, 0, 1, 0, FrameRate.Fps24));
        _engine.WriteFrame(new TimecodeValue(0, 0, 2, 0, FrameRate.Fps24));
        _engine.WriteFrame(new TimecodeValue(0, 0, 3, 0, FrameRate.Fps24));

        var result = await WithTimeout(tcs.Task);

        Assert.Equal(new TimecodeValue(0, 0, 3, 0, FrameRate.Fps24), result.RawTimecode);
        Assert.Equal(new TimecodeValue(0, 0, 3, 0, FrameRate.Fps24), _engine.CurrentRawTimecode);
    }

    [Fact]
    public async Task SignalLoss_NotTriggeredWhileReceiving()
    {
        // Start receiving
        var receivingTcs = new TaskCompletionSource<TimecodeStatusChangedEventArgs>();
        _engine.StatusChanged += (_, e) =>
        {
            if (e.IsReceiving)
                receivingTcs.TrySetResult(e);
        };

        _engine.WriteFrame(new TimecodeValue(0, 0, 1, 0, FrameRate.Fps24));
        await WithTimeout(receivingTcs.Task);

        // Keep sending frames every 200ms for 1 second
        var lostTcs = new TaskCompletionSource<TimecodeStatusChangedEventArgs>();
        _engine.StatusChanged += (_, e) =>
        {
            if (!e.IsReceiving)
                lostTcs.TrySetResult(e);
        };

        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(200);
            _engine.WriteFrame(new TimecodeValue(0, 0, 1, i, FrameRate.Fps24));
        }

        // Signal loss should not have occurred
        Assert.False(lostTcs.Task.IsCompleted);
        Assert.True(_engine.IsReceiving);
    }

    [Fact]
    public void FrameRate_ReturnsConfiguredValue()
    {
        Assert.Equal(FrameRate.Fps24, _engine.FrameRate);
    }

    [Fact]
    public void Offset_DefaultIsZero()
    {
        var offset = _engine.Offset;
        Assert.Equal(0, offset.TotalFrames());
    }

    [Fact]
    public void Stop_SetsIsReceivingToFalse()
    {
        _engine.Stop();
        Assert.False(_engine.IsReceiving);
    }

    [Fact]
    public async Task Stop_RaisesStatusChangedIfWasReceiving()
    {
        // Start receiving
        var receivingTcs = new TaskCompletionSource<TimecodeStatusChangedEventArgs>();
        _engine.StatusChanged += (_, e) =>
        {
            if (e.IsReceiving)
                receivingTcs.TrySetResult(e);
        };

        _engine.WriteFrame(new TimecodeValue(0, 0, 1, 0, FrameRate.Fps24));
        await WithTimeout(receivingTcs.Task);

        // Now stop
        var stoppedTcs = new TaskCompletionSource<TimecodeStatusChangedEventArgs>();
        _engine.StatusChanged += (_, e) =>
        {
            if (!e.IsReceiving)
                stoppedTcs.TrySetResult(e);
        };

        _engine.Stop();

        var result = await WithTimeout(stoppedTcs.Task);
        Assert.False(result.IsReceiving);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var engine = new TimecodeEngine(FrameRate.Fps30);
        engine.WriteFrame(new TimecodeValue(0, 0, 1, 0, FrameRate.Fps30));
        engine.Dispose();
        // Double dispose should also be safe
        engine.Dispose();
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs = 3000)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
        if (completed != task)
            throw new TimeoutException($"Operation did not complete within {timeoutMs}ms.");
        return await task;
    }
}
