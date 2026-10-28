using Duack;

namespace Duack.Tests.JsonRoundTrip;

/// <summary>
/// Pins the playlist wire format: a code-built <see cref="PlaylistConfig"/> serializes to snake_case
/// JSON and parses back.
/// </summary>
public class PlaylistJsonTests
{
    private static PlaylistConfig CodeBuilt() =>
        new PlaylistConfigBuilder()
            .WithPlaylist("overworld", p => p
                .Track("theme_a", "Overworld")
                .Track("theme_b", "Overworld")
                .Repeat(PlaylistRepeatMode.Playlist)
                .Shuffle(PlaylistShuffleMode.Bag))
            .WithPlaylist("battle", p => p.Track("battle_loop", "Battle"))
            .Build();

    [Fact]
    public void Serialize_then_Parse_round_trips_a_code_built_config()
    {
        var reparsed = PlaylistJson.Parse(PlaylistJson.Serialize(CodeBuilt()));

        Assert.True(reparsed.TryGet("overworld", out var overworld));
        Assert.Equal(2, overworld.Tracks.Count);
        Assert.Equal("theme_a", overworld.Tracks[0].Name);
        Assert.Equal("Overworld", overworld.Tracks[0].Context);
        Assert.Equal(PlaylistRepeatMode.Playlist, overworld.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, overworld.Shuffle);

        Assert.True(reparsed.TryGet("battle", out var battle));
        Assert.Equal(PlaylistRepeatMode.Off, battle.Repeat);
        Assert.Equal("battle_loop", battle.Tracks[0].Name);
    }

    [Fact]
    public void Serialize_uses_snake_case_keys()
    {
        var json = PlaylistJson.Serialize(CodeBuilt());

        Assert.Contains("\"playlists\"", json);
        Assert.Contains("\"tracks\"", json);
        Assert.Contains("\"repeat\"", json);
        Assert.Contains("\"shuffle\"", json);
        Assert.DoesNotContain("Playlists", json);
        Assert.DoesNotContain("Tracks", json);
    }

    [Fact]
    public void Parse_reads_lowercase_enum_names()
    {
        var playlists = PlaylistJson.Parse("""
            { "playlists": { "overworld": { "tracks": [], "repeat": "playlist", "shuffle": "bag" } } }
            """);

        Assert.True(playlists.TryGet("overworld", out var definition));
        Assert.Equal(PlaylistRepeatMode.Playlist, definition.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, definition.Shuffle);
    }

    [Fact]
    public void Parse_reads_numeric_enum_values()
    {
        var playlists = PlaylistJson.Parse("""
            { "playlists": { "overworld": { "tracks": [], "repeat": 1, "shuffle": 1 } } }
            """);

        Assert.True(playlists.TryGet("overworld", out var definition));
        Assert.Equal(PlaylistRepeatMode.Playlist, definition.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, definition.Shuffle);
    }

    [Fact]
    public void Parse_accepts_comments_and_trailing_commas()
    {
        var playlists = PlaylistJson.Parse("""
            {
              // hand-edited files are allowed to be chatty
              "playlists": {
                "overworld": { "tracks": [ { "name": "theme_a", "context": "Overworld", }, ], },
              },
            }
            """);

        Assert.True(playlists.TryGet("overworld", out _));
    }

    [Fact]
    public void TryParse_reports_false_on_malformed_json()
    {
        Assert.False(PlaylistJson.TryParse("{ not json", out var playlists));
        Assert.Null(playlists);
    }

}
