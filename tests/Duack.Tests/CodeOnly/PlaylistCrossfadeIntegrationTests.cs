using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the contract the adapters build on: a playlist advance is realized with the existing
/// crossfade curve, and ducking (a concurrent active duck source) integrates with the fade gain
/// through <see cref="MusicVolumeMath.Effective"/> mid-transition. The core owns the numbers; the
/// adapter hands them to the engine.
/// </summary>
public class PlaylistCrossfadeIntegrationTests
{
    [Fact]
    public void The_crossfade_curve_reports_a_partial_gain_mid_transition()
    {
        var sequencer = new MusicTransitionSequencer();
        sequencer.BeginFadeIn();

        sequencer.Advance(0.6f, 1.2f);

        Assert.True(sequencer.InGain > 0f);
        Assert.True(sequencer.InGain < 1f);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(1.0f)]
    public void Ducking_stays_audible_and_in_range_across_a_fade(float progress)
    {
        const float duckRatio = 0.25f;
        var fadeGain = CrossfadeMath.EqualPower(progress).In;

        var effective = MusicVolumeMath.Effective(1f, 1f, duckRatio, fadeGain);

        Assert.InRange(effective, 0f, 1f);
        Assert.True(effective <= fadeGain + 1e-6f);
        if (fadeGain > 0f)
            Assert.True(effective > 0f);
    }

    [Fact]
    public void At_full_fade_the_only_attenuation_left_is_the_duck_source()
    {
        var fadeGain = CrossfadeMath.EqualPower(1f).In;

        var effective = MusicVolumeMath.Effective(1f, 1f, 0.25f, fadeGain);

        Assert.Equal(0.25f, effective, 3);
    }
}
