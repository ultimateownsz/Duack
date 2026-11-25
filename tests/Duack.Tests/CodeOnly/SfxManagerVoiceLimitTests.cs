using Duack;

namespace Duack.Tests.CodeOnly;

/// <summary>Voice-limit integration: SfxManager enforces authored caps and reports every refusal.</summary>
public class SfxManagerVoiceLimitTests
{
    private sealed class FakeSfxBackend : ISfxBackend
    {
        public List<(string Cue, float Pitch, float Volume)> Played { get; } = new();
        public bool Result { get; set; } = true;

        public bool Play(string cueName, float pitch, float volume)
        {
            Played.Add((cueName, pitch, volume));
            return Result;
        }

        public void Stop() { }
    }

    private sealed class FakeMusicBackend : IMusicBackend
    {
        public bool SupportsOverlap => true;
        public bool IsTrackPlaying => false;
        public float CurrentTrackElapsedSeconds { get; set; }
        public float? CurrentTrackDurationSeconds { get; set; }
        public event EventHandler? TrackEnded { add { } remove { } }
        public void PlayTrack(string trackName, float initialVolume, float pitch) { }
        public void SetActiveVolume(float volume) { }
        public void SetOutgoingVolume(float volume) { }
        public void StopOutgoing() { }
        public void Stop() { }
        public void Update(float deltaSeconds) { }
    }

    private sealed class FakeVoiceControlBackend : ISfxBackend, ISfxVoiceControlBackend
    {
        public List<(string Cue, float Pitch, float Volume)> Played { get; } = new();
        public List<object> StoppedVoices { get; } = new();
        public bool Result { get; set; } = true;

        public bool Play(string cueName, float pitch, float volume)
        {
            Played.Add((cueName, pitch, volume));
            return Result;
        }

        public object? PlayTracked(string cueName, float pitch, float volume)
        {
            Played.Add((cueName, pitch, volume));
            return Result ? new object() : null;
        }

        public void StopVoice(object handle) => StoppedVoices.Add(handle);

        public void Stop() { }
    }

    [Fact]
    public void A_stolen_voice_is_stopped_through_the_voice_control_seam()
    {
        var backend = new FakeVoiceControlBackend();
        var sfx = new SfxManager(Library, backend,
            new MusicManager(new FakeMusicBackend(),
                new MusicPlaylistController(new PlaylistConfigBuilder().Build(), new Random(1)),
                new MusicConfig()),
            voiceLimits: new VoiceLimitConfig { GlobalMaxVoices = 1, DefaultVoiceSeconds = 10f });

        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit"));
        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("dialog"));

        Assert.Equal(2, backend.Played.Count);
        var stolen = Assert.Single(backend.StoppedVoices);
        Assert.Equal(1, sfx.ActiveVoiceCount); // the steal freed the victim's slot, then re-reserved it with the new voice
    }

    [Fact]
    public void A_slot_only_stealing_fallback_reports_the_steal_without_stopping_anything()
    {
        var backend = new FakeSfxBackend(); // does not implement the control seam
        var sfx = CreateManager(backend, new VoiceLimitConfig { GlobalMaxVoices = 1, DefaultVoiceSeconds = 10f });

        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit"));
        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("dialog"));

        Assert.Equal(2, backend.Played.Count); // fallback keeps one backend call per play
        Assert.Equal(1, sfx.ActiveVoiceCount);
    }

    private static readonly AudioLibrary Library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav", "SFX/dialog.wav" });

    private static SfxManager CreateManager(ISfxBackend backend, VoiceLimitConfig? limits = null) =>
        new(Library, backend,
            new MusicManager(new FakeMusicBackend(),
                new MusicPlaylistController(new PlaylistConfigBuilder().Build(), new Random(1)),
                new MusicConfig()),
            voiceLimits: limits);

    [Fact]
    public void Global_cap_steals_and_active_voice_count_is_readable()
    {
        var backend = new FakeSfxBackend();
        var sfx = CreateManager(backend, new VoiceLimitConfig { GlobalMaxVoices = 2, DefaultVoiceSeconds = 10f });

        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit"));
        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit"));
        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit"));

        Assert.Equal(2, sfx.ActiveVoiceCount);
        Assert.Equal(3, backend.Played.Count);
    }

    [Fact]
    public void Priority_refusal_does_not_reach_the_backend_and_is_reported()
    {
        var backend = new FakeSfxBackend();
        var sfx = CreateManager(backend, new VoiceLimitConfig
        {
            GlobalMaxVoices = 1,
            DefaultVoiceSeconds = 10f,
            DefaultStealingPolicy = VoiceStealingPolicy.Priority,
        });

        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit", priority: 10));
        var refused = sfx.PlayDetailed("dialog", priority: 1);

        Assert.Equal(SfxPlayResult.RejectedBelowPriority, refused);
        Assert.Equal(SfxPlayResult.RejectedBelowPriority, sfx.LastPlayResult);
        Assert.Single(backend.Played);
    }

    [Fact]
    public void A_backend_refusal_releases_the_reserved_voice()
    {
        var backend = new FakeSfxBackend { Result = false };
        var sfx = CreateManager(backend, new VoiceLimitConfig { GlobalMaxVoices = 1 });

        Assert.Equal(SfxPlayResult.BackendRefused, sfx.PlayDetailed("hit"));
        Assert.Equal(0, sfx.ActiveVoiceCount);
    }

    [Fact]
    public void Update_ages_voices_and_frees_slots()
    {
        var backend = new FakeSfxBackend();
        var sfx = CreateManager(backend, new VoiceLimitConfig
        {
            GlobalMaxVoices = 1,
            DefaultVoiceSeconds = 0.5f,
        });

        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit"));
        sfx.Update(0.51f);
        Assert.Equal(0, sfx.ActiveVoiceCount);
        Assert.Equal(SfxPlayResult.Played, sfx.PlayDetailed("hit"));
    }

    [Fact]
    public void An_unknown_cue_reports_its_failure_without_touching_the_limiter()
    {
        var backend = new FakeSfxBackend();
        var sfx = CreateManager(backend, new VoiceLimitConfig { GlobalMaxVoices = 1 });

        Assert.Equal(SfxPlayResult.UnknownCue, sfx.PlayDetailed("explode"));
        Assert.Empty(backend.Played);
        Assert.Equal(0, sfx.ActiveVoiceCount);
    }

    [Fact]
    public void AudioConfigBuilder_authors_global_and_per_cue_voice_limits()
    {
        var config = new AudioConfigBuilder()
            .WithVoiceLimit(8, defaultVoiceSeconds: 1.5f)
            .WithCueVoiceLimit("hit", new CueVoiceLimit { MaxVoices = 2, Priority = 7 })
            .Build();

        Assert.Equal(8, config.Sfx.VoiceLimits!.GlobalMaxVoices);
        Assert.Equal(1.5f, config.Sfx.VoiceLimits.DefaultVoiceSeconds);
        Assert.Equal(2, config.Sfx.VoiceLimits.CueLimits["hit"].MaxVoices);
        Assert.Equal(7, config.Sfx.VoiceLimits.CueLimits["hit"].Priority);
    }
}
