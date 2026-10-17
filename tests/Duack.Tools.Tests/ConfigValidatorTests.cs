namespace Duack.Tools.Tests;

/// <summary>
/// Pins the headless validation rules: each out-of-range or unknown field is reported with a clear,
/// path-qualified message and the right severity, and a clean config reports nothing.
/// </summary>
public class ConfigValidatorTests
{
    private static AudioConfig Clean() => new AudioConfigBuilder()
        .WithMasterVolume(0.9f)
        .WithMusicMasterVolume(0.8f)
        .WithCrossfadeSeconds(1.5f)
        .WithContext("Overworld", 1f)
        .WithDuckSource("sfx", new DuckSourceConfig { Ratio = 0.3f, AttackSeconds = 0.06f, ReleaseSeconds = 0.35f })
        .Build();

    [Fact]
    public void A_clean_config_has_no_issues()
    {
        Assert.Empty(ConfigValidator.Validate(Clean()));
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void Master_volume_out_of_range_is_an_error(float value)
    {
        var config = Clean();
        config.MasterVolume = value;

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("master_volume", issue.Message);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Non_finite_master_volume_is_an_error(float value)
    {
        var config = Clean();
        config.Music.MasterVolume = value;

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("music.master_volume", issue.Message);
    }

    [Fact]
    public void Crossfade_below_the_minimum_is_an_error()
    {
        var config = Clean();
        config.Music.CrossfadeSeconds = 0f;

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("crossfade_seconds", issue.Message);
    }

    [Fact]
    public void A_context_out_of_range_names_the_context()
    {
        var config = Clean();
        config.Music.Contexts["Battle"] = 1.5f;

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("Battle", issue.Message);
    }

    [Fact]
    public void A_duck_source_out_of_range_names_the_source_and_field()
    {
        var config = Clean();
        config.Music.DuckSources["dialogue"] = new DuckSourceConfig { Ratio = -0.2f };

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("dialogue", issue.Message);
        Assert.Contains("ratio", issue.Message);
    }

    [Fact]
    public void A_negative_duck_time_is_an_error()
    {
        var config = Clean();
        config.Music.DuckSources["sfx"].AttackSeconds = -1f;

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("attack_seconds", issue.Message);
    }

    [Fact]
    public void A_missing_or_legacy_schema_version_is_a_warning_not_an_error()
    {
        var config = Clean();
        config.SchemaVersion = 0;

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Contains("schema_version", issue.Message);
    }

    [Fact]
    public void A_future_schema_version_is_an_error()
    {
        var config = Clean();
        config.SchemaVersion = AudioConfig.CurrentSchemaVersion + 1;

        var issue = Assert.Single(ConfigValidator.Validate(config));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("schema_version", issue.Message);
    }

    [Fact]
    public void A_playlist_track_with_a_missing_name_is_an_error()
    {
        var playlists = new PlaylistConfig
        {
            Playlists =
            {
                ["overworld"] = new PlaylistDefinition
                {
                    Tracks = { new PlaylistTrack { Name = "  ", Context = "Overworld" } },
                },
            },
        };

        var issue = Assert.Single(ConfigValidator.Validate(Clean(), playlists));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Contains("overworld", issue.Message);
    }

    [Fact]
    public void A_playlist_track_with_an_unknown_context_is_a_warning()
    {
        var playlists = new PlaylistConfig
        {
            Playlists =
            {
                ["overworld"] = new PlaylistDefinition
                {
                    Tracks = { new PlaylistTrack { Name = "theme_a", Context = "Nowhere" } },
                },
            },
        };

        var issue = Assert.Single(ConfigValidator.Validate(Clean(), playlists));
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
        Assert.Contains("Nowhere", issue.Message);
    }

    [Fact]
    public void A_clean_config_with_valid_playlists_has_no_issues()
    {
        var playlists = new PlaylistConfig
        {
            Playlists =
            {
                ["overworld"] = new PlaylistDefinition
                {
                    Tracks = { new PlaylistTrack { Name = "theme_a", Context = "Overworld" } },
                },
            },
        };

        Assert.Empty(ConfigValidator.Validate(Clean(), playlists));
    }
}
