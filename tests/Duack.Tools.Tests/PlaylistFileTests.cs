namespace Duack.Tools.Tests;

/// <summary>
/// Pins how a <see cref="PlaylistConfig"/> is read: a standalone JSON file (snake_case, string or
/// numeric enums, comments/trailing commas allowed) and a <c>playlists</c> section embedded in an
/// audio-config.json (captured by its <c>ExtensionData</c>).
/// </summary>
public class PlaylistFileTests
{
    [Fact]
    public void Parses_a_standalone_playlist_file()
    {
        const string json = """
            {
              "playlists": {
                "overworld": {
                  "tracks": [ { "name": "theme_a", "context": "Overworld" } ],
                  "repeat": "playlist",
                  "shuffle": "bag"
                }
              }
            }
            """;

        var playlists = PlaylistFile.Parse(json);
        Assert.True(playlists.TryGet("overworld", out var overworld));
        Assert.Equal("theme_a", overworld.Tracks[0].Name);
        Assert.Equal(PlaylistRepeatMode.Playlist, overworld.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, overworld.Shuffle);
    }

    [Fact]
    public void Accepts_comments_and_trailing_commas()
    {
        const string json = """
            {
              // hand-edited files are allowed to be chatty
              "playlists": {
                "overworld": { "tracks": [ { "name": "theme_a", } ], },
              },
            }
            """;

        var playlists = PlaylistFile.Parse(json);
        Assert.True(playlists.TryGet("overworld", out _));
    }

    [Fact]
    public void Reads_playlists_embedded_in_an_audio_config()
    {
        const string json = """
            {
              "master_volume": 1.0,
              "playlists": {
                "overworld": { "tracks": [ { "name": "theme_a", "context": "Overworld" } ] }
              }
            }
            """;

        var config = AudioConfigJson.Parse(json);
        var playlists = PlaylistFile.FromConfig(config);

        Assert.NotNull(playlists);
        Assert.True(playlists!.TryGet("overworld", out _));
    }

    [Fact]
    public void An_audio_config_without_playlists_yields_null()
    {
        var config = AudioConfigJson.Parse("""{ "master_volume": 1.0 }""");
        Assert.Null(PlaylistFile.FromConfig(config));
    }
}
