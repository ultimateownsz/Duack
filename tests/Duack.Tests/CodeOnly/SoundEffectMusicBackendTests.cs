using System;
using Duack;

namespace Duack.Tests.CodeOnly;

public class SoundEffectMusicBackendTests
{
    private static (SoundEffectMusicBackend backend, FakeAudioClipLibrary library, FakeAudioClip clip) Create()
    {
        var library = new FakeAudioClipLibrary();
        var clip = library.Add("overworld");
        return (new SoundEffectMusicBackend(library), library, clip);
    }

    [Fact]
    public void SupportsOverlap_IsTrue()
    {
        var (backend, _, _) = Create();
        Assert.True(backend.SupportsOverlap);
    }

    [Fact]
    public void PlayTrack_StartsNonLoopedInstanceAtRequestedVolumeAndPitch()
    {
        var (backend, _, clip) = Create();

        backend.PlayTrack("overworld", 0.42f, 0.1f);

        var instance = Assert.Single(clip.Instances);
        Assert.Equal(1, instance.PlayCount);
        Assert.False(instance.IsLooped);
        Assert.Equal(0.42f, instance.Volume, 3);
        Assert.Equal(0.1f, instance.Pitch, 3);
        Assert.True(backend.IsTrackPlaying);
    }

    [Fact]
    public void PlayTrack_UnknownName_StaysSilentAndDoesNotThrow()
    {
        var (backend, _, _) = Create();

        backend.PlayTrack("does-not-exist", 0.5f, 0f);

        Assert.False(backend.IsTrackPlaying);
    }

    [Fact]
    public void SetActiveVolume_SetsActiveInstanceVolume()
    {
        var (backend, _, clip) = Create();
        backend.PlayTrack("overworld", 0f, 0f);

        backend.SetActiveVolume(0.7f);

        Assert.Equal(0.7f, Assert.Single(clip.Instances).Volume, 3);
    }

    [Fact]
    public void PlayTrack_WhenAlreadyPlaying_DemotesOldToOutgoing()
    {
        var library = new FakeAudioClipLibrary();
        var first = library.Add("overworld");
        var second = library.Add("battle");
        var backend = new SoundEffectMusicBackend(library);

        backend.PlayTrack("overworld", 0.5f, 0f);
        backend.PlayTrack("battle", 0f, 0f);

        // Old instance keeps playing (overlap) but is now the outgoing slot.
        backend.SetOutgoingVolume(0.25f);
        Assert.Equal(0.25f, Assert.Single(first.Instances).Volume, 3);
        Assert.Equal(1, Assert.Single(first.Instances).PlayCount);

        // The new track is the active one.
        backend.SetActiveVolume(0.9f);
        Assert.Equal(0.9f, Assert.Single(second.Instances).Volume, 3);
    }

    [Fact]
    public void StopOutgoing_StopsAndDisposesOutgoing_LeavesActive()
    {
        var library = new FakeAudioClipLibrary();
        var first = library.Add("overworld");
        var second = library.Add("battle");
        var backend = new SoundEffectMusicBackend(library);

        backend.PlayTrack("overworld", 0.5f, 0f);
        backend.PlayTrack("battle", 0.5f, 0f);

        backend.StopOutgoing();

        var outgoing = Assert.Single(first.Instances);
        Assert.Equal(1, outgoing.StopCount);
        Assert.True(outgoing.Disposed);
        Assert.Single(second.Instances);
        Assert.True(backend.IsTrackPlaying);
    }

    [Fact]
    public void PlayTrack_Twice_DropsAnyOlderOutgoing()
    {
        var library = new FakeAudioClipLibrary();
        var first = library.Add("overworld");
        var second = library.Add("battle");
        var third = library.Add("menu");
        var backend = new SoundEffectMusicBackend(library);

        backend.PlayTrack("overworld", 0.5f, 0f);
        backend.PlayTrack("battle", 0.5f, 0f);
        backend.PlayTrack("menu", 0.5f, 0f);

        // The very first track was the outgoing when the second switch happened; it must be gone.
        Assert.True(Assert.Single(first.Instances).Disposed);
        Assert.Equal(1, Assert.Single(first.Instances).StopCount);
        Assert.False(Assert.Single(second.Instances).Disposed);
        Assert.True(backend.IsTrackPlaying);
    }

