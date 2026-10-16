using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the slider-to-amplitude mapping. A linear slider feeding a linear amplitude sounds wrong
/// (the top half of the travel is barely perceptible), so the slider is mapped through a
/// perceptual curve. <see cref="VolumeCurve.ToSlider"/> is the inverse so a saved amplitude can be
/// shown back on the slider without drift.
/// </summary>
public class VolumeCurveTests
{
    [Fact]
    public void ToAmplitude_maps_the_endpoints_exactly()
    {
        Assert.Equal(0f, VolumeCurve.ToAmplitude(0f));
        Assert.Equal(1f, VolumeCurve.ToAmplitude(1f));
    }

    [Fact]
    public void ToAmplitude_is_below_linear_in_the_middle()
    {
        // The perceptual curve makes the midpoint quieter than a linear slider would.
        Assert.Equal(0.25f, VolumeCurve.ToAmplitude(0.5f), 4);
        Assert.True(VolumeCurve.ToAmplitude(0.5f) < 0.5f);
    }

    [Fact]
    public void ToAmplitude_clamps_out_of_range_input()
    {
        Assert.Equal(0f, VolumeCurve.ToAmplitude(-3f));
        Assert.Equal(1f, VolumeCurve.ToAmplitude(9f));
    }

    [Fact]
    public void ToAmplitude_treats_non_finite_input_as_silence_except_positive_infinity()
    {
        Assert.Equal(0f, VolumeCurve.ToAmplitude(float.NaN));
        Assert.Equal(1f, VolumeCurve.ToAmplitude(float.PositiveInfinity));
    }

    [Fact]
    public void ToSlider_is_the_inverse_of_ToAmplitude()
    {
        foreach (var slider in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f })
            Assert.Equal(slider, VolumeCurve.ToSlider(VolumeCurve.ToAmplitude(slider)), 4);
    }

    [Fact]
    public void ToSlider_clamps_out_of_range_input()
    {
        Assert.Equal(0f, VolumeCurve.ToSlider(-2f));
        Assert.Equal(1f, VolumeCurve.ToSlider(4f));
    }

    [Fact]
    public void ToSlider_treats_non_finite_input_as_silence()
    {
        Assert.Equal(0f, VolumeCurve.ToSlider(float.NaN));
    }
}
