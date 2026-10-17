using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the fluent <see cref="PlaylistConfigBuilder"/>: a terse way to assemble a
/// <see cref="PlaylistConfig"/> in code so a game can define its playlists in C# instead of JSON.
/// </summary>
public class PlaylistConfigBuilderTests
{
    [Fact]
    public void Builds_a_named_playlist_with_ordered_tracks()
    {
        var config = new PlaylistConfigBuilder()
            .WithPlaylist("overworld", p => p
                .Track("theme_a", "Overworld")
                .Track("theme_b", "Overworld"))
            .Build();

        Assert.True(config.TryGet("overworld", out var definition));
        Assert.Equal(2, definition.Tracks.Count);
        Assert.Equal("theme_a", definition.Tracks[0].Name);
        Assert.Equal("theme_b", definition.Tracks[1].Name);
        Assert.Equal("Overworld", definition.Tracks[0].Context);
    }

    [Fact]
    public void Defaults_repeat_and_shuffle_to_off()
    {
        var config = new PlaylistConfigBuilder()
            .WithPlaylist("x", p => p.Track("a", "Overworld"))
            .Build();

        Assert.True(config.TryGet("x", out var definition));
        Assert.Equal(PlaylistRepeatMode.Off, definition.Repeat);
        Assert.Equal(PlaylistShuffleMode.Off, definition.Shuffle);
    }

    [Fact]
    public void Sets_repeat_and_shuffle_modes()
    {
        var config = new PlaylistConfigBuilder()
            .WithPlaylist("x", p => p
                .Track("a", "Overworld")
                .Repeat(PlaylistRepeatMode.Track)
                .Shuffle(PlaylistShuffleMode.Bag))
            .Build();

        Assert.True(config.TryGet("x", out var definition));
        Assert.Equal(PlaylistRepeatMode.Track, definition.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, definition.Shuffle);
    }

    [Fact]
    public void WithPlaylist_can_be_called_without_a_configuration()
    {
        var config = new PlaylistConfigBuilder().WithPlaylist("empty").Build();

        Assert.True(config.TryGet("empty", out var definition));
        Assert.Empty(definition.Tracks);
    }

    [Fact]
    public void Later_writes_to_the_same_playlist_win()
    {
        var config = new PlaylistConfigBuilder()
            .WithPlaylist("x", p => p.Track("a", "Overworld"))
            .WithPlaylist("x", p => p.Track("b", "Overworld"))
            .Build();

        Assert.True(config.TryGet("x", out var definition));
        Assert.Single(definition.Tracks);
        Assert.Equal("b", definition.Tracks[0].Name);
    }

    [Fact]
    public void Chained_calls_return_the_same_builder_instance()
    {
        var builder = new PlaylistConfigBuilder();

        Assert.Same(builder, builder.WithPlaylist("x"));
    }
}