    [Fact]
    public void Stop_StopsAndDisposesActiveAndOutgoing()
    {
        var library = new FakeAudioClipLibrary();
        var first = library.Add("overworld");
        var second = library.Add("battle");
        var backend = new SoundEffectMusicBackend(library);

        backend.PlayTrack("overworld", 0.5f, 0f);
        backend.PlayTrack("battle", 0.5f, 0f);

        backend.Stop();

        Assert.True(Assert.Single(first.Instances).Disposed);
        Assert.True(Assert.Single(second.Instances).Disposed);
        Assert.False(backend.IsTrackPlaying);
    }

    [Fact]
    public void Update_WhenActiveReachesEnd_RaisesTrackEndedOnce()
    {
        var (backend, _, clip) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        backend.Update(0.016f);
        Assert.Equal(0, ended); // still playing

        Assert.Single(clip.Instances).SimulateNaturalEnd();
        backend.Update(0.016f);
        backend.Update(0.016f);

        Assert.Equal(1, ended);
        Assert.False(backend.IsTrackPlaying);
    }

    [Fact]
    public void Position_tracks_the_clip_clock_and_resets_on_stop()
    {
        var (backend, _, _) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);

        backend.Update(5f);
        backend.Update(0.5f);
        Assert.Equal(TimeSpan.FromSeconds(5.5), backend.Position);

        backend.Stop();
        Assert.Equal(TimeSpan.Zero, backend.Position);
    }

    [Fact]
    public void Position_is_zero_before_any_playback()
    {
        var (backend, _, _) = Create();

        Assert.Equal(TimeSpan.Zero, backend.Position);
    }

    [Fact]
    public void Pause_freezes_the_position_clock()
    {
        var (backend, _, _) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);

        backend.Update(5f);
        backend.Pause();
        backend.Update(3f);

        // The clip clock must freeze while paused, like the web backend's player position:
        // a context resume that reads Position after a pause must not skip ahead.
        Assert.Equal(TimeSpan.FromSeconds(5), backend.Position);
    }

    [Fact]
    public void Resume_continues_the_clock_from_the_paused_position()
    {
        var (backend, _, _) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);

        backend.Update(5f);
        backend.Pause();
        backend.Update(3f);
        backend.Resume();
        backend.Update(1f);

        Assert.Equal(TimeSpan.FromSeconds(6), backend.Position);
    }

    [Fact]
    public void Update_while_paused_does_not_raise_TrackEnded()
    {
        var (backend, _, _) = Create();
        backend.PlayTrack("overworld", 0.5f, 0f);
        var ended = 0;
        backend.TrackEnded += (_, _) => ended++;

        // A paused SoundEffectInstance reports IsPlaying == false; the end detection
        // must not read that as a natural end.
        backend.Pause();
        backend.Update(0.016f);
        backend.Update(0.016f);

        Assert.Equal(0, ended);
    }

    [Fact]
    public void Duration_reports_the_active_clips_duration()
    {
        var library = new FakeAudioClipLibrary();
        library.Add("overworld").DurationSeconds = 95.5f;
        var backend = new SoundEffectMusicBackend(library);

        Assert.Equal(TimeSpan.Zero, backend.Duration); // nothing active yet

        backend.PlayTrack("overworld", 0.5f, 0f);
        Assert.Equal(TimeSpan.FromSeconds(95.5), backend.Duration);

        backend.Stop();
        Assert.Equal(TimeSpan.Zero, backend.Duration);
    }

    [Fact]
    public void Clip_backends_cannot_seek()
    {
        var (backend, _, _) = Create();

        Assert.False(backend.SupportsSeek);
    }
}