using System;
using System.Collections.Generic;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the positional (2D spatial) SFX API: <see cref="SfxManager.SetListener"/>,
/// <see cref="SfxManager.PlayAt"/> composes distance gain with the existing volume chain and
/// hands the backend a pan, the mute floor releases distant voices without a backend call, and
/// <see cref="SfxManager.UpdatePosition"/> re-mixes a moving source's active voices.
/// </summary>
public class SfxSpatialTests
{
    private sealed class FakePositionalBackend : IPositionalSfxBackend, ISfxBackend
    {
        public List<(string Cue, float Pitch, float Volume)> Played { get; } = new();
        public List<(string Cue, float Pitch, float Volume, float Pan)> PlayedPanned { get; } = new();
        public List<(string Cue, float Pan, float Volume)> Mixed { get; } = new();

        public bool Play(string cueName, float pitch, float volume)
        {
            Played.Add((cueName, pitch, volume));
            return true;
        }

        public bool Play(string cueName, float pitch, float volume, float pan)
        {
            PlayedPanned.Add((cueName, pitch, volume, pan));
            return true;
        }

        public bool SetCueMix(string cueName, float pan, float volume)
        {
            Mixed.Add((cueName, pan, volume));
            return true;
        }

        public void Stop() { }
    }

    private sealed class FakePlainBackend : ISfxBackend
    {
        public List<(string Cue, float Pitch, float Volume)> Played { get; } = new();
        public bool Play(string cueName, float pitch, float volume) { Played.Add((cueName, pitch, volume)); return true; }
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

    private static MusicManager CreateMusicManager() => new(
        new FakeMusicBackend(),
        new MusicPlaylistController(
            new PlaylistConfigBuilder().WithPlaylist("solo", p => p.Track("track", "")).Build(),
            new Random(1)),
        new MusicConfig { CrossfadeSeconds = 0f });

    // Defaults: linear model, dref = 1, dmax = 32, rolloff 1, pan distance 12, saturation 0.7,
    // sigma 0.5. Listener and source share x unless a test says otherwise, so pin one expected
    // gain: d = 2 -> 1 - (2-1)/(32-1) = 30/31.
    private const float ExpectedGainAt2 = 30f / 31f;

    [Fact]
    public void Listener_position_reads_back_what_was_set()
    {
        var sfx = new SfxManager(Library, new FakePositionalBackend(), CreateMusicManager());
        sfx.SetListener(10f, -4f);
        Assert.Equal((10f, -4f), sfx.ListenerPosition);
    }

    [Fact]
    public void PlayAt_composes_distance_gain_into_the_volume_and_keeps_the_centered_pan()
    {
        var backend = new FakePositionalBackend();
        var sfx = new SfxManager(Library, backend, CreateMusicManager());
        sfx.SetListener(10f, 10f);

        Assert.True(sfx.PlayAt("hit", sourceX: 10f, sourceY: 12f, pitch: 0.25f));

        var played = Assert.Single(backend.PlayedPanned);
        Assert.Equal("hit", played.Cue);
        Assert.Equal(0.25f, played.Pitch);
        Assert.Equal(ExpectedGainAt2, played.Volume, 4);
        Assert.Equal(0f, played.Pan, 4); // no horizontal offset: centered
    }

    [Fact]
    public void PlayAt_applies_the_saturated_pan_for_an_off_center_source()
    {
        var backend = new FakePositionalBackend();
        var sfx = new SfxManager(Library, backend, CreateMusicManager());
        sfx.SetListener(10f, 10f);

        // 12 world units right = exactly the pan distance: pan = 0.7 * tanh(1 / 0.5).
        Assert.True(sfx.PlayAt("hit", sourceX: 22f, sourceY: 10f));

        var played = Assert.Single(backend.PlayedPanned);
        Assert.Equal(0.7f * MathF.Tanh(2f), played.Pan, 4);
        // d = 12 -> linear gain 1 - 11/31, composed with volume 1.
        Assert.Equal(20f / 31f, played.Volume, 4);
    }

