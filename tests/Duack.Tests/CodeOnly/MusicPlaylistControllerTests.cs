using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the backend-agnostic playlist sequencer. All sequencing decisions (which track is current,
/// when the playlist advances or stops, how repeat/shuffle behave) live here; the adapter only
/// reports that a track finished via <see cref="MusicPlaylistController.NotifyTrackCompleted"/>.
/// </summary>
public class MusicPlaylistControllerTests
{
    private static PlaylistConfig Config(
        PlaylistRepeatMode repeat = PlaylistRepeatMode.Off,
        PlaylistShuffleMode shuffle = PlaylistShuffleMode.Off,
        params string[] tracks)
    {
        var definition = new PlaylistDefinition { Repeat = repeat, Shuffle = shuffle };
        foreach (var name in tracks)
            definition.Tracks.Add(new PlaylistTrack { Name = name, Context = "overworld" });

        return new PlaylistConfig { Playlists = { ["p"] = definition } };
    }

    private static PlaylistConfig Sequential(params string[] tracks) =>
        Config(PlaylistRepeatMode.Off, PlaylistShuffleMode.Off, tracks);

    private static MusicPlaylistController Controller(
        PlaylistConfig config, int? seed = null) =>
        seed is null ? new MusicPlaylistController(config) : new MusicPlaylistController(config, new Random(seed.Value));

    [Fact]
    public void A_null_config_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new MusicPlaylistController(null!));
    }

    [Fact]
    public void A_fresh_controller_is_not_playing()
    {
        var controller = Controller(Sequential("a"));

        Assert.False(controller.IsPlaying);
        Assert.Null(controller.CurrentTrack);
        Assert.Null(controller.CurrentPlaylistName);
    }

    [Fact]
    public void Play_starts_the_first_track_in_a_sequential_playlist()
    {
        var controller = Controller(Sequential("a", "b", "c"));

        controller.Play("p");

        Assert.True(controller.IsPlaying);
        Assert.Equal("p", controller.CurrentPlaylistName);
        Assert.Equal("a", controller.CurrentTrack!.Name);
        Assert.Equal("overworld", controller.CurrentTrack!.Context);
    }

    [Fact]
    public void Play_is_a_noop_for_an_unknown_playlist()
    {
        var controller = Controller(Sequential("a"));

        controller.Play("nope");

        Assert.False(controller.IsPlaying);
        Assert.Null(controller.CurrentTrack);
    }

    [Fact]
    public void Play_is_a_noop_for_an_empty_playlist()
    {
        var controller = Controller(Config());

        controller.Play("p");

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void Play_restarts_the_playlist_from_the_top()
    {
        var controller = Controller(Sequential("a", "b", "c"));

        controller.Play("p");
        controller.Next();
        controller.Next();
        Assert.Equal("c", controller.CurrentTrack!.Name);

        controller.Play("p");
        Assert.Equal("a", controller.CurrentTrack!.Name);
    }

    [Fact]
    public void Next_advances_to_the_next_track()
    {
        var controller = Controller(Sequential("a", "b", "c"));
        controller.Play("p");

        controller.Next();

        Assert.Equal("b", controller.CurrentTrack!.Name);
    }

    [Fact]
    public void Next_is_a_noop_when_not_playing()
    {
        var controller = Controller(Sequential("a", "b"));

        controller.Next();

        Assert.False(controller.IsPlaying);
        Assert.Null(controller.CurrentTrack);
    }

    [Fact]
    public void Next_stops_at_the_end_when_repeat_is_off()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Off, PlaylistShuffleMode.Off, "a", "b"));
        controller.Play("p");
        controller.Next();

        controller.Next();

        Assert.False(controller.IsPlaying);
        Assert.Null(controller.CurrentTrack);
    }

    [Fact]
    public void Next_wraps_to_the_first_track_when_repeat_is_playlist()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Off, "a", "b"));
        controller.Play("p");
        controller.Next();

        controller.Next();

        Assert.True(controller.IsPlaying);
        Assert.Equal("a", controller.CurrentTrack!.Name);
    }

    [Fact]
    public void Next_breaks_a_track_repeat_loop()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Track, PlaylistShuffleMode.Off, "a", "b"));
        controller.Play("p");

        controller.Next();

        Assert.Equal("b", controller.CurrentTrack!.Name);
    }

    [Fact]
    public void Completion_repeats_the_same_track_when_repeat_is_track()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Track, PlaylistShuffleMode.Off, "a", "b"));
        controller.Play("p");

        controller.NotifyTrackCompleted();
        controller.NotifyTrackCompleted();

        Assert.True(controller.IsPlaying);
        Assert.Equal("a", controller.CurrentTrack!.Name);
    }

    [Fact]
    public void Completion_advances_then_wraps_when_repeat_is_playlist()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Off, "a", "b"));
        controller.Play("p");

        controller.NotifyTrackCompleted();
        Assert.Equal("b", controller.CurrentTrack!.Name);

        controller.NotifyTrackCompleted();
        Assert.Equal("a", controller.CurrentTrack!.Name);
    }

    [Fact]
    public void Completion_stops_after_the_last_track_when_repeat_is_off()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Off, PlaylistShuffleMode.Off, "a", "b"));
        controller.Play("p");

        controller.NotifyTrackCompleted();
        Assert.True(controller.IsPlaying);
        Assert.Equal("b", controller.CurrentTrack!.Name);

        controller.NotifyTrackCompleted();
        Assert.False(controller.IsPlaying);
        Assert.Null(controller.CurrentTrack);
    }

    [Fact]
    public void Completion_is_a_noop_when_not_playing()
    {
        var controller = Controller(Sequential("a", "b"));

        controller.NotifyTrackCompleted();

        Assert.False(controller.IsPlaying);
    }

    [Fact]
    public void TrackCompleted_raises_with_the_track_that_finished()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Off, "a", "b"));
        var finished = new List<string>();
        controller.TrackCompleted += track => finished.Add(track.Name);
        controller.Play("p");

        controller.NotifyTrackCompleted();
        controller.NotifyTrackCompleted();

        Assert.Equal(new[] { "a", "b" }, finished);
    }

    [Fact]
    public void TrackCompleted_is_not_raised_by_Play_or_Stop()
    {
        var controller = Controller(Sequential("a", "b"));
        var raised = 0;
        controller.TrackCompleted += _ => raised++;

        controller.Play("p");
        controller.Stop();

        Assert.Equal(0, raised);
    }

    [Fact]
    public void A_TrackCompleted_handler_sees_the_successor_as_CurrentTrack()
    {
        var controller = Controller(Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Off, "a", "b"));
        controller.Play("p");

        PlaylistTrack? seenOnHandler = null;
        var sawStopped = false;
        controller.TrackCompleted += _ =>
        {
            seenOnHandler = controller.CurrentTrack;
            sawStopped = !controller.IsPlaying;
        };

        controller.NotifyTrackCompleted();

        Assert.NotNull(seenOnHandler);
        Assert.Equal("b", seenOnHandler!.Name);
        Assert.False(sawStopped);
    }

    [Fact]
    public void Stop_clears_the_current_state()
    {
        var controller = Controller(Sequential("a", "b"));
        controller.Play("p");

        controller.Stop();

        Assert.False(controller.IsPlaying);
        Assert.Null(controller.CurrentTrack);
        Assert.Null(controller.CurrentPlaylistName);
    }

    [Fact]
    public void Shuffle_bag_plays_every_track_once_per_cycle()
    {
        var controller = Controller(
            Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Bag, "a", "b", "c", "d"), seed: 42);

        controller.Play("p");
        var firstCycle = new List<string> { controller.CurrentTrack!.Name };
        for (var i = 1; i < 4; i++)
        {
            controller.NotifyTrackCompleted();
            firstCycle.Add(controller.CurrentTrack!.Name);
        }

        Assert.Equal(new[] { "a", "b", "c", "d" }, firstCycle.OrderBy(x => x));
    }

    [Fact]
    public void Shuffle_bag_stops_after_one_cycle_when_repeat_is_off()
    {
        var controller = Controller(
            Config(PlaylistRepeatMode.Off, PlaylistShuffleMode.Bag, "a", "b", "c"), seed: 7);

        controller.Play("p");
        controller.NotifyTrackCompleted();
        controller.NotifyTrackCompleted();
        Assert.True(controller.IsPlaying);

        controller.NotifyTrackCompleted();

        Assert.False(controller.IsPlaying);
        Assert.Null(controller.CurrentTrack);
    }

    [Fact]
    public void Shuffle_bag_loops_when_repeat_is_playlist()
    {
        var controller = Controller(
            Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Bag, "a", "b", "c"), seed: 7);
        controller.Play("p");

        for (var i = 0; i < 3; i++)
            controller.NotifyTrackCompleted();

        Assert.True(controller.IsPlaying);
        Assert.NotNull(controller.CurrentTrack);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(99)]
    public void Shuffle_bag_never_repeats_across_a_cycle_boundary(int seed)
    {
        var controller = Controller(
            Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Bag, "a", "b", "c", "d"), seed);

        controller.Play("p");
        var sequence = new List<string> { controller.CurrentTrack!.Name };
        for (var i = 0; i < 16; i++)
        {
            controller.NotifyTrackCompleted();
            sequence.Add(controller.CurrentTrack!.Name);
        }

        for (var i = 1; i < sequence.Count; i++)
            Assert.NotEqual(sequence[i - 1], sequence[i]);
    }

    [Fact]
    public void Shuffle_is_deterministic_for_a_seeded_random()
    {
        var config = Config(PlaylistRepeatMode.Playlist, PlaylistShuffleMode.Bag, "a", "b", "c", "d", "e");

        var a = Controller(config, seed: 2026);
        var b = Controller(config, seed: 2026);

        a.Play("p");
        b.Play("p");
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(a.CurrentTrack!.Name, b.CurrentTrack!.Name);
            a.NotifyTrackCompleted();
            b.NotifyTrackCompleted();
        }
    }
}
