using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the playlist config object graph: named playlists, each an ordered list of tracks carrying
/// a name and a context tag, plus the repeat and shuffle modes.
/// </summary>
public class PlaylistConfigTests
{
    [Fact]
    public void A_new_definition_defaults_to_no_tracks_off_repeat_and_off_shuffle()
    {
        var definition = new PlaylistDefinition();

        Assert.Empty(definition.Tracks);
        Assert.Equal(PlaylistRepeatMode.Off, definition.Repeat);
        Assert.Equal(PlaylistShuffleMode.Off, definition.Shuffle);
    }

    [Fact]
    public void A_new_config_has_no_playlists()
    {
        var config = new PlaylistConfig();

        Assert.Empty(config.Playlists);
    }

    [Fact]
    public void TryGet_finds_a_named_playlist_by_its_exact_name()
    {
        var definition = new PlaylistDefinition();
        var config = new PlaylistConfig { Playlists = { ["overworld"] = definition } };

        Assert.True(config.TryGet("overworld", out var found));
        Assert.Same(definition, found);
    }

    [Fact]
    public void TryGet_reports_false_for_an_unknown_name()
    {
        var config = new PlaylistConfig { Playlists = { ["overworld"] = new PlaylistDefinition() } };

        Assert.False(config.TryGet("battle", out var found));
        Assert.Null(found);
    }

    [Fact]
    public void A_track_carries_a_name_and_a_context_tag()
    {
        var track = new PlaylistTrack { Name = "overworld_a", Context = "overworld" };

        Assert.Equal("overworld_a", track.Name);
        Assert.Equal("overworld", track.Context);
    }
}
