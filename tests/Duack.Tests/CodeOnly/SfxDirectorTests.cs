using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the <see cref="SfxDirector"/>: cue-name one-shot playback against a discovered
/// <see cref="AudioLibrary"/> with automatic music ducking — a successful play pulses the music
/// director's duck source, unknown cues and backend refusals never duck, and <see cref="SfxDirector.PlayNext"/>
/// draws from a shuffle bag so a cue never repeats back-to-back.
/// </summary>
public class SfxDirectorTests
{
    private sealed class FakeSfxBackend : ISfxBackend
    {
        public List<(string Cue, float Pitch, float Volume)> Played { get; } = new();

        /// <summary>What the next call returns (false = engine refuses the cue).</summary>
        public bool Result { get; set; } = true;

        public bool Play(string cueName, float pitch, float volume)
        {
            Played.Add((cueName, pitch, volume));
            return Result;
        }

        public void Stop() { }
    }

    /// <summary>Minimal music backend: the duck test only needs the director's mix math.</summary>
    private sealed class FakeMusicBackend : IMusicBackend
    {
        public bool SupportsOverlap => true;
        public bool IsTrackPlaying { get; private set; }

        public event EventHandler? TrackEnded
        {
            add { }
            remove { }
        }

        public void PlayTrack(string trackName, float initialVolume, float pitch) => IsTrackPlaying = true;
        public void SetActiveVolume(float volume) { }
        public void SetOutgoingVolume(float volume) { }
        public void StopOutgoing() { }
        public void Stop() => IsTrackPlaying = false;
        public void Update(float deltaSeconds) { }
    }

    private static readonly AudioLibrary Library = AudioLibrary.FromFiles(new[]
    {
        "SFX/hit.wav",
        "SFX/dialog.wav",
        "SFX/notice-video-game-sfx.wav",
    });

    /// <summary>A director whose named duck source is configured for an instant 0.3x dip.</summary>
    private static MusicDirector CreateMusicDirector(string duckSource = "sfx")
    {
        var music = new MusicConfig { CrossfadeSeconds = 0f };
        music.DuckSources[duckSource] = new DuckSourceConfig
        {
            Ratio = 0.3f,
            AttackSeconds = 0f,
            HoldSeconds = 10f,
            ReleaseSeconds = 0f,
        };

        var playlists = new PlaylistConfigBuilder()
            .WithPlaylist("solo", p => p.Track("track", ""))
            .Build();
        var director = new MusicDirector(
            new FakeMusicBackend(),
            new MusicPlaylistController(playlists, new Random(1)),
            music);

        director.Play("solo");
        director.Update(0.016f);
        return director;
    }

