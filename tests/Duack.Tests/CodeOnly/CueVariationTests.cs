using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the sound-effect cue-variation model: each play can jitter its pitch and volume within a
/// configured range so repeated effects (footsteps, hits) do not sound machine-gunned. The range
/// is expressed in engine pitch units (MonoGame pitch: 0 = unchanged, +/-1 = one octave) and a
/// linear volume scale around 1.
/// </summary>
public class CueVariationTests
{
    [Fact]
    public void None_plays_at_the_base_pitch_and_volume()
    {
        var play = CueVariation.None.Resolve(new Random(1));

        Assert.Equal(0f, play.Pitch);
        Assert.Equal(1f, play.VolumeScale);
    }

    [Fact]
    public void Resolved_pitch_stays_within_the_configured_range()
    {
        var variation = new CueVariation(pitchRange: 0.2f, volumeRange: 0f);
        var random = new Random(42);

        for (var i = 0; i < 200; i++)
        {
            var play = variation.Resolve(random);
            Assert.InRange(play.Pitch, -0.2f, 0.2f);
            Assert.Equal(1f, play.VolumeScale);
        }
    }

    [Fact]
    public void Resolved_volume_stays_within_the_configured_range()
    {
        var variation = new CueVariation(pitchRange: 0f, volumeRange: 0.1f);
        var random = new Random(7);

        for (var i = 0; i < 200; i++)
        {
            var play = variation.Resolve(random);
            Assert.Equal(0f, play.Pitch);
            Assert.InRange(play.VolumeScale, 0.9f, 1.1f);
        }
    }

    [Fact]
    public void Pitch_is_clamped_to_the_engine_range()
    {
        var variation = new CueVariation(pitchRange: 5f, volumeRange: 0f);
        var random = new Random(3);

        for (var i = 0; i < 200; i++)
            Assert.InRange(variation.Resolve(random).Pitch, -1f, 1f);
    }

    [Fact]
    public void Volume_scale_never_goes_negative()
    {
        var variation = new CueVariation(pitchRange: 0f, volumeRange: 5f);
        var random = new Random(11);

        for (var i = 0; i < 200; i++)
            Assert.True(variation.Resolve(random).VolumeScale >= 0f);
    }

    [Fact]
    public void Same_seed_produces_the_same_jitter()
    {
        var variation = new CueVariation(pitchRange: 0.3f, volumeRange: 0.2f);

        var first = variation.Resolve(new Random(555));
        var second = variation.Resolve(new Random(555));

        Assert.Equal(first.Pitch, second.Pitch);
        Assert.Equal(first.VolumeScale, second.VolumeScale);
    }

    [Fact]
    public void Negative_ranges_are_normalized_to_zero()
    {
        var variation = new CueVariation(pitchRange: -1f, volumeRange: -1f);
        var play = variation.Resolve(new Random(1));

        Assert.Equal(0f, play.Pitch);
        Assert.Equal(1f, play.VolumeScale);
    }
}
