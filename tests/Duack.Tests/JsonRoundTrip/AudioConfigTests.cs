using Duack;

namespace Duack.Tests.JsonRoundTrip;

public class AudioConfigTests
{
    private const string Json = """
        {
          "music": {
            "master_volume": 0.8,
            "crossfade_seconds": 2.0,
            "contexts": { "Overworld": 0.9, "Hospital": 0.7 },
            "duck_sources": {
              "sfx": { "ratio": 0.3, "attack_seconds": 0.06, "hold_seconds": 0.2, "release_seconds": 0.35 }
            }
          },
          "sfx": { "master_volume": 0.75 }
        }
        """;

    [Fact]
    public void Parse_reads_snake_case_keys()
    {
        var config = AudioConfigJson.Parse(Json);

        Assert.Equal(0.8f, config.Music.MasterVolume);
        Assert.Equal(2.0f, config.Music.CrossfadeSeconds);
        Assert.Equal(0.75f, config.Sfx.MasterVolume);
    }

    [Fact]
    public void Parse_reads_contexts_by_enum_name()
    {
        var config = AudioConfigJson.Parse(Json);

        Assert.Equal(0.9f, config.Music.Contexts["Overworld"]);
        Assert.Equal(0.7f, config.Music.Contexts["Hospital"]);
    }

    [Fact]
    public void Parse_reads_duck_source_parameters()
    {
        var config = AudioConfigJson.Parse(Json);

        var sfx = config.Music.DuckSources["sfx"];
        Assert.Equal(0.3f, sfx.Ratio);
        Assert.Equal(0.06f, sfx.AttackSeconds);
        Assert.Equal(0.2f, sfx.HoldSeconds);
        Assert.Equal(0.35f, sfx.ReleaseSeconds);
    }

    [Fact]
    public void Parse_reads_cue_variation_for_music_and_sfx()
    {
        var config = AudioConfigJson.Parse("""
            {
              "music": { "cue_variation": { "pitch_range": 0.1, "volume_range": 0.2 } },
              "sfx":   { "cue_variation": { "pitch_range": 0.05, "volume_range": 0.15 } }
            }
            """);

        Assert.NotNull(config.Music.CueVariation);
        Assert.Equal(0.1f, config.Music.CueVariation!.Value.PitchRange);
        Assert.Equal(0.2f, config.Music.CueVariation!.Value.VolumeRange);

        Assert.NotNull(config.Sfx.CueVariation);
        Assert.Equal(0.05f, config.Sfx.CueVariation!.Value.PitchRange);
        Assert.Equal(0.15f, config.Sfx.CueVariation!.Value.VolumeRange);
    }

    [Fact]
    public void Cue_variation_round_trips_across_serialize()
    {
        var config = AudioConfigJson.Parse("""
            {
              "music": { "cue_variation": { "pitch_range": 0.1, "volume_range": 0.2 } },
              "sfx":   { "cue_variation": { "pitch_range": 0.05, "volume_range": 0.15 } }
            }
            """);

        var reparsed = AudioConfigJson.Parse(AudioConfigJson.Serialize(config));

        Assert.Equal(0.1f, reparsed.Music.CueVariation!.Value.PitchRange);
        Assert.Equal(0.15f, reparsed.Sfx.CueVariation!.Value.VolumeRange);
    }

    [Fact]
    public void Cue_variation_is_omitted_when_unset()
    {
        var json = AudioConfigJson.Serialize(new AudioConfig());

        Assert.DoesNotContain("cue_variation", json);
    }

    [Fact]
    public void Sanitize_normalizes_negative_cue_variation_ranges()
    {
        var config = AudioConfigJson.Parse("""
            {
              "music": { "cue_variation": { "pitch_range": -0.5, "volume_range": -0.5 } },
              "sfx":   { "cue_variation": { "pitch_range": -1, "volume_range": -1 } }
            }
            """);

        config.Sanitize();

        Assert.Equal(0f, config.Music.CueVariation!.Value.PitchRange);
        Assert.Equal(0f, config.Music.CueVariation!.Value.VolumeRange);
        Assert.Equal(0f, config.Sfx.CueVariation!.Value.PitchRange);
        Assert.Equal(0f, config.Sfx.CueVariation!.Value.VolumeRange);
    }

    [Fact]
    public void Parse_ignores_the_build_only_tracks_section()
    {
        var config = AudioConfigJson.Parse("""
            {
              "tracks": {
                "overworld.wav": { "loudnorm": { "i": -16.0 }, "compress": { "ratio": 3.0 } }
              }
            }
            """);

        Assert.Equal(1f, config.Music.MasterVolume); // defaults untouched
        Assert.Empty(config.Music.Contexts);
        Assert.Empty(config.Music.DuckSources);
    }

    [Fact]
    public void Parse_returns_defaults_on_empty_json()
    {
        var config = AudioConfigJson.Parse("{}");

        Assert.Equal(1f, config.Music.MasterVolume);
        Assert.Equal(1.2f, config.Music.CrossfadeSeconds);
        Assert.Equal(1f, config.Sfx.MasterVolume);
    }

    [Fact]
    public void Parse_is_case_insensitive_for_keys()
    {
        var config = AudioConfigJson.Parse("""
            { "MUSIC": { "Master_Volume": 0.5 } }
            """);

        Assert.Equal(0.5f, config.Music.MasterVolume);
    }

