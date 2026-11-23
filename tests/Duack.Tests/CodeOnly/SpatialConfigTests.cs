using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the <see cref="SpatialConfig"/> defaults and clamping, and the
/// <see cref="AudioConfigBuilder.WithSpatial"/> surface.
/// </summary>
public class SpatialConfigTests
{
    [Fact]
    public void Defaults_are_the_linear_spatial_profile()
    {
        var config = new SpatialConfig();
        Assert.Equal(1f, config.ReferenceDistance);
        Assert.Equal(32f, config.MaxDistance);
        Assert.Equal(1f, config.Rolloff);
        Assert.Equal(12f, config.PanDistance);
        Assert.Equal(0.7f, config.PanSaturation);
        Assert.Equal(0.5f, config.PanSigma);
        Assert.Equal(DistanceModel.Linear, config.Model);
    }

    [Fact]
    public void Sanitize_clamps_impossible_values()
    {
        var config = new SpatialConfig
        {
            ReferenceDistance = 0f,
            MaxDistance = 0f,
            PanDistance = -1f,
            PanSaturation = 2f,
            PanSigma = 0f,
            Rolloff = -1f,
        };
        config.Sanitize();

        Assert.True(config.ReferenceDistance > 0f);
        Assert.True(config.MaxDistance > config.ReferenceDistance);
        Assert.True(config.PanDistance > 0f);
        Assert.Equal(1f, config.PanSaturation);
        Assert.True(config.PanSigma > 0f);
        Assert.Equal(0f, config.Rolloff);
    }

    [Fact]
    public void Builder_WithSpatial_carries_the_config_through_sanitization()
    {
        var spatial = new SpatialConfig { MaxDistance = 24f, Model = DistanceModel.Inverse };
        var config = new AudioConfigBuilder().WithSpatial(spatial).Build();

        Assert.Same(spatial, config.Sfx.Spatial);
        Assert.Equal(24f, config.Sfx.Spatial.MaxDistance);
        Assert.Equal(DistanceModel.Inverse, config.Sfx.Spatial.Model);
    }

    [Fact]
    public void Builder_WithSpatial_configures_through_the_callback()
    {
        var config = new AudioConfigBuilder()
            .WithSpatial(s => { s.MaxDistance = 16f; s.PanSaturation = 0.5f; })
            .Build();

        Assert.Equal(16f, config.Sfx.Spatial.MaxDistance);
        Assert.Equal(0.5f, config.Sfx.Spatial.PanSaturation);
    }

    [Fact]
    public void AudioConfig_sanitize_clamps_the_nested_spatial_config()
    {
        var config = new AudioConfig();
        config.Sfx.Spatial = new SpatialConfig { PanSigma = -5f };
        config.Sanitize();

        Assert.True(config.Sfx.Spatial.PanSigma > 0f);
    }
}
