using System;
using System.Collections.Generic;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the normative slice-B spatial math (SHARED §12): the constant-power pan split, the
/// saturated pan curve p = psat·tanh(x_norm / sigma), and the three distance models with their
/// clamp distance and the exact mute floor g = 0 at d &ge; dmax.
/// </summary>
public class SpatialMathTests
{
    private const float Epsilon = 1e-4f;

    // --- Constant-power pan law -----------------------------------------

    [Fact]
    public void ConstantPower_centered_pan_splits_evenly()
    {
        var (left, right) = SpatialMath.ConstantPower(0f);
        Assert.Equal(0.7071f, left, 4);
        Assert.Equal(0.7071f, right, 4);
    }

    [Fact]
    public void ConstantPower_hard_pan_is_silent_on_the_far_channel()
    {
        var (left, right) = SpatialMath.ConstantPower(1f);
        Assert.Equal(0f, left, 4);
        Assert.Equal(1f, right, 4);

        (left, right) = SpatialMath.ConstantPower(-1f);
        Assert.Equal(1f, left, 4);
        Assert.Equal(0f, right, 4);
    }

    [Fact]
    public void ConstantPower_power_is_conserved_for_every_pan()
    {
        for (var p = -1f; p <= 1.001f; p += 0.125f)
        {
            var (left, right) = SpatialMath.ConstantPower(p);
            Assert.Equal(1f, left * left + right * right, 4);
        }
    }

    // --- Saturation pan curve -------------------------------------------

    [Fact]
    public void Pan_centered_source_is_centered()
    {
        Assert.Equal(0f, SpatialMath.Pan(0f, panDistance: 12f, panSaturation: 0.7f, panSigma: 0.5f), Epsilon);
    }

    [Fact]
    public void Pan_is_monotone_in_the_offset()
    {
        float previous = float.NegativeInfinity;
        for (var x = -24f; x <= 24.001f; x += 0.5f)
        {
            var p = SpatialMath.Pan(x, 12f, 0.7f, 0.5f);
            Assert.True(p >= previous - Epsilon, $"pan decreased at x={x}");
            previous = p;
        }
    }

    [Fact]
    public void Pan_saturates_at_panSaturation_and_never_exceeds_it()
    {
        Assert.Equal(0.7f, SpatialMath.Pan(1e6f, 12f, 0.7f, 0.5f), 3);
        Assert.Equal(-0.7f, SpatialMath.Pan(-1e6f, 12f, 0.7f, 0.5f), 3);
    }

    [Fact]
    public void Pan_matches_the_normative_formula_at_sample_offsets()
    {
        // p = psat * tanh((x / panDistance) / sigma), pinned exactly.
        foreach (var x in new[] { -24f, -6f, -1.5f, 0f, 1.5f, 6f, 24f })
        {
            var expected = 0.7f * MathF.Tanh(x / 12f / 0.5f);
            Assert.Equal(expected, SpatialMath.Pan(x, 12f, 0.7f, 0.5f), 4);
        }
    }

    // --- Distance models -------------------------------------------------

    [Theory]
    [InlineData(DistanceModel.Linear)]
    [InlineData(DistanceModel.Inverse)]
    [InlineData(DistanceModel.Exponential)]
    public void DistanceGain_is_one_at_or_inside_the_reference_distance(DistanceModel model)
    {
        Assert.Equal(1f, SpatialMath.DistanceGain(0f, 1f, 32f, model, rolloff: 1f), Epsilon);
        Assert.Equal(1f, SpatialMath.DistanceGain(1f, 1f, 32f, model, rolloff: 1f), Epsilon);
    }

    [Theory]
    [InlineData(DistanceModel.Linear)]
    [InlineData(DistanceModel.Inverse)]
    [InlineData(DistanceModel.Exponential)]
    public void DistanceGain_mute_floor_is_exactly_zero_at_and_beyond_the_max_distance(DistanceModel model)
    {
        Assert.Equal(0f, SpatialMath.DistanceGain(32f, 1f, 32f, model, rolloff: 1f), Epsilon);
        Assert.Equal(0f, SpatialMath.DistanceGain(64f, 1f, 32f, model, rolloff: 1f), Epsilon);
    }

    [Theory]
    [InlineData(DistanceModel.Linear)]
    [InlineData(DistanceModel.Inverse)]
    [InlineData(DistanceModel.Exponential)]
    public void DistanceGain_is_monotone_non_increasing_and_never_negative(DistanceModel model)
    {
        var previous = 1f;
        for (var d = 0f; d <= 40f; d += 0.5f)
        {
            var g = SpatialMath.DistanceGain(d, 1f, 32f, model, rolloff: 1f);
            Assert.True(g <= previous + Epsilon, $"{model} gain increased at d={d}");
            Assert.True(g >= 0f, $"{model} gain went negative at d={d}");
            previous = g;
        }
    }

    [Fact]
    public void DistanceGain_inverse_model_matches_the_free_field_formula()
    {
        // g = dref / (dref + rolloff * (d - dref))
        Assert.Equal(2f / (2f + 1f * (8f - 2f)), SpatialMath.DistanceGain(8f, 2f, 32f, DistanceModel.Inverse, 1f), 4);
    }

    [Fact]
    public void DistanceGain_linear_model_matches_the_linear_formula()
    {
        // g = 1 - rolloff * (d - dref) / (dmax - dref)
        Assert.Equal(1f - 1f * (16f - 2f) / (32f - 2f), SpatialMath.DistanceGain(16f, 2f, 32f, DistanceModel.Linear, 1f), 4);
    }

    [Fact]
    public void DistanceGain_exponential_model_matches_the_exponential_formula()
    {
        // g = (dref / d) ^ rolloff
        Assert.Equal(MathF.Pow(2f / 8f, 2f), SpatialMath.DistanceGain(8f, 2f, 32f, DistanceModel.Exponential, 2f), 4);
    }

    [Fact]
    public void DistanceGain_zero_rolloff_holds_full_gain_until_the_max_distance()
    {
        Assert.Equal(1f, SpatialMath.DistanceGain(20f, 1f, 32f, DistanceModel.Inverse, 0f), Epsilon);
        Assert.Equal(0f, SpatialMath.DistanceGain(32f, 1f, 32f, DistanceModel.Inverse, 0f), Epsilon);
    }
}
