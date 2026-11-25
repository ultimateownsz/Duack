using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the authored spatial settings: the settled defaults, the sanitization clamps the
/// config/builder path applies, and the degenerate-config guarantees (a collapsed distance
/// band or a broken field can never panic the mix — it degrades to a centered, muffled cue).
/// </summary>
public class SpatialConfigTests
{
    [Fact]
    public void Defaults_are_the_documented_starting_mix()
    {
        var config = new SpatialConfig();
        Assert.Equal(1f, config.ReferenceDistance);
        Assert.Equal(32f, config.MaxDistance);
        Assert.Equal(1f, config.Rolloff);
        Assert.Equal(12f, config.PanDistance);
        Assert.Equal(0.7f, config.PanSaturation);
        Assert.Equal(DistanceModel.Inverse, config.Model);
    }

    [Fact]
    public void Sanitize_clamps_field_values_into_their_safe_ranges()
    {
        var config = new SpatialConfig
        {
            ReferenceDistance = 0f,
            MaxDistance = 0f,
            Rolloff = -1f,
            PanDistance = -5f,
            PanSaturation = 2f,
        };

        config.Sanitize();

        Assert.Equal(0.01f, config.ReferenceDistance, 4);
        Assert.Equal(0.02f, config.MaxDistance, 4); // one step above the reference, never below
        Assert.Equal(0f, config.Rolloff);
        Assert.Equal(0.01f, config.PanDistance, 4);
        Assert.Equal(1f, config.PanSaturation);
    }

    [Fact]
    public void Sanitize_resets_a_non_finite_field_to_its_default_instead_of_poisoning_the_mix()
    {
        var config = new SpatialConfig
        {
            ReferenceDistance = float.NaN,
            MaxDistance = float.PositiveInfinity,
            Rolloff = float.NegativeInfinity,
            PanDistance = float.NaN,
            PanSaturation = float.NaN,
        };

        config.Sanitize();

        var defaults = new SpatialConfig();
        Assert.Equal(defaults.ReferenceDistance, config.ReferenceDistance);
        Assert.Equal(defaults.MaxDistance, config.MaxDistance);
        Assert.Equal(defaults.Rolloff, config.Rolloff);
        Assert.Equal(defaults.PanDistance, config.PanDistance);
        Assert.Equal(defaults.PanSaturation, config.PanSaturation);
    }

    [Fact]
    public void Sanitize_keeps_a_sane_config_exactly_as_authored()
    {
        var config = new SpatialConfig
        {
            ReferenceDistance = 2f,
            MaxDistance = 20f,
            Rolloff = 1.5f,
            PanDistance = 8f,
            PanSaturation = 0.5f,
            Model = DistanceModel.Linear,
        };

        config.Sanitize();

        Assert.Equal(2f, config.ReferenceDistance);
        Assert.Equal(20f, config.MaxDistance);
        Assert.Equal(1.5f, config.Rolloff);
        Assert.Equal(8f, config.PanDistance);
        Assert.Equal(0.5f, config.PanSaturation);
        Assert.Equal(DistanceModel.Linear, config.Model);
    }

    [Fact]
    public void Builder_WithSpatial_authors_the_spatial_settings_and_Build_sanitizes_them()
    {
        var config = new AudioConfigBuilder()
            .WithSpatial(new SpatialConfig
            {
                ReferenceDistance = 4f,
                MaxDistance = 64f,
                PanDistance = 16f,
            })
            .Build();

        Assert.NotNull(config.Sfx.Spatial);
        Assert.Equal(4f, config.Sfx.Spatial!.ReferenceDistance);
        Assert.Equal(64f, config.Sfx.Spatial.MaxDistance);
        Assert.Equal(16f, config.Sfx.Spatial.PanDistance);
    }

    [Fact]
    public void AudioConfig_without_spatial_settings_sanitizes_to_nothing()
    {
        var config = new AudioConfigBuilder().Build();
        Assert.Null(config.Sfx.Spatial);
    }
}
