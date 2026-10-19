using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Headless pin of the <see cref="MusicDirector"/>: a fake backend stands in for the engine,
/// so playlist advance, crossfade realization, cue pitch application, and the full
/// master x context x duck x fade volume composition are all verifiable without MonoGame or KNI.
/// The adapters stay thin precisely because these behaviors live here.
/// </summary>
public class MusicDirectorTests
{
    /// <summary>Records every call the director makes; can be overlap-capable or single-instance.</summary>
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

        /// <summary>Simulates the active track reaching its end (completion detection).</summary>
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

    [Fact]
    public void Play_starts_the_first_track_on_the_next_update()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);

        Assert.Equal("a", backend.Played.Single().Name);
        Assert.True(director.IsPlaying);
    }

    [Fact]
    public void Backend_track_end_advances_the_playlist_and_starts_the_next_track()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);
        backend.RaiseTrackEnded();
        director.Update(0.016f);

        Assert.Equal("b", backend.Played.Last().Name);
        Assert.Equal("b", director.CurrentTrack?.Name);
    }

    [Fact]
    public void Track_completed_reports_the_finished_track_after_the_playlist_advanced()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist);
        var reported = new List<string?>();
        director.TrackCompleted += t => reported.Add(t.Name);

        director.Play("main");
        director.Update(0.016f);
        backend.RaiseTrackEnded();

        Assert.Equal(new[] { "a" }, reported);
    }

    [Fact]
    public void Repeat_track_restarts_the_same_track_after_a_natural_end()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo", PlaylistRepeatMode.Track), new Random(1));
        var director = new MusicDirector(backend, playlist);

        director.Play("main");
        director.Update(0.016f);
        backend.RaiseTrackEnded();
        director.Update(0.016f);

        Assert.Equal(2, backend.Played.Count(p => p.Name == "solo"));
        Assert.True(director.IsPlaying);
    }

    [Fact]
    public void Repeat_off_stops_after_the_last_track()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist);

        director.Play("main");
        director.Update(0.016f);
        backend.RaiseTrackEnded();
        director.Update(0.016f);
        backend.RaiseTrackEnded();
        director.Update(0.016f);

        Assert.False(director.IsPlaying);
        Assert.Equal(2, backend.Played.Count);
        Assert.Equal("b", backend.Played[^1].Name);
    }

    [Fact]
    public void Overlap_backend_crossfades_with_equal_power_volumes_after_a_skip()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);
        director.Next();
        director.Update(0.016f);

        Assert.Equal(2, backend.Played.Count);
        Assert.Equal(0f, backend.Played[^1].InitialVolume);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
        Assert.True(backend.OutgoingVolumes.Last() > 0f);
    }

    [Fact]
    public void Overlap_crossfade_completes_by_stopping_the_outgoing_track_at_full_volume()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);
        director.Next();
        director.Update(0.016f);
        director.Update(1f);

        Assert.Equal(1, backend.StopOutgoingCalls);
        Assert.Equal(1f, backend.ActiveVolumes.Last());
    }

    [Fact]
    public void Zero_crossfade_switches_instantly_to_full_volume()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 0f });

        director.Play("main");
        director.Update(0.016f);
        director.Next();
        director.Update(0.016f);

        // One crossfade completes per transition (first track + skip), each ending at full volume.
        Assert.Equal(2, backend.StopOutgoingCalls);
        Assert.Equal(1f, backend.ActiveVolumes.Last());
    }

    [Fact]
    public void Non_overlap_backend_fades_out_then_switches_then_fades_in()
    {
        var backend = new FakeMusicBackend(supportsOverlap: false);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);
        director.Next();

        // Fade-out phase: the single instance follows the outgoing ramp down.
        director.Update(0.25f);
        var volumeAfterFadeOutStart = backend.ActiveVolumes.Last();
        Assert.True(volumeAfterFadeOutStart < 1f);

        // Cross the fade-out boundary (half of 1s): switch to "b".
        director.Update(0.3f);
        Assert.Equal(2, backend.Played.Count);
        Assert.Equal("b", backend.Played[^1].Name);
        Assert.Equal(1, backend.StopCalls);

        // Fade-in ramp finishes at full volume.
        director.Update(1f);
        Assert.Equal(1f, backend.ActiveVolumes.Last());
    }

    [Fact]
    public void Non_overlap_first_play_fades_in_from_silence()
    {
        var backend = new FakeMusicBackend(supportsOverlap: false);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 1f });

        director.Play("main");
        director.Update(0.016f);

        Assert.Equal("solo", backend.Played.Single().Name);
        Assert.Equal(0f, backend.Played.Single().InitialVolume);
        Assert.True(backend.ActiveVolumes.Last() < 1f);
    }

    [Fact]
    public void Music_cue_variation_applies_pitch_to_played_tracks()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var music = new MusicConfig { CueVariation = new CueVariation(0.5f, 0f) };
        var director = new MusicDirector(backend, playlist, music, new Random(42));

        director.Play("main");
        director.Update(0.016f);

        var pitch = backend.Played.Single().Pitch;
        Assert.InRange(pitch, -0.5f, 0.5f);
    }

    [Fact]
    public void Without_cue_variation_the_pitch_is_unchanged()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig());

        director.Play("main");
        director.Update(0.016f);

        Assert.Equal(0f, backend.Played.Single().Pitch);
    }

    [Fact]
    public void Master_volume_scales_the_composed_volume()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 0f, MasterVolume = 0.5f });

        director.Play("main");
        director.Update(0.016f);

        Assert.Equal(0.5f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Context_scale_multiplies_the_composed_volume()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var music = new MusicConfig { CrossfadeSeconds = 0f };
        music.Contexts["Overworld"] = 0.25f;
        var director = new MusicDirector(backend, playlist, music);

        director.Play("main");
        director.Update(0.016f);

        Assert.Equal(0.25f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Duck_ratio_multiplies_the_composed_volume()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var music = new MusicConfig { CrossfadeSeconds = 0f };
        music.DuckSources["sfx"] = new DuckSourceConfig { Ratio = 0.3f, AttackSeconds = 0f, ReleaseSeconds = 0f };
        var director = new MusicDirector(backend, playlist, music);

        director.Play("main");
        director.Update(0.016f);
        director.SetDuck("sfx", true);
        director.Update(0.016f);

        Assert.Equal(0.3f, director.DuckRatio, 3);
        Assert.Equal(0.3f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Releasing_a_duck_returns_the_volume_to_full()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var music = new MusicConfig { CrossfadeSeconds = 0f };
        music.DuckSources["sfx"] = new DuckSourceConfig { Ratio = 0.3f, AttackSeconds = 0f, ReleaseSeconds = 0f };
        var director = new MusicDirector(backend, playlist, music);

        director.Play("main");
        director.Update(0.016f);
        director.SetDuck("sfx", true);
        director.Update(0.016f);
        director.SetDuck("sfx", false);
        director.Update(0.016f);

        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Stop_halts_the_playlist_and_the_backend()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist);

        director.Play("main");
        director.Update(0.016f);
        director.Stop();

        Assert.False(director.IsPlaying);
        Assert.True(backend.StopCalls > 0);
        Assert.Null(director.CurrentTrack);
    }

    [Fact]
    public void Play_unknown_playlist_is_a_no_op()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(TwoTracks("a", "b"), new Random(1));
        var director = new MusicDirector(backend, playlist);

        director.Play("missing");
        director.Update(0.016f);

        Assert.Empty(backend.Played);
        Assert.False(director.IsPlaying);
    }

    [Fact]
    public void Updates_while_idle_do_not_touch_the_backend_volumes()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var director = new MusicDirector(backend, new MusicPlaylistController(TwoTracks("a", "b"), new Random(1)));

        director.Update(0.016f);
        director.Update(0.016f);

        Assert.Empty(backend.Played);
        Assert.Empty(backend.ActiveVolumes);
    }

    [Fact]
    public void Master_bus_scales_the_composed_music_volume()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var music = new MusicConfig { CrossfadeSeconds = 0f, MasterVolume = 0.5f };
        var director = new MusicDirector(backend, playlist, music, masterVolume: 0.5f);

        director.Play("main");
        director.Update(0.016f);

        // master bus (0.5) x music bus (0.5) = 0.25
        Assert.Equal(0.25f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Master_bus_defaults_to_full_volume_when_omitted()
    {
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var music = new MusicConfig { CrossfadeSeconds = 0f, MasterVolume = 0.5f };
        var director = new MusicDirector(backend, playlist, music);

        director.Play("main");
        director.Update(0.016f);

        // no master bus passed: music bus (0.5) alone
        Assert.Equal(0.5f, backend.ActiveVolumes.Last(), 3);
    }

    [Fact]
    public void Master_bus_can_be_changed_at_runtime()
    {
        // A settings slider must move the master live, without rebuilding the director -
        // the same live behaviour the music bus already has through its config reference.
        var backend = new FakeMusicBackend(supportsOverlap: true);
        var playlist = new MusicPlaylistController(OneTrack("solo"), new Random(1));
        var director = new MusicDirector(backend, playlist, new MusicConfig { CrossfadeSeconds = 0f });

        director.Play("main");
        director.Update(0.016f);
        Assert.Equal(1f, backend.ActiveVolumes.Last(), 3);

        director.MasterVolume = 0.25f;
        director.Update(0.016f);

        Assert.Equal(0.25f, backend.ActiveVolumes.Last(), 3);
    }
}