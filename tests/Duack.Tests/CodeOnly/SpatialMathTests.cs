using System;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the normative 2D spatial math (SHARED section 12): the constant-power pan law the
/// web <c>StereoPannerNode</c> uses, the saturating pan curve for a listener-space x offset,
/// and the two distance-attenuation models with their exact mute floor at the max distance.
/// </summary>
public class SpatialMathTests
{
    // ---------- ConstantPower ----------

    [Fact]
    public void ConstantPower_is_silent_left_at_full_right_and_symmetric()
    {
        var fullRight = SpatialMath.ConstantPower(1f);
        Assert.Equal(0f, fullRight.Left, 4);
        Assert.Equal(1f, fullRight.Right, 4);

        var center = SpatialMath.ConstantPower(0f);
        Assert.Equal(0.7071f, center.Left, 4);
        Assert.Equal(0.7071f, center.Right, 4);

        var fullLeft = SpatialMath.ConstantPower(-1f);
        Assert.Equal(1f, fullLeft.Left, 4);
        Assert.Equal(0f, fullLeft.Right, 4);
    }

    [Fact]
    public void ConstantPower_preserves_power_for_every_pan()
    {
        for (var i = 0; i <= 20; i++)
        {
            var pan = i / 10f - 1f;
            var (left, right) = SpatialMath.ConstantPower(pan);
            Assert.Equal(1f, left * left + right * right, 4);
        }
    }

    // ---------- Pan ----------

    [Fact]
    public void Pan_is_zero_at_center_and_for_nonfinite_input()
    {
        Assert.Equal(0f, SpatialMath.Pan(0f, panDistance: 12f, panSaturation: 0.7f), 4);
        Assert.Equal(0f, SpatialMath.Pan(float.NaN, 12f, 0.7f));
        Assert.Equal(0f, SpatialMath.Pan(float.PositiveInfinity, 12f, 0.7f));
        Assert.Equal(0f, SpatialMath.Pan(float.NegativeInfinity, 12f, 0.7f));
    }

    [Fact]
    public void Pan_at_the_pan_distance_carries_tanh_of_one_of_the_saturation()
    {
        // p = sat * tanh(x / panDistance): at x = panDistance the tanh argument is 1,
        // so the law does NOT hard-saturate there — it keeps easing toward the ceiling.
        Assert.Equal(0.7f * MathF.Tanh(1f), SpatialMath.Pan(12f, 12f, 0.7f), 4);
        Assert.Equal(-0.7f * MathF.Tanh(1f), SpatialMath.Pan(-12f, 12f, 0.7f), 4);
    }

    [Fact]
    public void Pan_saturates_at_the_saturation_ceiling_and_never_past_it()
    {
        // Far off-centre the curve sits effectively at the authored ceiling, never above it.
        Assert.Equal(0.7f, SpatialMath.Pan(120f, 12f, 0.7f), 3);
        Assert.Equal(-0.7f, SpatialMath.Pan(-120f, 12f, 0.7f), 3);
        Assert.Equal(0.35f, SpatialMath.Pan(240f, 12f, 0.35f), 3);
    }

    [Fact]
    public void Pan_is_monotone_in_the_offset_and_odd_in_the_sign()
    {
        float previous = -1f;
        for (var x = -24f; x <= 24f; x += 0.5f)
        {
            var p = SpatialMath.Pan(x, 12f, 0.7f);
            Assert.True(p >= previous, $"pan regressed at x={x}");
            previous = p;
            Assert.Equal(SpatialMath.Pan(-x, 12f, 0.7f), -p, 5);
        }
    }

    [Fact]
    public void Pan_never_pans_for_a_non_positive_or_non_finite_pan_distance()
    {
        Assert.Equal(0f, SpatialMath.Pan(5f, 0f, 0.7f));
        Assert.Equal(0f, SpatialMath.Pan(5f, -1f, 0.7f));
        Assert.Equal(0f, SpatialMath.Pan(5f, float.NaN, 0.7f));
        // And a non-finite saturation flattens the pan too: a broken config is centered sound.
        Assert.Equal(0f, SpatialMath.Pan(5f, 12f, float.NaN));
    }

