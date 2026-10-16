using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the fluent <see cref="AudioConfigBuilder"/>: a terse way to assemble an <see cref="AudioConfig"/>
/// in code (tests, samples, first-run defaults) with the same clamping the file path applies.
/// </summary>
public class AudioConfigBuilderTests
{
    [Fact]
    public void Defaults_match_a_plain_sanitized_config()
    {
        var built = new AudioConfigBuilder().Build();
        var expected = new AudioConfig();
        expected.Sanitize();

        Assert.Equal(expected.MasterVolume, built.MasterVolume);
        Assert.Equal(expected.Music.MasterVolume, built.Music.MasterVolume);
        Assert.Equal(expected.Sfx.MasterVolume, built.Sfx.MasterVolume);
        Assert.Equal(expected.Music.CrossfadeSeconds, built.Music.CrossfadeSeconds);
    }

    [Fact]
    public void Every_setter_lands_in_the_right_destination()
    {
        var built = new AudioConfigBuilder()
            .WithMasterVolume(0.5f)
            .WithMusicMasterVolume(0.6f)
            .WithSfxMasterVolume(0.7f)
            .WithCrossfadeSeconds(2.5f)
            .WithContext("Overworld", 0.4f)
            .WithContext("Battle", 0.9f)
            .WithDuckSource("sfx", new DuckSourceConfig { Ratio = 0.2f })
            .Build();

        Assert.Equal(0.5f, built.MasterVolume);
        Assert.Equal(0.6f, built.Music.MasterVolume);
        Assert.Equal(0.7f, built.Sfx.MasterVolume);
        Assert.Equal(2.5f, built.Music.CrossfadeSeconds);
        Assert.Equal(0.4f, built.Music.Contexts["Overworld"]);
        Assert.Equal(0.9f, built.Music.Contexts["Battle"]);
        Assert.Equal(0.2f, built.Music.DuckSources["sfx"].Ratio);
    }

    [Fact]
    public void Build_applies_sanitize()
    {
        var built = new AudioConfigBuilder()
            .WithMasterVolume(2f)
            .WithCrossfadeSeconds(900f)
            .WithContext("Loud", 5f)
            .Build();

        Assert.Equal(1f, built.MasterVolume);
        Assert.Equal(10f, built.Music.CrossfadeSeconds);
        Assert.Equal(1f, built.Music.Contexts["Loud"]);
    }

    [Fact]
    public void Chained_calls_return_the_same_builder_instance()
    {
        var builder = new AudioConfigBuilder();

        Assert.Same(builder, builder.WithMasterVolume(0.5f));
        Assert.Same(builder, builder.WithContext("X", 0.5f));
    }

    [Fact]
    public void Later_writes_to_the_same_context_win()
    {
        var built = new AudioConfigBuilder()
            .WithContext("Overworld", 0.2f)
            .WithContext("Overworld", 0.8f)
            .Build();

        Assert.Equal(0.8f, built.Music.Contexts["Overworld"]);
    }
}
