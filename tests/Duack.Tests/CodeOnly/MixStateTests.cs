using System;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins named mix states (issue #46): code-first authoring via
/// <see cref="AudioConfigBuilder.WithMixState"/>, instant <c>ApplyState</c>, timed
/// <c>TransitionTo</c> pinned against the one-pole smoothing formula of the shared plan,
/// transition telemetry (crossfade pattern), loud unknown-state reporting, composition order
/// (state targets are bus volumes; duck stays live on top; the player's master slider layers
/// after), and the player-settings overlay carrying authored states over.
/// </summary>
public class MixStateTests
{
    private static AudioConfig ConfigWithStates() =>
        new AudioConfigBuilder()
            .WithMixState("calm", s => s.Bus("music", 1f).Bus("ambience", 1f))
            .WithMixState("combat", s => s.Bus("music", 0.4f).Bus("ambience", 0.6f))
            .Build();

    [Fact]
    public void Builder_WithMixState_AuthorsNamedTargetVectors()
    {
        var config = ConfigWithStates();
        Assert.Equal(2, config.MixStates.Count);
        Assert.Equal(0.4f, config.MixStates["combat"].Buses["music"]);
        Assert.Equal(0.6f, config.MixStates["combat"].Buses["ambience"]);
    }

    [Fact]
    public void Build_SanitizesStateBusTargets()
    {
        var config = new AudioConfigBuilder()
            .WithMixState("hot", s => s.Bus("music", 2f).Bus("ambience", -0.5f))
            .Build();
        Assert.Equal(1f, config.MixStates["hot"].Buses["music"]);
        Assert.Equal(0f, config.MixStates["hot"].Buses["ambience"]);
    }

    [Fact]
    public void ApplyState_WritesTargetsInstantlyOnEveryMixer()
    {
        var config = ConfigWithStates();
        var music = new BusMixer(config.Buses);
        var sfx = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, music, sfx);

        manager.ApplyState("combat");

        Assert.Equal(0.4f, music.VolumeOf("music"));
        Assert.Equal(0.6f, sfx.VolumeOf("ambience"));
        Assert.Equal("combat", manager.CurrentState);
        Assert.False(manager.IsTransitioning);
    }

    [Fact]
    public void ApplyState_LeavesOmittedBusesUntouched()
    {
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        mixer.SetBus("ui", 0.8f);
        var manager = new MixStateManager(config.MixStates, mixer);

        manager.ApplyState("combat");

        Assert.Equal(0.8f, mixer.VolumeOf("ui"));
    }

    [Fact]
    public void ApplyState_UnknownName_ReportsAndTouchesNothing()
    {
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, mixer);
        string? reported = null;
        manager.UnknownState += name => reported = name;

        var applied = manager.ApplyState("bossfight");

        Assert.False(applied);
        Assert.Equal("bossfight", reported);
        Assert.Null(manager.CurrentState);
        Assert.Equal(1f, mixer.VolumeOf("music"));
    }

    [Fact]
    public void TransitionTo_EasesTowardTargetsPerOnePoleFormula()
    {
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, mixer);
        manager.ApplyState("calm");

        var started = manager.TransitionTo("combat", 2f);
        Assert.True(started);
        Assert.True(manager.IsTransitioning);
        Assert.Equal(0f, manager.ElapsedSeconds);
        Assert.Equal(2f, manager.DurationSeconds);
        Assert.Equal("calm", manager.FromState);
        Assert.Equal("combat", manager.ToState);

        // One-pole response (SHARED.md §12): g(t) = target + (g0 - target)*e^(-t/tau),
        // tau = duration / ln(1/SettleEpsilon), so the transition settles at its duration.
        var tau = 2f / MathF.Log(1f / MixStateManager.SettleEpsilon);
        var expected = 0.4f + (1f - 0.4f) * MathF.Exp(-0.5f / tau);
        manager.Update(0.5f);
        Assert.Equal(expected, mixer.VolumeOf("music"), 5);
        Assert.Equal(0.5f, manager.ElapsedSeconds, 5);
        Assert.True(manager.IsTransitioning);
    }

    [Fact]
    public void TransitionTo_IsMonotoneAndSettlesExactlyAtDuration()
    {
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, mixer);
        manager.ApplyState("calm");
        manager.TransitionTo("combat", 1f);

        var previous = mixer.VolumeOf("ambience");
        for (var t = 0f; t < 0.95f; t += 0.05f)
        {
            manager.Update(0.05f);
            var current = mixer.VolumeOf("ambience");
            Assert.True(current <= previous + 1e-6f, $"volume must not rise while easing down at {t}s");
            previous = current;
        }

        manager.Update(0.05f); // reaches the 1s mark
        Assert.Equal(0.6f, mixer.VolumeOf("ambience"));
        Assert.False(manager.IsTransitioning);
        Assert.Equal("combat", manager.CurrentState);
        Assert.Null(manager.FromState);
        Assert.Null(manager.ToState);
    }

    [Fact]
    public void TransitionTo_ZeroDuration_AppliesInstantly()
    {
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, mixer);
        manager.ApplyState("calm");

        manager.TransitionTo("combat", 0f);

        Assert.False(manager.IsTransitioning);
        Assert.Equal(0.4f, mixer.VolumeOf("music"));
        Assert.Equal("combat", manager.CurrentState);
    }

    [Fact]
    public void TransitionTo_UnknownName_ReportsAndReturnsFalse()
    {
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, mixer);
        string? reported = null;
        manager.UnknownState += name => reported = name;
        manager.ApplyState("calm");

        var started = manager.TransitionTo("bossfight", 1f);

        Assert.False(started);
        Assert.Equal("bossfight", reported);
        Assert.False(manager.IsTransitioning);
        Assert.Equal(1f, mixer.VolumeOf("music"));
    }

    [Fact]
    public void CompositionOrder_StateTargetsThenLiveDuckThenPlayerMaster()
    {
        // State targets are bus volume inputs to the same single resolver: the state writes
        // the music bus, the duck ratio is read live on top (never embedded), and the
        // player's master slider layers last as the master bus write.
        var config = ConfigWithStates();
        var duckRatio = 0.25f;
        var mixer = new BusMixer(config.Buses, target => duckRatio);
        var manager = new MixStateManager(config.MixStates, mixer);
        manager.ApplyState("combat");

        // The player's settings slider writes the master bus after the state's targets.
        mixer.SetBus(BusNames.Master, 0.5f);

        // Music composition: master -> music bus (state target) -> duck (live, unchanged in state).
        var composedGain =
            mixer.Resolve(BusNames.Master).Gain * mixer.Resolve(BusNames.Music).Gain * duckRatio;
        Assert.Equal(0.5f * 0.4f * 0.25f, composedGain, 5);
        // The state target stays a bus volume; the duck moves without touching it.
        Assert.Equal(0.4f, mixer.VolumeOf(BusNames.Music));
        duckRatio = 1f;
        Assert.Equal(0.5f * 0.4f, mixer.Resolve(BusNames.Master).Gain * mixer.Resolve(BusNames.Music).Gain, 5);
    }

    [Fact]
    public void PlayerSettings_Overlay_CarriesAuthoredMixStatesOver()
    {
        var authored = ConfigWithStates();
        var store = new InMemoryPlayerSettingsStore();
        store.Save(new PlayerAudioSettings { MasterVolume = 0.5f, MusicMasterVolume = 0.7f, SfxMasterVolume = 0.9f });

        var effective = PlayerSettings.ApplyOverrides(authored, store);

        Assert.Equal(0.4f, effective.MixStates["combat"].Buses["music"]);
        Assert.Equal(1f, authored.MixStates["calm"].Buses["ambience"]); // authored graph untouched
    }

    [Fact]
    public void MusicManager_ExposesItsBusMixerAsAStableReadback()
    {
        var backend = new FakeMusicBackend();
        var music = new MusicManager(backend, new MusicPlaylistController(new PlaylistConfig()));
        Assert.Same(music.Buses, music.Buses);
        Assert.Equal(1f, music.Buses.VolumeOf(BusNames.Master));
    }

    /// <summary>Records every call; playback state stays unused by the readback test.</summary>
    private sealed class FakeMusicBackend : IMusicBackend
    {
        public bool SupportsOverlap => true;
        public bool IsTrackPlaying { get; private set; }
        public float CurrentTrackElapsedSeconds { get; set; }
        public float? CurrentTrackDurationSeconds { get; set; }
        public event EventHandler? TrackEnded;

        public void PlayTrack(string trackName, float initialVolume = 1f, float pitch = 0f) => IsTrackPlaying = true;
        public void SetActiveVolume(float volume) { }
        public void SetOutgoingVolume(float volume) { }
        public void StopOutgoing() { }
        public void Stop() => IsTrackPlaying = false;
        public void Update(float deltaSeconds) { }

        /// <summary>Simulates the active track reaching its end, keeping the event in use.</summary>
        public void RaiseTrackEnded()
        {
            IsTrackPlaying = false;
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
