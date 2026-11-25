using System;
using System.Collections.Generic;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins bus routing on <see cref="SfxManager.Play"/>: the bus gain composes into the
/// per-play volume after master and sfx-bus, an unknown bus falls back (and reports), a
/// zero-effective-gain play reports instead of staying silent, and a duck source targeted
/// at a bus scales plays on it.
/// </summary>
public class BusRoutingTests
{
    private sealed class FakeSfxBackend : ISfxBackend
    {
        public List<(string Cue, float Pitch, float Volume)> Played { get; } = new();

        public bool Play(string cueName, float pitch, float volume)
        {
            Played.Add((cueName, pitch, volume));
            return true;
        }

        public void Stop() { }
    }

    private sealed class FakeMusicBackend : IMusicBackend
    {
        public bool SupportsOverlap => true;
        public bool IsTrackPlaying { get; private set; }
        public float CurrentTrackElapsedSeconds { get; set; }
        public float? CurrentTrackDurationSeconds { get; set; }
        public event EventHandler? TrackEnded { add { } remove { } }
        public void PlayTrack(string trackName, float initialVolume, float pitch) => IsTrackPlaying = true;
        public void SetActiveVolume(float volume) { }
        public void SetOutgoingVolume(float volume) { }
        public void StopOutgoing() { }
        public void Stop() => IsTrackPlaying = false;
        public void Update(float deltaSeconds) { }
    }

    private static readonly AudioLibrary Library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav" });

    private static MusicManager CreateMusicManager(string duckSource, DuckSourceConfig? config = null)
    {
        var music = new MusicConfig { CrossfadeSeconds = 0f };
        music.DuckSources[duckSource] = config ?? new DuckSourceConfig
        {
            Ratio = 0.3f,
            AttackSeconds = 0f,
            HoldSeconds = 1f,
            ReleaseSeconds = 0f,
        };

        var playlists = new PlaylistConfigBuilder()
            .WithPlaylist("solo", p => p.Track("track", ""))
            .Build();
        var manager = new MusicManager(
            new FakeMusicBackend(),
            new MusicPlaylistController(playlists, new Random(1)),
            music);

        manager.Play("solo");
        manager.Update(0.016f);
        return manager;
    }

    private static SfxManager CreateSfx(FakeSfxBackend backend, MusicManager music, SfxConfig? sfxConfig = null)
    {
        var sfx = new SfxManager(Library, backend, music, sfxConfig: sfxConfig);
        return sfx;
    }

    private static SfxConfig ConfigBus(string name, float volume) => new()
    {
        Buses = new Dictionary<string, BusConfig> { [name] = new BusConfig { Volume = volume } }
    };

    [Fact]
    public void Play_BusVolume_ComposesAfterMasterAndSfxBus()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("sfx");
        var sfx = CreateSfx(backend, music, ConfigBus("ambience", 0.5f));
        sfx.MasterVolume = 0.5f;

        sfx.Play("hit", bus: "ambience");

        Assert.Equal(0.25f, backend.Played[0].Volume, 2);
    }

    [Fact]
    public void Play_UnknownBus_FallsBackToSfxGainAndReports()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("sfx");
        var sfx = CreateSfx(backend, music);

        string? reported = null;
        sfx.Buses.BusFellBack += bus => reported = bus;

        sfx.Play("hit", bus: "ambientt");

        Assert.Equal("ambientt", reported);
        Assert.Equal(1f, backend.Played[0].Volume, 2);
    }

    [Fact]
    public void Play_ZeroEffectiveGain_NoBackendCallAndReports()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("sfx");
        var sfx = CreateSfx(backend, music, ConfigBus("ambience", 1f));
        string? reported = null;
        sfx.ZeroEffectiveGain += cue => reported = cue;

        sfx.Buses.SetMute("ambience", true);
        var played = sfx.Play("hit", bus: "ambience");

        Assert.True(played);
        Assert.Empty(backend.Played);
        Assert.Equal("hit", reported);
    }

    [Fact]
    public void Play_BusTargetedDuckActive_ScalesCuesOnThatBusOnly()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("dialogue", new DuckSourceConfig
        {
            Ratio = 0.5f,
            AttackSeconds = 0f,
            HoldSeconds = 1f,
            ReleaseSeconds = 0f,
            Target = "ambience",
        });
        var sfx = CreateSfx(backend, music, ConfigBus("ambience", 1f));

        sfx.Play("hit", bus: "ambience");
        Assert.Equal(1f, backend.Played[0].Volume, 2);

        music.SetDuck("dialogue", true);
        music.Update(1f);
        sfx.Play("hit", bus: "ambience");
        Assert.Equal(0.5f, backend.Played[1].Volume, 2);

        sfx.Play("hit");
        Assert.Equal(1f, backend.Played[2].Volume, 2);
    }
}
