using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

public class GainTests
{
    [Fact]
    public void ToAmplitude_and_ToGain_map_the_endpoints_exactly()
    {
        Assert.Equal(0f, Gain.ToAmplitude(0f));
        Assert.Equal(1f, Gain.ToAmplitude(1f));
        Assert.Equal(0f, Gain.ToGain(0f));
        Assert.Equal(1f, Gain.ToGain(1f));
        Assert.Equal(0f, Gain.FromSlider(0f));
        Assert.Equal(1f, Gain.FromSlider(1f));
    }

    [Fact]
    public void ToGain_is_below_linear_in_the_middle()
    {
        Assert.Equal(0.25f, Gain.ToGain(0.5f), 4);
        Assert.True(Gain.ToGain(0.5f) < 0.5f);
    }

    [Fact]
    public void ToSlider_is_the_inverse_of_ToGain()
    {
        foreach (var slider in new[] { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f })
            Assert.Equal(slider, Gain.ToSlider(Gain.ToGain(slider)), 4);
    }

    [Fact]
    public void VolumeCurve_ToGain_matches_Gain_ToGain()
    {
        foreach (var slider in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            Assert.Equal(Gain.ToGain(slider), VolumeCurve.ToGain(slider));
            Assert.Equal(Gain.ToAmplitude(slider), VolumeCurve.ToAmplitude(slider));
        }
    }
}