    [Fact]
    public void Serialize_round_trips_known_values()
    {
        var config = AudioConfigJson.Parse(Json);

        var reparsed = AudioConfigJson.Parse(AudioConfigJson.Serialize(config));

        Assert.Equal(0.8f, reparsed.Music.MasterVolume);
        Assert.Equal(2.0f, reparsed.Music.CrossfadeSeconds);
        Assert.Equal(0.75f, reparsed.Sfx.MasterVolume);
        Assert.Equal(0.9f, reparsed.Music.Contexts["Overworld"]);
        Assert.Equal(0.3f, reparsed.Music.DuckSources["sfx"].Ratio);
    }

    [Fact]
    public void Serialize_preserves_build_only_sections_across_a_round_trip()
    {
        var withTracks = AudioConfigJson.Parse("""
            {
              "music": { "master_volume": 0.5 },
              "tracks": {
                "overworld.wav": { "loudnorm": { "i": -16.0 }, "compress": { "ratio": 3.0 } }
              }
            }
            """);

        var reparsed = AudioConfigJson.Parse(AudioConfigJson.Serialize(withTracks));

        Assert.Equal(0.5f, reparsed.Music.MasterVolume);
        Assert.NotNull(reparsed.Tracks);
        Assert.True(reparsed.Tracks.ContainsKey("overworld.wav"));
        Assert.Equal(-16.0, reparsed.Tracks["overworld.wav"].Loudnorm.I);
    }

    [Fact]
    public void Serialize_uses_snake_case_keys()
    {
        var config = new AudioConfig { Music = new MusicConfig { MasterVolume = 0.42f } };

        var json = AudioConfigJson.Serialize(config);

        Assert.Contains("\"master_volume\"", json);
        Assert.DoesNotContain("MasterVolume", json);
    }

    [Fact]
    public void Sanitize_clamps_values_that_would_break_the_mix()
    {
        var config = AudioConfigJson.Parse("""
            {
              "music": {
                "master_volume": 1.5,
                "crossfade_seconds": 0.0,
                "contexts": { "Overworld": 2.0, "Hospital": 0.9 },
                "duck_sources": {
                  "sfx": { "ratio": 1.5, "attack_seconds": -0.1, "hold_seconds": -2, "release_seconds": -0.5 }
                }
              },
              "sfx": { "master_volume": -0.2 }
            }
            """);

        config.Sanitize();

        Assert.Equal(1f, config.Music.MasterVolume);
        Assert.Equal(0.1f, config.Music.CrossfadeSeconds);
        Assert.Equal(1f, config.Music.Contexts["Overworld"]);
        Assert.Equal(0.9f, config.Music.Contexts["Hospital"]);
        Assert.Equal(0f, config.Sfx.MasterVolume);
        Assert.Equal(1f, config.Music.DuckSources["sfx"].Ratio);
        Assert.Equal(0f, config.Music.DuckSources["sfx"].AttackSeconds);
        Assert.Equal(0f, config.Music.DuckSources["sfx"].HoldSeconds);
        Assert.Equal(0f, config.Music.DuckSources["sfx"].ReleaseSeconds);
    }

    [Fact]
    public void Sanitize_leaves_in_range_values_untouched()
    {
        var config = AudioConfigJson.Parse(Json);

        config.Sanitize();

        Assert.Equal(0.8f, config.Music.MasterVolume);
        Assert.Equal(2.0f, config.Music.CrossfadeSeconds);
        Assert.Equal(0.75f, config.Sfx.MasterVolume);
        Assert.Equal(0.3f, config.Music.DuckSources["sfx"].Ratio);
        Assert.Equal(0.06f, config.Music.DuckSources["sfx"].AttackSeconds);
        Assert.Equal(0.2f, config.Music.DuckSources["sfx"].HoldSeconds);
        Assert.Equal(0.35f, config.Music.DuckSources["sfx"].ReleaseSeconds);
    }

    [Fact]
    public void Load_clamps_out_of_range_values_like_TryLoad()
    {
        // Load() is the boot path (AudioMix.Config) while TryLoad() is the hot-reload path. Before
        // this test they disagreed: TryLoad() called Sanitize() and Load() did not, so a hand-edited
        // authored config with a 1.5 music master was honoured at launch and then silently clamped
        // the first time the file was touched and re-read.
        const string bad = """
            {
              "music": {
                "master_volume": 1.5,
                "crossfade_seconds": 0.0,
                "contexts": { "Overworld": 2.0 },
                "duck_sources": {
                  "sfx": { "ratio": 1.5, "attack_seconds": -0.1 }
                }
              },
              "sfx": { "master_volume": -0.2 }
            }
            """;
        var previous = AudioConfigJson.OpenStream;
        try
        {
            AudioConfigJson.OpenStream = _ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(bad));

            var loaded = AudioConfigJson.Load();

            Assert.Equal(1f, loaded.Music.MasterVolume);
            Assert.Equal(0.1f, loaded.Music.CrossfadeSeconds);
            Assert.Equal(1f, loaded.Music.Contexts["Overworld"]);
            Assert.Equal(0f, loaded.Sfx.MasterVolume);
            Assert.Equal(1f, loaded.Music.DuckSources["sfx"].Ratio);
            Assert.Equal(0f, loaded.Music.DuckSources["sfx"].AttackSeconds);
        }
        finally
        {
            AudioConfigJson.OpenStream = previous;
        }
    }

    [Fact]
    public void Save_writes_the_serialized_config_to_disk()
    {
        var config = AudioConfigJson.Parse(Json);
        var path = Path.Combine(Path.GetTempPath(), $"audio-config-test-{Guid.NewGuid():N}.json");
        try
        {
            AudioConfigJson.Save(config, path);

            var reparsed = AudioConfigJson.Parse(File.ReadAllText(path));
            Assert.Equal(0.8f, reparsed.Music.MasterVolume);
            Assert.Equal(0.75f, reparsed.Sfx.MasterVolume);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
