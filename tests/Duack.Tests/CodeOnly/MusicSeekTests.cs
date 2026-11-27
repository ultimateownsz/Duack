using System;
using System.Collections.Generic;
using System.Linq;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the clip backend's seek rebuild (#70): a seekable clip's
/// <see cref="SoundEffectMusicBackend.Seek"/> swaps only the active instance for an
/// offset-created one — started before the old stops (no gap), volume/pitch and pause carried
/// over, readback set to the offset, the outgoing track of an in-flight crossfade untouched,
/// the old instance released — and an unseekable clip is a fail-soft no-op.
/// </summary>
public class MusicSeekTests
{
    private sealed class FakeSeekableInstance : IAudioInstance
    {
        public bool IsLooped { get; set; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public bool IsPlaying { get; private set; }
        public bool Disposed { get; private set; }
        public void Play() => IsPlaying = true;
        public void Stop() => IsPlaying = false;
        public void Pause() { }
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeSeekableClip : ISeekableAudioClip
    {
        public List<FakeSeekableInstance> Instances { get; } = new();
        public List<TimeSpan> Offsets { get; } = new();
        public List<IAudioInstance> Released { get; } = new();

        public IAudioInstance CreateInstance()
        {
            var instance = new FakeSeekableInstance();
            Instances.Add(instance);
            return instance;
        }

        public IAudioInstance? CreateInstance(TimeSpan startOffset)
        {
            Offsets.Add(startOffset);
            return CreateInstance();
        }

        public float? DurationSeconds => null;
        public void Release(IAudioInstance instance) => Released.Add(instance);
        public void Dispose() { }
    }

    private sealed class SingleClipLibrary : IAudioClipLibrary
    {
        private readonly IAudioClip _clip;
        public SingleClipLibrary(IAudioClip clip) => _clip = clip;
        public IAudioClip? Get(string trackName) => _clip;
    }

    private sealed class PairedClipLibrary : IAudioClipLibrary
    {
        private readonly IAudioClip _first;
        private readonly IAudioClip _second;
        private int _calls;

        public PairedClipLibrary(IAudioClip first, IAudioClip second) => (_first, _second) = (first, second);

        public IAudioClip? Get(string trackName) => ++_calls == 1 ? _first : _second;
    }

    [Fact]
    public void Seek_rebuilds_only_the_active_instance()
    {
        var clip = new FakeSeekableClip();
        var backend = new SoundEffectMusicBackend(new SingleClipLibrary(clip));
        backend.PlayTrack("overworld", 0.3f, 0.25f);
        backend.SetActiveVolume(0.4f);
        var old = Assert.Single(clip.Instances);
        Assert.Equal(0.4f, old.Volume, 4);

        backend.Seek(TimeSpan.FromSeconds(13));

        var replacement = clip.Instances[1];
        Assert.Equal(TimeSpan.FromSeconds(13), clip.Offsets.Single());
        Assert.Equal(TimeSpan.FromSeconds(13), backend.Position);
        Assert.True(backend.IsTrackPlaying);
        // The replacement started before the old instance stopped - no audible gap.
        Assert.True(replacement.IsPlaying);
        Assert.Equal(0.4f, replacement.Volume, 4);
        Assert.Equal(0.25f, replacement.Pitch, 4);
        Assert.False(old.IsPlaying);
        Assert.True(old.Disposed);
        Assert.Equal(new[] { old }, clip.Released);
    }

    [Fact]
    public void Seek_mid_crossfade_keeps_the_outgoing_track()
    {
        var firstClip = new FakeSeekableClip();
        var secondClip = new FakeSeekableClip();
        var backend = new SoundEffectMusicBackend(new PairedClipLibrary(firstClip, secondClip));
        backend.PlayTrack("overworld", 0.5f, 0f);
        backend.PlayTrack("battle", 0.5f, 0f);
        var outgoing = Assert.Single(firstClip.Instances);
        var oldActive = Assert.Single(secondClip.Instances);

        backend.Seek(TimeSpan.FromSeconds(3));

        // The outgoing track of the in-flight crossfade keeps playing untouched; only the
        // replaced active instance is stopped, disposed and released.
        Assert.False(outgoing.Disposed);
        Assert.True(oldActive.Disposed);
        Assert.Empty(firstClip.Released);
        Assert.Equal(new[] { oldActive }, secondClip.Released);
    }

    [Fact]
    public void Seek_while_paused_stays_paused()
    {
        var clip = new FakeSeekableClip();
        var backend = new SoundEffectMusicBackend(new SingleClipLibrary(clip));
        backend.PlayTrack("overworld", 0.5f, 0f);
        backend.Pause();
        var before = Assert.Single(clip.Instances);

        backend.Seek(TimeSpan.FromSeconds(9));

        Assert.True(backend.IsPaused);
        Assert.Equal(TimeSpan.FromSeconds(9), backend.Position);
        var replacement = clip.Instances[1];
        Assert.False(replacement.IsPlaying);
        Assert.True(before.Disposed);
    }

    [Fact]
    public void Seek_on_an_unseekable_clip_reports_and_touches_nothing()
    {
        var clip = new FakeAudioClip();
        var backend = new SoundEffectMusicBackend(new SingleClipLibrary(clip));
        backend.PlayTrack("overworld", 0.5f, 0f);
        backend.Update(4f);
        var active = Assert.Single(clip.Instances);

        backend.Seek(TimeSpan.FromSeconds(13));

        Assert.Same(active, Assert.Single(clip.Instances));
        Assert.False(active.Disposed);
        Assert.Equal(TimeSpan.FromSeconds(4), backend.Position);
    }

    [Fact]
    public void SupportsSeek_requires_the_clip_capability()
    {
        var seekable = new SoundEffectMusicBackend(new SingleClipLibrary(new FakeSeekableClip()));
        seekable.PlayTrack("overworld", 0.5f, 0f);
        Assert.True(seekable.SupportsSeek);

        var plain = new SoundEffectMusicBackend(new SingleClipLibrary(new FakeAudioClip()));
        plain.PlayTrack("overworld", 0.5f, 0f);
        Assert.False(plain.SupportsSeek);
    }

    [Fact]
    public void Seek_before_any_play_is_a_noop()
    {
        var backend = new SoundEffectMusicBackend(new SingleClipLibrary(new FakeSeekableClip()));

        backend.Seek(TimeSpan.FromSeconds(13));

        Assert.Equal(TimeSpan.Zero, backend.Position);
        Assert.False(backend.IsTrackPlaying);
    }

    [Fact]
    public void Seek_clamps_a_negative_offset_to_zero()
    {
        var clip = new FakeSeekableClip();
        var backend = new SoundEffectMusicBackend(new SingleClipLibrary(clip));
        backend.PlayTrack("overworld", 0.5f, 0f);

        backend.Seek(TimeSpan.FromSeconds(-3));

        Assert.Equal(new[] { TimeSpan.Zero }, clip.Offsets);
        Assert.Equal(TimeSpan.Zero, backend.Position);
    }
}
