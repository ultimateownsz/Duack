using System;
using Duack.KNI;

namespace Duack.Adapters.Tests;

public class MediaPlayerMusicBackendTests
{
    private const float Settle = 0.25f;

    private static (MediaPlayerMusicBackend backend, FakeMediaPlayerHost host) Create() =>
        Create(new FakeMediaPlayerHost());

    private static (MediaPlayerMusicBackend backend, FakeMediaPlayerHost host) Create(FakeMediaPlayerHost host) =>
        (new MediaPlayerMusicBackend(host, settleSeconds: Settle), host);

    private static void DrainSettle(MediaPlayerMusicBackend backend) => backend.Update(Settle);

    [Fact]
    public void SupportsOverlap_IsFalse()
    {
        var (backend, _) = Create();
        Assert.False(backend.SupportsOverlap);
    }

    [Fact]
    public void PlayTrack_PlaysNonRepeatingAtRequestedVolume_AndResolvesOggUri()
    {
        var (backend, host) = Create();

        backend.PlayTrack("overworld", 0.3f, 0.5f);

        Assert.Equal("Content/Audio/overworld.ogg", host.LastUri);
        Assert.False(host.IsRepeating);
        Assert.Equal(0.3f, host.Volume, 3);
        Assert.True(backend.IsTrackPlaying);
    }

    [Fact]
    public void PlayTrack_PlayThrows_StaysSilentAndDoesNotReportEnd()
    {
        var host = new FakeMediaPlayerHost { ThrowOnPlay = new InvalidOperationException("missing file") };
        var (backend, _) = Create(host);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        backend.PlayTrack("overworld", 0.3f, 0f);

        Assert.False(backend.IsTrackPlaying);
        DrainSettle(backend);
        Assert.Equal(0, ended);
    }

    [Fact]
    public void SetActiveVolume_WritesHostVolume()
    {
        var (backend, host) = Create();
        backend.PlayTrack("overworld", 0f, 0f);

        backend.SetActiveVolume(0.8f);

        Assert.Equal(0.8f, host.Volume, 3);
    }

    [Fact]
    public void SetOutgoingVolume_And_StopOutgoing_AreNoOps()
    {
        var (backend, host) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);

        backend.SetOutgoingVolume(0f);
        backend.StopOutgoing();

        Assert.Equal(0.5f, host.Volume, 3);
        Assert.Equal(0, host.StopCount);
    }

    [Fact]
    public void NaturalEnd_RaisesTrackEndedOnce_AfterSettle()
    {
        var (backend, host) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        DrainSettle(backend);            // leave the churn window
        host.SimulateNaturalEnd();       // platform reports stopped + event

        Assert.Equal(1, ended);
        Assert.False(backend.IsTrackPlaying);

        backend.Update(0.016f);          // polling path must not double-report
        Assert.Equal(1, ended);
    }

    [Fact]
    public void Stop_NeverReportsAnEnd()
    {
        var (backend, host) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        DrainSettle(backend);
        backend.Stop();

        Assert.Equal(1, host.StopCount);
        Assert.False(backend.IsTrackPlaying);
        Assert.Equal(0, ended);
    }

    [Fact]
    public void StaleStopFromPriorTrack_WithinSettleWindow_DoesNotReportEnd()
    {
        var (backend, host) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);
        DrainSettle(backend);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        // A context switch in the core: Stop() immediately followed by Play(next).
        backend.Stop();
        backend.PlayTrack("battle", 0.0f, 0f);

        // The browser now delivers the OLD track's "stopped" signal, late, still inside the window.
        host.SimulateNaturalEnd();

        Assert.Equal(0, ended);
    }

    [Fact]
    public void StaleEventWhileStillPlaying_IsIgnored()
    {
        var (backend, host) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);
        DrainSettle(backend);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        // A spurious state-change while the player is genuinely playing must be ignored.
        host.RaiseStateChanged();

        Assert.Equal(0, ended);
        Assert.True(backend.IsTrackPlaying);
    }

    [Fact]
    public void Update_PollsForEnd_WhenPlatformEventNeverFires()
    {
        var (backend, host) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        // Platform stops silently (no StateChanged), then a later poll notices.
        host.StopWithoutEvent();
        DrainSettle(backend);

        Assert.Equal(1, ended);
    }
}