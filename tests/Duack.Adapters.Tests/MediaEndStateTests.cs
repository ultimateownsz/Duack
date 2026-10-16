using Duack.KNI;

namespace Duack.Adapters.Tests;

public class MediaEndStateTests
{
    private const float Settle = 0.25f;

    [Fact]
    public void Started_IsRunning()
    {
        var state = new MediaEndState(Settle);
        state.Started();
        Assert.True(state.IsRunning);
    }

    [Fact]
    public void ObserveStopped_InsideSettleWindow_IsIgnored()
    {
        var state = new MediaEndState(Settle);
        state.Started();

        Assert.False(state.ObserveStopped());
        Assert.True(state.IsRunning); // still considered running
    }

    [Fact]
    public void ObserveStopped_AfterSettleWindow_ReportsOnce()
    {
        var state = new MediaEndState(Settle);
        state.Started();
        state.Advance(Settle); // drain the window

        Assert.True(state.ObserveStopped());
        Assert.False(state.IsRunning);

        // A second observation of the same end must not report again.
        Assert.False(state.ObserveStopped());
    }

    [Fact]
    public void Advance_OnlyDrainsToZero()
    {
        var state = new MediaEndState(Settle);
        state.Started();
        state.Advance(10f);
        Assert.True(state.ObserveStopped());
    }

    [Fact]
    public void StoppedByUs_NeverReportsAnEnd()
    {
        var state = new MediaEndState(Settle);
        state.Started();
        state.Advance(Settle);

        state.StoppedByUs();

        Assert.False(state.ObserveStopped());
        Assert.False(state.IsRunning);
    }

    [Fact]
    public void StoppedByUs_ClearsSettleWindowForNextStart()
    {
        var state = new MediaEndState(Settle);
        state.Started();
        state.StoppedByUs();

        // A fresh start must re-arm the settle window (a stale event arrives right after).
        state.Started();
        Assert.False(state.ObserveStopped());
    }

    [Fact]
    public void ZeroSettle_ReportsImmediatelyOnce()
    {
        var state = new MediaEndState(0f);
        state.Started();
        Assert.True(state.ObserveStopped());
        Assert.False(state.ObserveStopped());
    }

    [Fact]
    public void ObserveStopped_WithoutStart_IsIgnored()
    {
        var state = new MediaEndState(Settle);
        Assert.False(state.ObserveStopped());
    }
}