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
}
