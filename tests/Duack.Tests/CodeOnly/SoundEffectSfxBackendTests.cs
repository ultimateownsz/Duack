using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the MonoGame one-shot backend behind the core <see cref="ISfxBackend"/> seam: it starts a
/// non-looping instance per cue with the requested pitch/volume (clamped to engine ranges), reports
/// missing clips as failures, and reaps finished instances so a long session cannot leak them.
/// </summary>
public class SoundEffectSfxBackendTests
{
    [Fact]
    public void Play_starts_a_non_looping_instance_with_pitch_and_volume()
    {
        var clips = new FakeAudioClipLibrary();
        var clip = clips.Add("hit");
        var backend = new SoundEffectSfxBackend(clips);

        Assert.True(backend.Play("hit", pitch: 0.25f, volume: 0.8f));

        var instance = Assert.Single(clip.Instances);
        Assert.Equal(1, instance.PlayCount);
        Assert.False(instance.IsLooped);
        Assert.Equal(0.25f, instance.Pitch);
        Assert.Equal(0.8f, instance.Volume);
    }

    [Fact]
    public void Play_reports_false_when_the_clip_cannot_be_loaded()
    {
        var backend = new SoundEffectSfxBackend(new FakeAudioClipLibrary()); // no clips at all

        Assert.False(backend.Play("missing"));
    }

    [Fact]
    public void Play_clamps_pitch_and_volume_to_the_engine_ranges()
    {
        var clips = new FakeAudioClipLibrary();
        var clip = clips.Add("hit");
        var backend = new SoundEffectSfxBackend(clips);

        backend.Play("hit", pitch: 2f, volume: -1f);

        var instance = Assert.Single(clip.Instances);
        Assert.Equal(1f, instance.Pitch);
        Assert.Equal(0f, instance.Volume);
    }

    [Fact]
    public void Finished_instances_are_disposed_on_the_next_play()
    {
        var clips = new FakeAudioClipLibrary();
        var clip = clips.Add("hit");
        var backend = new SoundEffectSfxBackend(clips);

        backend.Play("hit");
        var first = Assert.Single(clip.Instances);
        first.SimulateNaturalEnd();

        backend.Play("hit");

        Assert.True(first.Disposed);
        Assert.Equal(2, clip.Instances.Count);
    }

    [Fact]
    public void Dispose_stops_and_disposes_every_tracked_instance()
    {
        var clips = new FakeAudioClipLibrary();
        var clip = clips.Add("hit");
        var backend = new SoundEffectSfxBackend(clips);

        backend.Play("hit");
        backend.Play("hit");
        backend.Dispose();

        Assert.Equal(2, clip.Instances.Count);
        Assert.All(clip.Instances, instance =>
        {
            Assert.Equal(1, instance.StopCount);
            Assert.True(instance.Disposed);
        });
    }

    [Fact]
    public void The_constructor_rejects_a_null_clip_library()
    {
        Assert.Throws<ArgumentNullException>(() => new SoundEffectSfxBackend(null!));
    }
}
