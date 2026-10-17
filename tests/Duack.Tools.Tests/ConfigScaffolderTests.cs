namespace Duack.Tools.Tests;

/// <summary>
/// Pins the <c>duack init</c> scaffold: one <c>tracks</c> entry per discovered WAV (normalized,
/// sorted), the default music/sfx sections with the sfx/dialogue duck sources, and a scaffold that
/// validates clean so it can be edited from a working starting point.
/// </summary>
public class ConfigScaffolderTests
{
    [Fact]
    public void Creates_one_track_per_wav_with_the_extension_stripped()
    {
        var config = ConfigScaffolder.Build(new[] { "theme_a.wav", "sfx_hit.WAV" });

        Assert.NotNull(config.Tracks);
        Assert.True(config.Tracks!.ContainsKey("theme_a"));
        Assert.True(config.Tracks.ContainsKey("sfx_hit"));
    }

    [Fact]
    public void Track_keys_are_sorted_and_deduplicated()
    {
        var config = ConfigScaffolder.Build(new[] { "zeta.wav", "alpha.wav", "zeta.wav" });

        Assert.Equal(new[] { "alpha", "zeta" }, config.Tracks!.Keys);
    }

    [Fact]
    public void Includes_the_sfx_and_dialogue_duck_sources()
    {
        var config = ConfigScaffolder.Build(Array.Empty<string>());

        Assert.True(config.Music.DuckSources.ContainsKey("sfx"));
        Assert.True(config.Music.DuckSources.ContainsKey("dialogue"));
    }

    [Fact]
    public void No_wavs_omits_the_tracks_section()
    {
        var config = ConfigScaffolder.Build(Array.Empty<string>());

        Assert.Null(config.Tracks);
    }

    [Fact]
    public void The_scaffold_validates_clean()
    {
        var config = ConfigScaffolder.Build(new[] { "theme_a.wav" });
        Assert.Empty(ConfigValidator.Validate(config));
    }
}
