using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the shape of a music crossfade. It replaced a first-order exponential ease on both gains,
/// which was front-loaded (63% of the change in one time constant) and, because the two complementary
/// exponentials sum to 1 in amplitude, produced a ~-3 dB power dip in the middle: the old track
/// seemed to drop out and the new one to arrive afterwards. <see cref="CrossfadeMath.EqualPower"/> is
/// an equal-power (constant-power) fade with an S-curve time remap, so the blend is even across the
/// whole duration and the incoming track eases in from silence.
/// </summary>
public class CrossfadeMathTests
{
    [Fact]
    public void Progress_is_zero_at_start_half_at_middle_and_one_at_end()
    {
        Assert.Equal(0f, CrossfadeMath.Progress(0f, 1.2f), 4);
        Assert.Equal(0.5f, CrossfadeMath.Progress(0.6f, 1.2f), 4);
        Assert.Equal(1f, CrossfadeMath.Progress(1.2f, 1.2f), 4);
    }

    [Fact]
    public void Progress_clamps_past_the_end_and_before_the_start()
    {
        Assert.Equal(1f, CrossfadeMath.Progress(5f, 1.2f), 4);
        Assert.Equal(0f, CrossfadeMath.Progress(-1f, 1.2f), 4);
    }

    [Fact]
    public void Progress_is_complete_for_a_non_positive_or_non_finite_duration()
    {
        Assert.Equal(1f, CrossfadeMath.Progress(0.5f, 0f));
        Assert.Equal(1f, CrossfadeMath.Progress(0.5f, -1f));
        Assert.Equal(1f, CrossfadeMath.Progress(float.NaN, 1.2f));
        Assert.Equal(1f, CrossfadeMath.Progress(0.5f, float.PositiveInfinity));
    }

    [Fact]
    public void SmoothStep_hits_the_endpoints_and_the_midpoint_and_clamps()
    {
        Assert.Equal(0f, CrossfadeMath.SmoothStep(0f), 4);
        Assert.Equal(1f, CrossfadeMath.SmoothStep(1f), 4);
        Assert.Equal(0.5f, CrossfadeMath.SmoothStep(0.5f), 4);
        Assert.Equal(0f, CrossfadeMath.SmoothStep(-2f), 4);
        Assert.Equal(1f, CrossfadeMath.SmoothStep(2f), 4);
    }

    [Fact]
    public void EqualPower_hits_the_endpoints_and_the_midpoint()
    {
        var start = CrossfadeMath.EqualPower(0f);
        Assert.Equal(0f, start.In, 4);
        Assert.Equal(1f, start.Out, 4);

        var end = CrossfadeMath.EqualPower(1f);
        Assert.Equal(1f, end.In, 4);
        Assert.Equal(0f, end.Out, 4);

        var mid = CrossfadeMath.EqualPower(0.5f);
        Assert.Equal(0.7071f, mid.In, 3);
        Assert.Equal(0.7071f, mid.Out, 3);
    }

    [Fact]
    public void EqualPower_keeps_constant_power_across_the_whole_fade()
    {
        for (var i = 0; i <= 100; i++)
        {
            var (inGain, outGain) = CrossfadeMath.EqualPower(i / 100f);
            Assert.Equal(1f, inGain * inGain + outGain * outGain, 3);
        }
    }

    [Fact]
    public void EqualPower_is_monotonic_in_and_out()
    {
        var previousIn = -1f;
        var previousOut = 2f;
        for (var i = 0; i <= 100; i++)
        {
            var (inGain, outGain) = CrossfadeMath.EqualPower(i / 100f);
            Assert.True(inGain >= previousIn);
            Assert.True(outGain <= previousOut);
            previousIn = inGain;
            previousOut = outGain;
        }
    }

    [Fact]
    public void EqualPower_treats_non_finite_progress_as_complete()
    {
        var (inGain, outGain) = CrossfadeMath.EqualPower(float.NaN);
        Assert.Equal(1f, inGain, 4);
        Assert.Equal(0f, outGain, 4);
    }

    [Fact]
    public void EqualPower_does_not_drop_the_outgoing_track_immediately()
    {
        // The regression this replaces: the old exponential dropped the outgoing track to ~0.78 within
        // a tenth of the fade. Equal power keeps it near full while the incoming track rises.
        var (inGain, outGain) = CrossfadeMath.EqualPower(0.1f);
        Assert.True(outGain > 0.95f, $"outGain was {outGain}");
        Assert.True(inGain < outGain);
    }

    [Fact]
    public void EqualPower_never_returns_a_negative_gain()
    {
        // Regression: MathF.Cos(MathF.PI / 2f) is ~-4.4e-8 (float pi/2 overshoots a true quarter
        // turn), so the raw Out gain at the end of a fade was a tiny negative number. Desktop pushed
        // it into SoundEffectInstance.Volume, whose setter throws ArgumentOutOfRangeException for any
        // value < 0, crashing on the completion frame of every crossfade. The gains must stay in [0,1].
        Assert.True(MathF.Cos(MathF.PI / 2f) < 0f, "precondition: float pi/2 overshoots the quarter turn");

        Assert.Equal(0f, CrossfadeMath.EqualPower(1f).Out);
        Assert.Equal(0f, CrossfadeMath.EqualPower(0f).In);

        for (var i = 0; i <= 1000; i++)
        {
            var (inGain, outGain) = CrossfadeMath.EqualPower(i / 1000f);
            Assert.True(inGain >= 0f && inGain <= 1f, $"inGain {inGain} out of range at {i}");
            Assert.True(outGain >= 0f && outGain <= 1f, $"outGain {outGain} out of range at {i}");
        }
    }

    [Fact]
    public void The_S_curve_flattens_the_start_of_the_fade()
    {
        // Raw sin(0.05 * PI/2) is already ~0.08; the smoothstep remap keeps the incoming gain near
        // silent at the same progress, which is the "gradually fades in" half of the request.
        var eased = CrossfadeMath.EqualPower(0.05f).In;
        var raw = MathF.Sin(0.05f * (MathF.PI / 2f));
        Assert.True(eased < raw, $"eased {eased} was not below raw {raw}");
        Assert.True(eased < 0.02f, $"eased was {eased}");
    }
}
