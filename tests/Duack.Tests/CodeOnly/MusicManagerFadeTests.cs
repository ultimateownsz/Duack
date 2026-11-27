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
        public float CurrentTrackElapsedSeconds { get; set; }
        public float? CurrentTrackDurationSeconds { get; set; }
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
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main");
        manager.Update(0.016f);

        manager.Stop(0.5f);

        // Not stopped immediately: the fade runs first.
        Assert.True(manager.IsPlaying);
        Assert.Equal(0, backend.StopCalls);

        manager.Update(0.25f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.Equal(0, backend.StopCalls);

        manager.Update(0.30f);
        Assert.False(manager.IsPlaying);
        Assert.True(backend.StopCalls > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Play_with_fade_in_starts_the_first_track_at_silence_and_ramps_up(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main", fadeInSeconds: 0.5f);
        manager.Update(0.016f);

        Assert.Equal("solo", backend.Played.Single().Name);
        Assert.Equal(0f, backend.Played.Single().InitialVolume);
        Assert.True(backend.ActiveVolumes.Last() < 1f);

        manager.Update(1f);
        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Stop_without_fade_out_stops_immediately()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var manager = new MusicManager(backend, playlist);

        manager.Play("main");
        manager.Update(0.016f);

        manager.Stop();

        Assert.False(manager.IsPlaying);
        Assert.True(backend.StopCalls > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Play_fade_in_duration_controls_the_ramp(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main", fadeInSeconds: 0.25f);
        manager.Update(0.016f);

        manager.Update(0.1f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.True(backend.ActiveVolumes.Last() > 0f);

        manager.Update(0.2f);
        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Per_track_fade_in_duration_controls_the_first_play_ramp(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrackWithFade("solo", fadeIn: 0.25f), new Random(1));
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main");
        manager.Update(0.016f);

        manager.Update(0.1f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.True(backend.ActiveVolumes.Last() > 0f);

        manager.Update(0.2f);
        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Per_track_fade_out_duration_controls_stop(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrackWithFade("solo", fadeOut: 0.25f), new Random(1));
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main");
        manager.Update(0.016f);

        manager.Stop();
        Assert.True(manager.IsPlaying);
        Assert.Equal(0, backend.StopCalls);

        manager.Update(0.1f);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.Equal(0, backend.StopCalls);

        manager.Update(0.2f);
        Assert.False(manager.IsPlaying);
        Assert.True(backend.StopCalls > 0);
    }

    [Fact]
    public void Overlap_transition_fades_out_the_prior_track_faster_than_it_fades_in_the_new_one()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main");
        manager.Update(0.016f);
        manager.Next();
        manager.Update(0.016f);

        // The prior (outgoing) track is already silent while the incoming track is still fading in.
        manager.Update(0.25f);
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
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main");
        manager.Update(0.016f);
        manager.Next();
        manager.Update(0.6f);

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Call_level_fade_in_overrides_the_track_fade_in(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrackWithFade("a", fadeIn: 0.4f));
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main", fadeInSeconds: 0.8f);

        Assert.True(manager.IsTransitioning);
        Assert.Equal(0.8f, manager.TransitionDurationSeconds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Track_fade_in_applies_when_the_call_omits_a_fade(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var playlist = new MusicPlaylistController(OneTrackWithFade("a", fadeIn: 0.4f));
        var manager = new MusicManager(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        manager.Play("main");

        Assert.True(manager.IsTransitioning);
        Assert.Equal(0.4f, manager.TransitionDurationSeconds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_call_fade_never_leaks_into_a_later_transition(bool supportsOverlap)
    {
        var backend = new FakeMusicBackend(supportsOverlap);
        var config = new PlaylistConfig
        {
            Playlists =
            {
                ["main"] = new PlaylistDefinition
                {
                    Tracks =
                    {
                        new PlaylistTrack { Name = "a", Context = "Overworld" },
                        new PlaylistTrack { Name = "b", Context = "Overworld" },
                    },
                },
            },
        };
        var manager = new MusicManager(backend, new MusicPlaylistController(config), new MusicConfig { CrossfadeSeconds = 0.5f });

        manager.Play("main");
        manager.Update(0.016f);
        manager.Play("main", fadeInSeconds: 1.0f);   // same track: the request is a no-op

        manager.Next();                               // the next transition must not see the 1.0f
        manager.Update(0f);

        Assert.True(manager.IsTransitioning);
        // Web splits the 0.5s crossfade into 0.25s out + 0.25s in, so the total is 0.5s on
        // both backends; the stale 1.0f call fade would have reported 1.0s / 1.5s instead.
        Assert.Equal(0.5f, manager.TransitionDurationSeconds);
    }
}