    [Fact]
    public void A_successful_play_reaches_the_backend_with_its_parameters_and_ducks_the_music()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music);

        Assert.True(sfx.Play("hit", pitch: 0.25f, volume: 0.8f));

        Assert.Equal(("hit", 0.25f, 0.8f), Assert.Single(backend.Played));
        music.Update(0.016f);
        Assert.Equal(0.3f, music.DuckRatio, 3);
    }

    [Fact]
    public void Sfx_cue_variation_applies_pitch_jitter_per_play()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(
            Library,
            backend,
            music,
            sfxConfig: new SfxConfig { CueVariation = new CueVariation(pitchRange: 0.5f, volumeRange: 0f) },
            random: new Random(42));

        Assert.True(sfx.Play("hit"));

        // Half-width pitch jitter (0.5) applied around the natural pitch; pitch-range-only jitter
        // leaves the volume untouched, and the master/bus defaults compose a full-volume play.
        var played = Assert.Single(backend.Played);
        Assert.Equal("hit", played.Cue);
        Assert.InRange(played.Pitch, -0.5f, 0.5f);
        Assert.Equal(1f, played.Volume);
    }

    [Fact]
    public void Sfx_cue_variation_adds_jitter_on_top_of_an_explicit_per_play_pitch()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(
            Library,
            backend,
            music,
            sfxConfig: new SfxConfig { CueVariation = new CueVariation(pitchRange: 0.5f, volumeRange: 0.2f) },
            random: new Random(42));

        Assert.True(sfx.Play("hit", pitch: 0.25f, volume: 0.8f));

        var played = Assert.Single(backend.Played);
        Assert.Equal("hit", played.Cue);
        // The caller's authored pitch (0.25) is the base; the jitter (±0.5) is added on top, clamped
        // to the engine's [-1, 1] range.
        Assert.InRange(played.Pitch, -0.25f, 0.75f);
        // Volume jitter (±20%) scales the caller's volume around its own value.
        Assert.InRange(played.Volume, 0.64f, 0.96f);
    }

    [Fact]
    public void Without_sfx_cue_variation_the_cue_plays_at_its_base_pitch_and_volume()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music);

        Assert.True(sfx.Play("hit", pitch: 0.25f, volume: 0.8f));

        // No authored variation: the caller's pitch/volume pass straight through untouched.
        Assert.Equal(("hit", 0.25f, 0.8f), Assert.Single(backend.Played));
    }

    [Fact]
    public void An_unknown_cue_neither_reaches_the_backend_nor_ducks()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music);

        Assert.False(sfx.Play("explode"));

        Assert.Empty(backend.Played);
        music.Update(0.016f);
        Assert.Equal(1f, music.DuckRatio, 3);
    }

    [Fact]
    public void A_backend_refusal_reports_failure_and_never_ducks()
    {
        var backend = new FakeSfxBackend { Result = false };
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music);

        Assert.False(sfx.Play("hit"));

        Assert.Single(backend.Played); // attempted, but not a success
        music.Update(0.016f);
        Assert.Equal(1f, music.DuckRatio, 3);
    }

    [Fact]
    public void PlayNext_draws_from_the_bag_and_never_repeats_a_cue_back_to_back()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music, random: new Random(7));

        var drawn = new List<string>();
        for (var i = 0; i < 15; i++)
        {
            Assert.True(sfx.PlayNext());
            drawn.Add(backend.Played[^1].Cue);
        }

        Assert.All(drawn, cue => Assert.Contains(cue, Library.Sfx));
        for (var i = 1; i < drawn.Count; i++)
            Assert.NotEqual(drawn[i - 1], drawn[i]);
    }

    [Fact]
    public void PlayNext_on_an_empty_library_reports_failure_without_touching_the_backend()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(AudioLibrary.FromFiles(Array.Empty<string>()), backend, music);

        Assert.False(sfx.PlayNext());

        Assert.Empty(backend.Played);
        music.Update(0.016f);
        Assert.Equal(1f, music.DuckRatio, 3);
    }

    [Fact]
    public void A_custom_duck_source_pulses_that_source_instead()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector(duckSource: "voice");
        var sfx = new SfxDirector(Library, backend, music, duckSource: "voice");

        Assert.True(sfx.Play("dialog"));

        music.Update(0.016f);
        Assert.Equal(0.3f, music.DuckRatio, 3);
    }

    [Fact]
    public void Names_exposes_the_discovered_sfx()
    {
        var sfx = new SfxDirector(Library, new FakeSfxBackend(), CreateMusicDirector());

        Assert.Equal(Library.Sfx, sfx.Names);
    }

    [Fact]
    public void The_constructor_rejects_null_dependencies()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();

        Assert.Throws<ArgumentNullException>(() => new SfxDirector(null!, backend, music));
        Assert.Throws<ArgumentNullException>(() => new SfxDirector(Library, null!, music));
        Assert.Throws<ArgumentNullException>(() => new SfxDirector(Library, backend, null!));
    }

    [Fact]
    public void Master_bus_and_sfx_bus_scale_the_played_volume()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music, masterVolume: 0.5f, sfxBusVolume: 0.5f);

        Assert.True(sfx.Play("hit", volume: 0.8f));

        // master bus (0.5) x sfx bus (0.5) x per-play (0.8) = 0.2
        Assert.Equal(("hit", 0f, 0.2f), Assert.Single(backend.Played));
    }

    [Fact]
    public void Sfx_bus_scales_the_played_volume_alone()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music, sfxBusVolume: 0.5f);

        Assert.True(sfx.Play("hit", volume: 0.8f));

        // sfx bus (0.5) x per-play (0.8) = 0.4
        Assert.Equal(("hit", 0f, 0.4f), Assert.Single(backend.Played));
    }

    [Fact]
    public void Master_bus_defaults_to_full_volume_when_omitted()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music);

        Assert.True(sfx.Play("hit", volume: 0.8f));

        Assert.Equal(("hit", 0f, 0.8f), Assert.Single(backend.Played));
    }

    [Fact]
    public void Buses_can_be_changed_at_runtime()
    {
        // Settings sliders move the buses live: the next play composes with the new values,
        // no director rebuild required.
        var backend = new FakeSfxBackend();
        var music = CreateMusicDirector();
        var sfx = new SfxDirector(Library, backend, music);

        Assert.True(sfx.Play("hit", volume: 0.8f));
        Assert.Equal(("hit", 0f, 0.8f), Assert.Single(backend.Played));

        sfx.MasterVolume = 0.5f;
        sfx.SfxBusVolume = 0.5f;
        Assert.True(sfx.Play("hit", volume: 0.8f));

        Assert.Equal(("hit", 0f, 0.2f), backend.Played.Last());
    }
}
