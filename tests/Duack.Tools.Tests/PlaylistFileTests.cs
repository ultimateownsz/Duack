namespace DuackCli.Tests;

/// <summary>
/// Pins how a <see cref="PlaylistConfig"/> is read from a standalone JSON file (snake_case, string
/// or numeric enums, comments/trailing commas allowed).
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

}