    [Fact]
    public void PlayAt_beyond_the_max_distance_hits_the_mute_floor_and_releases_the_voice()
    {
        var backend = new FakePositionalBackend();
        var sfx = new SfxManager(Library, backend, CreateMusicManager());
        string? reported = null;
        sfx.ZeroEffectiveGain += cue => reported = cue;
        sfx.SetListener(0f, 0f);

        Assert.True(sfx.PlayAt("hit", sourceX: 0f, sourceY: 32f));

        Assert.Empty(backend.PlayedPanned); // no voice started: nothing to release later
        Assert.Empty(backend.Played);
        Assert.Equal("hit", reported);
    }

    [Fact]
    public void PlayAt_composes_the_user_bus_gain_into_a_positional_play()
    {
        var backend = new FakePositionalBackend();
        var config = new SfxConfig();
        config.Buses["ambience"] = 0.5f;
        var sfx = new SfxManager(Library, backend, CreateMusicManager(), sfxConfig: config);
        sfx.SetListener(0f, 0f);

        Assert.True(sfx.PlayAt("hit", 0f, 2f, bus: "ambience"));

        Assert.Equal(ExpectedGainAt2 * 0.5f, Assert.Single(backend.PlayedPanned).Volume, 4);
    }

    [Fact]
    public void PlayAt_on_a_non_positional_backend_still_plays_with_the_distance_gain()
    {
        var backend = new FakePlainBackend();
        var sfx = new SfxManager(Library, backend, CreateMusicManager());
        sfx.SetListener(0f, 0f);

        Assert.True(sfx.PlayAt("hit", 0f, 2f));

        var played = Assert.Single(backend.Played);
        Assert.Equal(ExpectedGainAt2, played.Volume, 4); // pan quietly dropped, gain preserved
    }

    [Fact]
    public void UpdatePosition_re_mixes_the_active_voices_of_a_moving_source()
    {
        var backend = new FakePositionalBackend();
        var sfx = new SfxManager(Library, backend, CreateMusicManager());
        sfx.SetListener(0f, 0f);
        sfx.PlayAt("hit", 0f, 2f);

        // Source walks to the pan distance on the right: pan saturating, gain shrinking.
        sfx.UpdatePosition("hit", sourceX: 12f, sourceY: 0f);

        var mix = Assert.Single(backend.Mixed);
        Assert.Equal("hit", mix.Cue);
        Assert.Equal(0.7f * MathF.Tanh(2f), mix.Pan, 4);
        Assert.Equal(20f / 31f, mix.Volume, 4);
    }

    [Fact]
    public void UpdatePosition_respects_the_mute_floor_for_a_source_that_left_the_range()
    {
        var backend = new FakePositionalBackend();
        var sfx = new SfxManager(Library, backend, CreateMusicManager());
        sfx.SetListener(0f, 0f);
        sfx.PlayAt("hit", 0f, 2f);

        sfx.UpdatePosition("hit", sourceX: 0f, sourceY: 100f);

        var mix = Assert.Single(backend.Mixed);
        Assert.Equal(0f, mix.Volume); // muted in place; the voice fades out rather than hard-stops
    }

    [Fact]
    public void UpdatePosition_for_a_cue_the_backend_does_not_hold_still_re_mixes_once()
    {
        var backend = new FakePositionalBackend();
        var sfx = new SfxManager(Library, backend, CreateMusicManager());
        sfx.SetListener(0f, 0f);

        // Pass-through re-mix: the backend decides what "no such voice" means (a quiet no-op).
        // dx = 12 with the default pan distance 12: xNorm = 1, pan = 0.7*tanh(2).
        sfx.UpdatePosition("hit", sourceX: 12f, sourceY: 0f);

        var mix = Assert.Single(backend.Mixed);
        Assert.Equal(0.7f * MathF.Tanh(2f), mix.Pan, 4);
    }
}
