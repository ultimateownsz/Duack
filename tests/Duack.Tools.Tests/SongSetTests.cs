namespace Duack.Tools.Tests;

/// <summary>
/// Pins how the converter decides which files are "songs": the union of every playlist's track
/// names, matched case- and extension-insensitively so "Theme_A.ogg" and "theme_a.wav" agree.
/// </summary>
public class SongSetTests
{
    private static PlaylistConfig Playlists() => new()
    {
        Playlists =
        {
            ["overworld"] = new PlaylistDefinition
            {
                Tracks =
                {
                    new PlaylistTrack { Name = "theme_a", Context = "Overworld" },
                    new PlaylistTrack { Name = "theme_b.ogg", Context = "Overworld" },
                },
            },
            ["battle"] = new PlaylistDefinition
            {
                Tracks = { new PlaylistTrack { Name = "boss" } },
            },
        },
    };

    [Fact]
    public void Collects_the_union_of_all_playlist_tracks()
    {
        var songs = SongSet.FromPlaylists(Playlists());

        Assert.True(songs.Contains("theme_a"));
        Assert.True(songs.Contains("theme_b"));
        Assert.True(songs.Contains("boss"));
        Assert.False(songs.Contains("unrelated"));
    }

    [Theory]
    [InlineData("theme_a.wav")]
    [InlineData("THEME_A.WAV")]
    [InlineData("theme_a")]
    [InlineData("Theme_A.ogg")]
    public void Matches_case_and_extension_insensitively(string candidate)
    {
        var songs = SongSet.FromPlaylists(Playlists());
        Assert.True(songs.Contains(candidate));
    }

    [Fact]
    public void An_empty_playlist_config_yields_an_empty_set()
    {
        var songs = SongSet.FromPlaylists(new PlaylistConfig());
        Assert.False(songs.Contains("theme_a"));
    }
}
