using System;
using System.Collections.Generic;
using System.Linq;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins positional playback in <see cref="SfxManager"/>: the listener seam (ADR-11: primitives
/// only), distance gain composed after bus resolution (so the mute floor releases the voice
/// instead of consuming a limiter slot), pan handed to the additive positional seam keyed by
/// voice handle, the mono-centered fallback for backends without the seam, and live re-mix of
/// moving sources as the listener or source moves.
/// </summary>
public class SfxPositionalTests
{
    private sealed class FakePositionalBackend : ISfxPositionalBackend
    {
        public record StartedVoice(int Id, string Cue, float Pan, float Pitch, float Volume);

        public readonly List<StartedVoice> Started = new();
        public readonly List<(object Handle, float Volume, float Pan)> Remixed = new();
        public bool Result = true;
        private int _nextId;

        public bool Play(string cueName, float pitch, float volume) => PlayTrackedAt(cueName, 0f, pitch, volume) is not null;

        public object? PlayTracked(string cueName, float pitch = 0f, float volume = 1f) =>
            PlayTrackedAt(cueName, 0f, pitch, volume);

        public object? PlayTrackedAt(string cueName, float pan, float pitch = 0f, float volume = 1f)
        {
            if (!Result) return null;
            var id = ++_nextId;
            Started.Add(new StartedVoice(id, cueName, pan, pitch, volume));
            return id;
        }

        public void SetVoiceMix(object handle, float volume, float pan) =>
            Remixed.Add((handle, volume, pan));

        public void StopVoice(object handle) { }
        public void Stop() { }
    }

    /// <summary>Voice control without the positional seam: the additive-fallback target.</summary>
    private sealed class FakeVoiceControlBackend : ISfxBackend, ISfxVoiceControlBackend
    {
        public readonly List<(string Cue, float Pitch, float Volume)> Started = new();

        public bool Play(string cueName, float pitch, float volume) => PlayTracked(cueName, pitch, volume) is not null;

        public object? PlayTracked(string cueName, float pitch = 0f, float volume = 1f)
        {
            Started.Add((cueName, pitch, volume));
            return Started.Count;
        }

        public void StopVoice(object handle) { }
        public void Stop() { }
    }

    private sealed class FakePlainBackend : ISfxBackend
    {
        public readonly List<(string Cue, float Pitch, float Volume)> Played = new();

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

    private static MusicManager CreateDuckingMusicManager()
    {
        var music = new MusicConfig { CrossfadeSeconds = 0f };
        music.DuckSources["sfx"] = new DuckSourceConfig
        {
            Ratio = 0.3f,
            AttackSeconds = 0f,
            HoldSeconds = 10f,
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

    private static SfxManager CreateManager(
        ISfxBackend backend,
        SpatialConfig? spatial = null,
        VoiceLimitConfig? voiceLimits = null)
    {
        var sfxConfig = new SfxConfig();
        if (spatial is not null) sfxConfig.Spatial = spatial;
        if (voiceLimits is not null) sfxConfig.VoiceLimits = voiceLimits;
        return new SfxManager(Library, backend, CreateDuckingMusicManager(), sfxConfig: sfxConfig);
    }

    [Fact]
    public void A_positional_play_reaches_the_seam_with_pan_and_the_distance_attenuated_volume()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { PanSaturation = 0.7f, PanDistance = 12f });

        sfx.SetListener(0f, 0f);
        // Source 12 units to the right and 0.5 units away: pan = 0.7*tanh(1), gain is the
        // distance term for ~12.01 units under the default inverse model (dref 1, rolloff 1).
        sfx.PlayAt("hit", 12f, 0.5f);

        var voice = Assert.Single(backend.Started);
        Assert.Equal("hit", voice.Cue);
        Assert.Equal(0.7f * MathF.Tanh(1f), voice.Pan, 4);
        Assert.Equal(1f / (1f + (float)Math.Sqrt(12f * 12f + 0.5f * 0.5f) - 1f), voice.Volume, 3);
        Assert.Equal(0f, voice.Pitch); // natural pitch by default, untouched by position
    }

    [Fact]
    public void A_source_within_the_reference_distance_plays_full_volume()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { ReferenceDistance = 8f });

        sfx.SetListener(2f, 2f);
        sfx.PlayAt("hit", 3f, 3f);

        var voice = Assert.Single(backend.Started);
        Assert.Equal(1f, voice.Volume);
        // Offset is exactly one unit right: pan = sat * tanh(1 / panDistance).
        Assert.Equal(0.7f * MathF.Tanh(1f / 12f), voice.Pan, 4);
    }

    [Fact]
    public void The_distance_gain_composes_after_the_bus_chain()
    {
        var backend = new FakePositionalBackend();
        // Half master, half sfx bus: the positional distance gain multiplies on top of both.
        var sfx = CreateManager(backend, spatial: new SpatialConfig { ReferenceDistance = 8f });
        sfx.MasterVolume = 0.5f;
        sfx.SfxBusVolume = 0.5f;

        sfx.SetListener(0f, 0f);
        sfx.PlayAt("hit", 0f, 4f);

        var voice = Assert.Single(backend.Started);
        Assert.Equal(0.25f, voice.Volume, 4);
    }

    [Fact]
    public void The_mute_floor_reports_the_play_and_consumes_no_voice_slot()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(
            backend,
            spatial: new SpatialConfig { MaxDistance = 10f },
            voiceLimits: new VoiceLimitConfig { GlobalMaxVoices = 1, DefaultVoiceSeconds = 0.5f });

        var zeroFired = new List<string>();
        sfx.ZeroEffectiveGain += zeroFired.Add;
        sfx.SetListener(0f, 0f);
        var handle = sfx.PlayAt("hit", 0f, 100f);

        Assert.Null(handle);
        Assert.Empty(backend.Started); // the silent play never reaches the backend
        Assert.Equal(0, sfx.ActiveVoiceCount); // and consumes no limiter slot
        Assert.Equal("hit", Assert.Single(zeroFired));
        Assert.Equal(SfxPlayResult.Played, sfx.LastPlayResult); // reported, not a refusal

        // The slot is free for a real cue.
        Assert.NotNull(sfx.PlayAt("hit", 0f, 1f));
        Assert.Equal(1, sfx.ActiveVoiceCount);
    }

    [Fact]
    public void An_unknown_cue_reports_refusal_and_returns_no_handle()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend);

        sfx.SetListener(0f, 0f);
        var voice = sfx.PlayAt("nope", 0f, 0f);

        Assert.Null(voice);
        Assert.Empty(backend.Started);
        Assert.Equal(SfxPlayResult.UnknownCue, sfx.LastPlayResult);
    }

    [Fact]
    public void A_backend_without_the_positional_seam_plays_composed_volume_centered()
    {
        var plain = new FakePlainBackend();
        var sfx = CreateManager(plain); // default spatial: reference 1, inverse, max 32
        sfx.SetListener(0f, 0f);

        var voice = sfx.PlayAt("hit", 5f, 0f); // distance 5: inverse gain = 1/(1+4)

        Assert.Null(voice); // nothing to remix: the backend hands out no handles
        var played = Assert.Single(plain.Played);
        Assert.Equal(0.2f, played.Volume, 4); // distance gain applied
        Assert.Equal(0f, played.Pitch);
    }

    [Fact]
    public void A_voice_control_backend_without_the_positional_seam_still_gets_tracked_voices()
    {
        var backend = new FakeVoiceControlBackend();
        var sfx = CreateManager(backend); // default spatial: reference 1, inverse, max 32
        sfx.SetListener(0f, 0f);

        var voice = sfx.PlayAt("hit", 5f, 0f);

        Assert.Null(voice); // no positional seam: no remixable spatial voice is handed out
        var started = Assert.Single(backend.Started);
        Assert.Equal(0.2f, started.Volume, 4);
    }

    [Fact]
    public void SetPosition_remixes_the_same_voice_handle_with_the_new_pan_and_gain()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { PanDistance = 1f });

        sfx.SetListener(0f, 0f);
        var voice = sfx.PlayAt("hit", 0f, 0.5f);
        Assert.NotNull(voice);

        // Walk the source to the pan distance on the left, still inside the reference
        // distance: the mix moves, the underlying voice does not.
        var started = backend.Started.Single();
        voice!.SetPosition(-1f, 0f);

        var remix = Assert.Single(backend.Remixed);
        Assert.Equal(started.Id, Assert.IsType<int>(remix.Handle));
        Assert.Equal(-0.7f * MathF.Tanh(1f), remix.Pan, 4);
        Assert.Equal(1f, remix.Volume, 4); // distance 1 is at the reference: full gain
    }

    [Fact]
    public void Two_overlapping_voices_of_the_same_cue_are_remixed_independently()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { PanDistance = 1f });

        sfx.SetListener(0f, 0f);
        var first = sfx.PlayAt("hit", -1f, 0f);
        var second = sfx.PlayAt("hit", 1f, 0f);
        Assert.NotSame(first, second);

        first!.SetPosition(1f, 0f); // only the first voice moves to the right

        var remix = Assert.Single(backend.Remixed); // the second voice is never touched
        Assert.Equal(backend.Started[0].Id, Assert.IsType<int>(remix.Handle));
        Assert.Equal(0.7f * MathF.Tanh(1f), remix.Pan, 4);
    }

    [Fact]
    public void SetListener_remixes_every_live_voice_around_the_new_position()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { ReferenceDistance = 8f, PanDistance = 12f });

        sfx.SetListener(0f, 0f);
        sfx.PlayAt("hit", 4f, 0f);
        var started = backend.Started.Single();

        sfx.SetListener(4f, 0f); // listener walks onto the source

        var remix = Assert.Single(backend.Remixed);
        Assert.Equal(started.Id, Assert.IsType<int>(remix.Handle));
        Assert.Equal(1f, remix.Volume, 4); // on top of the listener: full volume
        Assert.Equal(0f, remix.Pan, 4); // dead center
    }

    [Fact]
    public void Listener_readback_reports_the_set_position_and_defaults_to_origin()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend);

        Assert.Equal(0f, sfx.ListenerPosition.X);
        Assert.Equal(0f, sfx.ListenerPosition.Y);

        sfx.SetListener(3.5f, -2f);
        Assert.Equal(3.5f, sfx.ListenerPosition.X);
        Assert.Equal(-2f, sfx.ListenerPosition.Y);
    }

    [Fact]
    public void Non_finite_positions_are_treated_as_centered_at_the_listener()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { ReferenceDistance = 1f });

        sfx.SetListener(float.NaN, float.PositiveInfinity);
        Assert.Equal(0f, sfx.ListenerPosition.X);
        Assert.Equal(0f, sfx.ListenerPosition.Y);

        sfx.PlayAt("hit", float.NaN, float.NegativeInfinity);
        var voice = Assert.Single(backend.Started);
        // Any non-finite coordinate puts the source at the listener: full gain, centered.
        Assert.Equal(1f, voice.Volume, 4);
        Assert.Equal(0f, voice.Pan, 4);
    }

    [Fact]
    public void Volume_clamps_to_one_when_the_composed_gains_exceed_full()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { ReferenceDistance = 1f });
        sfx.PlayAt("hit", 0f, 0f, volume: 1f);
        Assert.Equal(1f, backend.Started.Single().Volume, 4);
    }

    [Fact]
    public void Stop_stops_every_positional_voice_and_later_remixes_are_ignored()
    {
        var backend = new FakePositionalBackend();
        var sfx = CreateManager(backend, spatial: new SpatialConfig { ReferenceDistance = 8f });

        sfx.SetListener(0f, 0f);
        var voice = sfx.PlayAt("hit", 2f, 0f);
        Assert.NotNull(voice);

        sfx.Stop();
        voice!.Stop(); // double-stop is a no-op
        sfx.SetListener(5f, 5f);

        Assert.Empty(backend.Remixed); // no live voices left to remix
    }

    [Fact]
    public void A_manager_built_from_an_unsanitized_spatial_config_still_makes_sound()
    {
        var backend = new FakePositionalBackend();
        // Breaks every knob; the manager must sanitize defensively, not panic or mute all.
        var sfx = CreateManager(backend, spatial: new SpatialConfig
        {
            ReferenceDistance = -5f,
            MaxDistance = float.NaN,
            PanDistance = 0f,
            PanSaturation = 2f,
        });

        sfx.SetListener(0f, 0f);
        sfx.PlayAt("hit", 6f, 0f);

        var voice = Assert.Single(backend.Started);
        Assert.InRange(voice.Pan, -1f, 1f);
        Assert.InRange(voice.Volume, 0f, 1f);
    }

    [Fact]
    public void A_positional_play_ducks_the_music_like_a_plain_play()
    {
        var backend = new FakePositionalBackend();
        var music = CreateDuckingMusicManager();
        var sfx = new SfxManager(Library, backend, music, sfxConfig: new SfxConfig { Spatial = new SpatialConfig() });

        sfx.SetListener(0f, 0f);
        Assert.NotNull(sfx.PlayAt("hit", 0f, 0f));

        music.Update(0.016f);
        Assert.Equal(0.3f, music.DuckRatio, 3);
    }
}
