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
    [Fact]
    public void Play_MutedSfxBus_ReportsZeroEffectiveGainOnTheFixedPath()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("sfx");
        var sfx = CreateSfx(backend, music);
        string? reported = null;
        sfx.ZeroEffectiveGain += cue => reported = cue;

        sfx.Buses.SetMute(BusNames.Sfx, true);
        var played = sfx.Play("hit");

        Assert.True(played);
        Assert.Empty(backend.Played);
        Assert.Equal("hit", reported);
    }

    [Fact]
    public void Volume_properties_delegate_into_the_one_table()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("sfx");
        var sfx = CreateSfx(backend, music);

        sfx.MasterVolume = 0.5f;
        sfx.SfxBusVolume = 0.25f;

        Assert.Equal(0.5f, sfx.Buses.Views.Single(v => v.Name == BusNames.Master).Volume);
        Assert.Equal(0.25f, sfx.Buses.Views.Single(v => v.Name == BusNames.Sfx).Volume);

        sfx.Buses.SetBus(BusNames.Sfx, 0.5f);
        Assert.Equal(0.5f, sfx.SfxBusVolume);

        sfx.Play("hit");
        Assert.Equal(0.25f, backend.Played[0].Volume, 2); // master 0.5 x sfx 0.5
    }
    [Fact]
    public void Views_carry_duck_contribution_for_every_bus_including_the_fixed_three()
    {
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("d", new DuckSourceConfig
        {
            Ratio = 0.5f,
            AttackSeconds = 0f,
            HoldSeconds = 1f,
            ReleaseSeconds = 0f,
            Target = BusNames.Sfx,
        });
        var sfx = CreateSfx(backend, music, ConfigBus("ambience", 1f));

        // Every bus is in the readback - fixed three and user bus - and the duck contribution
        // is read live from the duck system at snapshot time (D4: never embedded in bus state).
        var views = sfx.Buses.Views;
        Assert.Equal(1f, views.Single(v => v.Name == BusNames.Master).DuckRatio);
        Assert.Equal(1f, views.Single(v => v.Name == BusNames.Music).DuckRatio);
        var sfxBus = views.Single(v => v.Name == BusNames.Sfx);
        Assert.Equal(1f, sfxBus.DuckRatio);
        Assert.Equal(1f, views.Single(v => v.Name == "ambience").DuckRatio);

        music.SetDuck("d", true);
        music.Update(1f);

        views = sfx.Buses.Views;
        Assert.Equal(0.5f, views.Single(v => v.Name == BusNames.Sfx).DuckRatio, 2);
        Assert.Equal(1f, views.Single(v => v.Name == BusNames.Master).DuckRatio, 2);
        Assert.Equal(1f, views.Single(v => v.Name == "ambience").DuckRatio, 2);
    }
    [Fact]
    public void SfxTargetedDuck_scales_the_fixed_unrouted_path()
    {
        // Gate D fold: a duck source aimed at the literal "sfx" bus dips the fixed (un-routed)
        // path too - one semantic, one path - not only bus-routed plays.
        var backend = new FakeSfxBackend();
        var music = CreateMusicManager("d", new DuckSourceConfig
        {
            Ratio = 0.5f,
            AttackSeconds = 0f,
            HoldSeconds = 1f,
            ReleaseSeconds = 0f,
            Target = BusNames.Sfx,
        });
        var sfx = CreateSfx(backend, music);

        sfx.Play("hit");
        Assert.Equal(1f, backend.Played[0].Volume, 2);

        music.SetDuck("d", true);
        music.Update(1f);
        sfx.Play("hit");
        Assert.Equal(0.5f, backend.Played[1].Volume, 2);
        sfx.Play("hit", duck: false);
        Assert.Equal(0.5f, backend.Played[2].Volume, 2); // the duck is a bus ratio, not a per-play hold

        music.SetDuck("d", false);
        music.Update(1f);
        sfx.Play("hit");
        Assert.Equal(1f, backend.Played[3].Volume, 2);
    }

    [Fact]
    public void OtherBusTargetedDuck_leaves_the_fixed_path_alone()
    {
        // The other way of the both-way pin: folding "sfx" in must not extend to buses a source
        // does not target - a duck aimed at "ambience" scales only that bus, fixed path stays.
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

        sfx.Play("hit");
        Assert.Equal(1f, backend.Played[0].Volume, 2);

        music.SetDuck("dialogue", true);
        music.Update(1f);
        sfx.Play("hit");
        Assert.Equal(1f, backend.Played[1].Volume, 2);
    }
}