    // ---------- DistanceGain ----------

    [Theory]
    [InlineData(DistanceModel.Linear)]
    [InlineData(DistanceModel.Inverse)]
    public void DistanceGain_is_full_at_the_reference_distance_and_muted_at_the_max(
        DistanceModel model)
    {
        Assert.Equal(1f, SpatialMath.DistanceGain(0f, 1f, 32f, model, rolloff: 1f));
        Assert.Equal(1f, SpatialMath.DistanceGain(1f, 1f, 32f, model, rolloff: 1f));
        Assert.Equal(0f, SpatialMath.DistanceGain(32f, 1f, 32f, model, rolloff: 1f));
        Assert.Equal(0f, SpatialMath.DistanceGain(1000f, 1f, 32f, model, rolloff: 1f));
    }

    [Fact]
    public void Linear_gain_hits_one_half_at_the_midpoint_of_the_sweep()
    {
        var mid = SpatialMath.DistanceGain(16.5f, 1f, 32f, DistanceModel.Linear, rolloff: 1f);
        Assert.Equal(0.5f, mid, 4);
    }

    [Fact]
    public void Inverse_gain_is_one_half_at_double_the_reference_distance()
    {
        // g = dref / (dref + rolloff * (d - dref)): the free-field spreading shape.
        Assert.Equal(0.5f, SpatialMath.DistanceGain(2f, 1f, 32f, DistanceModel.Inverse, rolloff: 1f), 4);
        Assert.Equal(1f / 3f, SpatialMath.DistanceGain(3f, 1f, 32f, DistanceModel.Inverse, rolloff: 1f), 4);
    }

    [Theory]
    [InlineData(DistanceModel.Linear)]
    [InlineData(DistanceModel.Inverse)]
    public void DistanceGain_is_monotone_non_increasing_between_the_distances(DistanceModel model)
    {
        float previous = float.MaxValue;
        for (var d = 1f; d <= 32f; d += 0.25f)
        {
            var gain = SpatialMath.DistanceGain(d, 1f, 32f, model, rolloff: 1f);
            Assert.True(gain <= previous, $"gain regressed at d={d}");
            Assert.InRange(gain, 0f, 1f);
            previous = gain;
        }
    }

    [Theory]
    [InlineData(DistanceModel.Linear)]
    [InlineData(DistanceModel.Inverse)]
    public void DistanceGain_holds_full_volume_for_a_zero_rolloff_until_the_mute_floor(
        DistanceModel model)
    {
        Assert.Equal(1f, SpatialMath.DistanceGain(16f, 1f, 32f, model, rolloff: 0f));
        Assert.Equal(1f, SpatialMath.DistanceGain(31.9f, 1f, 32f, model, rolloff: 0f));
        Assert.Equal(0f, SpatialMath.DistanceGain(32f, 1f, 32f, model, rolloff: 0f));
    }

    [Fact]
    public void DistanceGain_treats_a_non_finite_distance_as_sounding_at_the_listener()
    {
        // A NaN position is a caller bug, not a sound at the mute floor: centered and full.
        Assert.Equal(1f, SpatialMath.DistanceGain(float.NaN, 1f, 32f, DistanceModel.Linear, 1f));
        Assert.Equal(1f, SpatialMath.DistanceGain(float.PositiveInfinity, 1f, 32f, DistanceModel.Linear, 1f));
    }

    [Fact]
    public void DistanceGain_handles_a_degenerate_sweep_where_the_distances_collapse()
    {
        // dmax == dref: the band is empty; anything at or past the max is muted.
        Assert.Equal(0f, SpatialMath.DistanceGain(2f, 2f, 2f, DistanceModel.Linear, 1f));
        Assert.Equal(1f, SpatialMath.DistanceGain(1f, 2f, 2f, DistanceModel.Linear, 1f));
    }
}
