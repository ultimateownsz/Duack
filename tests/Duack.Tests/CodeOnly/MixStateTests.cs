using System;
using System.Collections.Generic;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins named mix states (issue #46, multiplicative layering #68): code-first authoring via
/// <see cref="AudioConfigBuilder.WithMixState"/>, instant <c>ApplyState</c>, timed
/// <c>TransitionTo</c> pinned against the one-pole smoothing formula of the shared plan,
/// transition telemetry (crossfade pattern), loud unknown-state reporting, composition order
/// (state factors multiply the authored bus volume; duck stays live on top; the player's
/// master slider layers after), and the player-settings overlay carrying authored states over.
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
    public void ApplyState_SetsFactorsWithoutTouchingBusVolumes()
    {
        var config = ConfigWithStates();
        var music = new BusMixer(config.Buses);
        var sfx = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, music, sfx);

        manager.ApplyState("combat");

        Assert.Equal(1f, music.VolumeOf("music"));
        Assert.Equal(0.4f, music.StateFactorOf("music"));
        Assert.Equal(0.6f, sfx.StateFactorOf("ambience"));
        Assert.Equal(0.4f, music.Resolve("music").Gain);
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
        Assert.Equal(1f, mixer.StateFactorOf("ui"));
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
        Assert.Equal(1f, mixer.StateFactorOf("music"));
    }

    [Fact]
    public void TransitionTo_EasesTheFactorTowardTargetsPerOnePoleFormula()
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
        Assert.Equal(expected, mixer.StateFactorOf("music"), 5);
        // The authored bus volume never moves: the state is a multiplicative layer.
        Assert.Equal(1f, mixer.VolumeOf("music"));
        Assert.Equal(0.5f, manager.ElapsedSeconds, 5);
        Assert.True(manager.IsTransitioning);
    }

    [Fact]
    public void TransitionTo_SliderWriteDuringTransitionSurvives()
    {
        // The old contract raced: the state layer's per-frame SetBus writes and the player's
        // settings slider wrote the same number (last writer wins). Factors live in separate
        // state, so a slider write mid-transition keeps its value and composes multiplicatively
        // with the eased factor (#68).
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, mixer);
        manager.ApplyState("calm");
        Assert.True(manager.TransitionTo("combat", 2f));

        mixer.SetBus("music", 0.3f); // the player drags the slider mid-transition
        manager.Update(0.5f);

        var tau = 2f / MathF.Log(1f / MixStateManager.SettleEpsilon);
        var expectedFactor = 0.4f + (1f - 0.4f) * MathF.Exp(-0.5f / tau);
        Assert.Equal(0.3f, mixer.VolumeOf("music"));
        Assert.Equal(expectedFactor, mixer.StateFactorOf("music"), 5);
        Assert.Equal(0.3f * expectedFactor, mixer.Resolve("music").Gain, 5);
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

        var previous = mixer.StateFactorOf("ambience");
        for (var t = 0f; t < 0.95f; t += 0.05f)
        {
            manager.Update(0.05f);
            var current = mixer.StateFactorOf("ambience");
            Assert.True(current <= previous + 1e-6f, $"factor must not rise while easing down at {t}s");
            previous = current;
        }

        manager.Update(0.05f); // reaches the 1s mark
        Assert.Equal(0.6f, mixer.StateFactorOf("ambience"));
        Assert.False(manager.IsTransitioning);
        Assert.Equal("combat", manager.CurrentState);
        Assert.Null(manager.FromState);
        Assert.Null(manager.ToState);
    }

    [Fact]
    public void TransitionTo_EasesEachMixerFromItsOwnCurrentFactor()
    {
        // A real project (monsterprojectweb) drives TWO mixers and diverges them on purpose:
        // the game's settings push writes the player's volumes onto one mixer each. The
        // mix-state FACTORS are per mixer too: a transition targeting sfx must ease EACH
        // mixer's factor from ITS OWN current factor, not a captured shared one.
        var config = new AudioConfigBuilder()
            .WithMixState("combat", s => s.Bus(BusNames.Music, 0.4f).Bus(BusNames.Sfx, 0.7f))
            .Build();
        var music = new BusMixer(config.Buses);
        var sfx = new BusMixer(config.Buses);
        sfx.SetStateFactor(BusNames.Sfx, 0.2f);
        var manager = new MixStateManager(config.MixStates, music, sfx);

        Assert.True(manager.TransitionTo("combat", 1f));
        manager.Update(0.25f);

        var tau = 1f / MathF.Log(1f / MixStateManager.SettleEpsilon);
        var expectedMusic = 0.7f + (1f - 0.7f) * MathF.Exp(-0.25f / tau);
        var expectedSfx = 0.7f + (0.2f - 0.7f) * MathF.Exp(-0.25f / tau);
        Assert.Equal(expectedMusic, music.StateFactorOf(BusNames.Sfx), 5);
        Assert.Equal(expectedSfx, sfx.StateFactorOf(BusNames.Sfx), 5);
        Assert.Equal(1f, music.VolumeOf(BusNames.Sfx));
        Assert.Equal(1f, sfx.VolumeOf(BusNames.Sfx));
    }

    [Fact]
    public void TransitionTo_InterruptingAnInFlightTransitionReportsTheInterruptedTargetAsFrom()
    {
        var config = ConfigWithStates();
        var mixer = new BusMixer(config.Buses);
        var manager = new MixStateManager(config.MixStates, mixer);
        manager.ApplyState("calm");
        manager.TransitionTo("combat", 2f);

        manager.TransitionTo("calm", 1f);

        Assert.True(manager.IsTransitioning);
        Assert.Equal("combat", manager.FromState);
        Assert.Equal("calm", manager.ToState);
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
        Assert.Equal(0.4f, mixer.StateFactorOf("music"));
        Assert.Equal(1f, mixer.VolumeOf("music"));
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
        Assert.Equal(1f, mixer.StateFactorOf("music"));
    }

    [Fact]
    public void CompositionOrder_StateFactorThenLiveDuckThenPlayerMaster()
    {
        // The state factor multiplies the authored bus volume inside the resolver; the duck
        // ratio stays live on top (never embedded in bus state), and the player's master
        // slider layers last as the master bus write.
        var config = ConfigWithStates();
        var duckRatio = 0.25f;
        var mixer = new BusMixer(config.Buses, target => duckRatio);
        var manager = new MixStateManager(config.MixStates, mixer);
        manager.ApplyState("combat");

        // The player's settings slider writes the master bus after the state's factor.
        mixer.SetBus(BusNames.Master, 0.5f);

        // Music composition: master -> music bus (authored 1f) -> state factor -> duck.
        var composedGain =
            mixer.Resolve(BusNames.Master).Gain * mixer.Resolve(BusNames.Music).Gain * duckRatio;
        Assert.Equal(0.5f * 1f * 0.4f * 0.25f, composedGain, 5);
        Assert.Equal(1f, mixer.VolumeOf(BusNames.Music));
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
        public bool SupportsOverlap { get; }
        public List<(string Name, float InitialVolume, float Pitch)> Played { get; } = new();
        public List<float> ActiveVolumes { get; } = new();
        public List<float> OutgoingVolumes { get; } = new();
        public int StopOutgoingCalls { get; private set; }
        public int StopCalls { get; private set; }
        public bool IsTrackPlaying { get; private set; }
        public float CurrentTrackElapsedSeconds { get; set; }
        public float? CurrentTrackDurationSeconds { get; set; }
        public bool IsPaused { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
        public bool SupportsSeek { get; set; }
        public event EventHandler? TrackEnded;

        public void PlayTrack(string trackName, float initialVolume = 1f, float pitch = 0f)
        {
            Played.Add((trackName, initialVolume, pitch));
            IsTrackPlaying = true;
            CurrentTrackElapsedSeconds = 0f;
        }

        public void SetActiveVolume(float volume) => ActiveVolumes.Add(volume);
        public void SetOutgoingVolume(float volume) => OutgoingVolumes.Add(volume);
        public void StopOutgoing() => StopOutgoingCalls++;
        public void Stop()
        {
            StopCalls++;
            IsTrackPlaying = false;
        }

        public void Seek(TimeSpan position) { }
        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
        public void Update(float deltaSeconds) { }

        /// <summary>Simulates the active track reaching its end (completion detection).</summary>
        public void RaiseTrackEnded()
        {
            IsTrackPlaying = false;
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}
