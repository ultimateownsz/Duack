using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the clip-backed SFX backend's positional capability: a pan travels to instances that
/// can carry one, is dropped for instances that cannot (the additive fallback, mono-centered),
/// and a live voice re-mixes volume and pan through the same handle StopVoice takes.
/// </summary>
public class SoundEffectSfxBackendPositionalTests
{
    private sealed class FakePanningInstance : IAudioInstance, IPanningVoice
    {
        public bool IsLooped { get; set; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float Pan { get; set; }
        public bool IsPlaying { get; private set; }

        public void Play() => IsPlaying = true;
        public void Stop() => IsPlaying = false;
        public void Dispose() { }
    }

    private sealed class PanningAudioClip : IAudioClip
    {
        public FakePanningInstance? Last { get; private set; }

        public IAudioInstance CreateInstance()
        {
            Last = new FakePanningInstance();
            return Last;
        }

        public void Dispose() { }
    }

    private sealed class SingleClipLibrary : IAudioClipLibrary
    {
        private readonly IAudioClip? _clip;
        public SingleClipLibrary(IAudioClip? clip) => _clip = clip;
        public IAudioClip? Get(string trackName) => trackName == "hit" ? _clip : null;
    }

    [Fact]
    public void PlayTrackedAt_applies_the_pan_to_a_panning_instance()
    {
        var clip = new PanningAudioClip();
        var backend = new SoundEffectSfxBackend(new SingleClipLibrary(clip));

        var handle = backend.PlayTrackedAt("hit", pan: 0.4f, pitch: 0f, volume: 0.8f);
        var instance = Assert.IsAssignableFrom<FakePanningInstance>(handle);

        Assert.Equal(0.4f, instance.Pan, 4);
        Assert.Equal(0.8f, instance.Volume, 4);
        Assert.True(instance.IsPlaying);
    }

    [Fact]
    public void PlayTrackedAt_on_a_non_panning_instance_plays_centered_instead()
    {
        // The shared FakeAudioInstance carries no IPanningVoice: pan has nowhere to land.
        var clip = new FakeAudioClip();
        var backend = new SoundEffectSfxBackend(new SingleClipLibrary(clip));

        var handle = backend.PlayTrackedAt("hit", pan: -0.8f, volume: 0.5f);

        Assert.NotNull(handle); // the play itself succeeds: the pan is the only thing dropped
        var instance = Assert.IsAssignableFrom<FakeAudioInstance>(handle);
        Assert.Equal(0.5f, instance.Volume, 4);
    }

    [Fact]
    public void SetVoiceMix_updates_volume_and_pan_of_one_live_voice()
    {
        var clip = new PanningAudioClip();
        var backend = new SoundEffectSfxBackend(new SingleClipLibrary(clip));
        var first = (FakePanningInstance)backend.PlayTrackedAt("hit", pan: 0f)!;
        var second = (FakePanningInstance)backend.PlayTrackedAt("hit", pan: 0f)!;

        backend.SetVoiceMix(second, volume: 0.3f, pan: -0.45f);

        Assert.Equal(0.3f, second.Volume, 4);
        Assert.Equal(-0.45f, second.Pan, 4);
        Assert.Equal(1f, first.Volume, 4); // the sibling voice is untouched
        Assert.Equal(0f, first.Pan, 4);
    }

    [Fact]
    public void SetVoiceMix_for_an_unknown_or_stale_handle_is_a_no_op()
    {
        var clip = new PanningAudioClip();
        var backend = new SoundEffectSfxBackend(new SingleClipLibrary(clip));
        backend.PlayTrackedAt("hit", pan: 0f);
        var instance = clip.Last!;

        backend.SetVoiceMix(new object(), volume: 0f, pan: 1f);
        backend.SetVoiceMix("not-a-handle", volume: 0f, pan: 1f);

        Assert.Equal(0f, instance.Pan, 4); // untouched
        Assert.Equal(1f, instance.Volume, 4);
    }

    [Fact]
    public void SetVoiceMix_clamps_its_values_into_the_engine_ranges()
    {
        var clip = new PanningAudioClip();
        var backend = new SoundEffectSfxBackend(new SingleClipLibrary(clip));
        var instance = (FakePanningInstance)backend.PlayTrackedAt("hit", pan: 0f)!;

        backend.SetVoiceMix(instance, volume: 7f, pan: 5f);

        Assert.Equal(1f, instance.Volume, 4);
        Assert.Equal(1f, instance.Pan, 4);
    }

    [Fact]
    public void A_positional_handle_is_the_voice_stop_and_play_track_take()
    {
        var clip = new PanningAudioClip();
        var backend = new SoundEffectSfxBackend(new SingleClipLibrary(clip));
        var handle = backend.PlayTrackedAt("hit", pan: 0.2f);
        var instance = (FakePanningInstance)handle!;

        backend.StopVoice(instance); // the same identity contract plain plays keep

        Assert.False(instance.IsPlaying);
    }
}
