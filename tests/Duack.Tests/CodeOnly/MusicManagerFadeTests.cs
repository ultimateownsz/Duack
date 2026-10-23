using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the fade-in / fade-out support on <see cref="MusicManager"/> (music playback fades).
/// The adapters stay thin: these behaviors are verified against the fake backend.
/// </summary>
public class MusicManagerFadeTests
{
    private sealed class FakeMusicBackend : IMusicBackend
    {
        public FakeMusicBackend(bool supportsOverlap) => SupportsOverlap = supportsOverlap;

        public bool SupportsOverlap { get; }
        public List<(string Name, float InitialVolume, float Pitch)> Played { get; } = new();
        public List<float> ActiveVolumes { get; } = new();
        public List<float> OutgoingVolumes { get; } = new();
        public int StopOutgoingCalls { get; private set; }
        public int StopCalls { get; private set; }
        public bool IsTrackPlaying { get; private set; }
        public event EventHandler? TrackEnded;

        public void PlayTrack(string trackName, float initialVolume = 1f, float pitch = 0f)
        {
            Played.Add((trackName, initialVolume, pitch));
            IsTrackPlaying = true;
        }

        public void SetActiveVolume(float volume) => ActiveVolumes.Add(volume);
        public void SetOutgoingVolume(float volume) => OutgoingVolumes.Add(volume);
        public void StopOutgoing() => StopOutgoingCalls++;
        public void Stop() { StopCalls++; IsTrackPlaying = false; }
        public void Update(float deltaSeconds) { }

        public void RaiseTrackEnded()
        {
            IsTrackPlaying = false;
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    private static PlaylistConfig TwoTracks(string first, string second, PlaylistRepeatMode repeat = PlaylistRepeatMode.Off) => new()
    {
        Playlists =
        {
            ["main"] = new PlaylistDefinition
            {
                Tracks =
                {
                    new PlaylistTrack { Name = first, Context = "Overworld" },
                    new PlaylistTrack { Name = second, Context = "Overworld" },
                },
                Repeat = repeat,
            },
        },
    };

    private static PlaylistConfig OneTrack(string name, PlaylistRepeatMode repeat = PlaylistRepeatMode.Off) => new()
    {
        Playlists =
        {
            ["main"] = new PlaylistDefinition
            {
                Tracks = { new PlaylistTrack { Name = name, Context = "Overworld" } },
                Repeat = repeat,
            },
        },
    };

    private static PlaylistConfig OneTrackWithFade(string name, float? fadeIn = null, float? fadeOut = null, PlaylistRepeatMode repeat = PlaylistRepeatMode.Off) => new()
    {
        Playlists =
        {
            ["main"] = new PlaylistDefinition
            {
                Tracks = { new PlaylistTrack { Name = name, Context = "Overworld", FadeInSeconds = fadeIn, FadeOutSeconds = fadeOut } },
                Repeat = repeat,
            },
        },
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Stop_with_fade_out_ramps_down_and_stops_after_the_duration(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);

        director.Stop(0.5f);

        // Not stopped immediately: the fade runs first.
        Assert.True(director.IsPlaying);
        Assert.Equal(0, backend.StopCalls);

        director.Update(0.25f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.Equal(0, backend.StopCalls);

        director.Update(0.30f);
        Assert.False(director.IsPlaying);
        Assert.True(backend.StopCalls > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Play_with_fade_in_starts_the_first_track_at_silence_and_ramps_up(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main", fadeInSeconds: 0.5f);
        director.Update(0.016f);

        Assert.Equal("solo", backend.Played.Single().Name);
        Assert.Equal(0f, backend.Played.Single().InitialVolume);
        Assert.True(backend.ActiveVolumes.Last() < 1f);

        director.Update(1f);
        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Stop_without_fade_out_stops_immediately()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicManager(backend, playlist);

        director.Play("main");
        director.Update(0.016f);

        director.Stop();

        Assert.False(director.IsPlaying);
        Assert.True(backend.StopCalls > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Play_fade_in_duration_controls_the_ramp(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main", fadeInSeconds: 0.25f);
        director.Update(0.016f);

        director.Update(0.1f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.True(backend.ActiveVolumes.Last() > 0f);

        director.Update(0.2f);
        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Per_track_fade_in_duration_controls_the_first_play_ramp(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrackWithFade("solo", fadeIn: 0.25f), new Random(1));
        var director = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);

        director.Update(0.1f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.True(backend.ActiveVolumes.Last() > 0f);

        director.Update(0.2f);
        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Per_track_fade_out_duration_controls_stop(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrackWithFade("solo", fadeOut: 0.25f), new Random(1));
        var director = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);

        director.Stop();
        Assert.True(director.IsPlaying);
        Assert.Equal(0, backend.StopCalls);

        director.Update(0.1f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.Equal(0, backend.StopCalls);

        director.Update(0.2f);
        Assert.False(director.IsPlaying);
        Assert.True(backend.StopCalls > 0);
    }

    [Fact]
    public void Overlap_transition_fades_out_the_prior_track_faster_than_it_fades_in_the_new_one()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);
        director.Next();
        director.Update(0.016f);

        // The prior (outgoing) track is already silent while the incoming track is still fading in.
        director.Update(0.25f);
        Assert.True(backend.OutgoingVolumes.Count > 0);
        Assert.Equal(0f, backend.OutgoingVolumes.Last(), 3);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.True(backend.ActiveVolumes.Last() > 0f);
    }

    [Fact]
    public void Sequenced_transition_switches_to_the_new_track_before_the_fade_in_completes()
    {
        var backend = new FakeMusicBackend(supportsOverlap: false);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);
        director.Next();
        director.Update(0.25f);

        Assert.Equal(2, backend.Played.Count);
        Assert.Equal("b", backend.Played[^1].Name);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
    }

    [Fact]
    public void Per_track_fade_durations_are_carried_on_the_track()
    {
        var track = new PlaylistTrack { Name = "solo", Context = "Overworld", FadeInSeconds = 1.25f, FadeOutSeconds = 0.75f };
        Assert.Equal(1.25f, track.FadeInSeconds);
        Assert.Equal(0.75f, track.FadeOutSeconds);
    }

    [Fact]
    public void PlaylistConfigBuilder_Track_accepts_fade_durations()
    {
        var builder = new PlaylistConfigBuilder();
        builder.WithPlaylist("main", p => p.Track("solo", "Overworld", fadeInSeconds: 1.0f, fadeOutSeconds: 0.5f));
        var config = builder.Build();
        var track = config.Playlists["main"].Tracks.Single();
        Assert.Equal(1.0f, track.FadeInSeconds);
        Assert.Equal(0.5f, track.FadeOutSeconds);
    }
}