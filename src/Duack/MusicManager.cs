using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// Core music manager: owns playlist sequencing, crossfade realization, ducking, context
/// scales, and master-volume composition — the final volume the backend plays at is
/// <see cref="MusicVolumeMath.Effective"/> of (master × context scale × duck ratio × fade).
/// It talks to a thin <see cref="IMusicBackend"/> that only plays tracks and applies
/// volumes, so both the MonoGame and KNI adapters stay mechanical.
///
/// <para>
/// Two crossfade models, both realized here: overlap-capable backends (desktop) demote the
/// playing track to "outgoing" when <see cref="IMusicBackend.PlayTrack"/> is called and both
/// volumes follow the equal-power curve (<see cref="CrossfadeMath"/>); non-overlap backends
/// (web, one <c>MediaPlayer</c>) run <see cref="MusicTransitionSequencer"/> — fade the
/// current track out, switch, fade the new track in — touching only the active volume.
/// </para>
///
/// <para>
/// Completion is adapter-reported: the backend fires <see cref="IMusicBackend.TrackEnded"/>
/// when the active track finishes and the director advances the playlist through
/// <see cref="MusicPlaylistController.NotifyTrackCompleted"/>.
/// </para>
/// </summary>
public class MusicManager
{
    private readonly IMusicBackend _backend;
    private readonly MusicPlaylistController _playlist;
    private readonly MusicConfig _music;
    private readonly Random _random;
    private readonly DuckMixer _duck;
    private readonly BusMixer _buses;
    private readonly MusicTransitionSequencer _sequencer = new();

    private PlaylistTrack? _active;
    private PlaylistTrack? _outgoing;
    private PlaylistTrack? _pending;
    private PlaylistTrack? _transitionFrom;
    private bool _isCrossfading;
    private float _crossfadeElapsed;

    private bool _stoppingWithFade;
    private float _stopFadeElapsed;
    private float _stopFadeDuration;
    private float? _fadeInDurationOverride;
    private float? _fadeOutDurationOverride;
    private float _transitionDuration;

    /// <summary>
    /// The overlap crossfade's fast outgoing fade: the outgoing track fades out FASTER than the
    /// incoming one fades in (pinned by
    /// <c>MusicManagerFadeTests.Overlap_transition_fades_out_the_prior_track_faster_than_it_fades_in_the_new_one</c>),
    /// so it clears the mix while the new track is still rising.
    /// </summary>
    private const float FastFadeOutSeconds = 0.2f;

    private float _fadeOutDuration = FastFadeOutSeconds;

    /// <summary>Raised after the playlist advanced: carries the track that just completed.</summary>
    public event Action<PlaylistTrack>? TrackCompleted;

    /// <summary>Combined duck ratio the music is currently multiplied by (1 = unducked).</summary>
    public float DuckRatio => _duck.EffectiveRatio;

    /// <summary>
    /// The combined duck ratio currently aimed at <paramref name="target"/>: <c>music</c> equals
    /// <see cref="DuckRatio"/>; a bus target composes only the sources aimed at that bus (1 when
    /// none are active).
    /// </summary>
    public float DuckRatioFor(string target) => _duck.RatioFor(target);

    /// <summary>Combined gain reduction / attenuation currently applied to music (0 = no reduction, 1 = full silence).</summary>
    public float GainReduction => _duck.GainReduction;

    /// <summary>Alias for <see cref="GainReduction"/>.</summary>
    public float Attenuation => GainReduction;

    /// <summary>True while a track transition is being realized.</summary>
    public bool IsTransitioning => _isCrossfading;

    /// <summary>Elapsed time in the current transition, including both web fade phases.</summary>
    public float TransitionElapsedSeconds
    {
        get
        {
            if (!_isCrossfading)
                return 0f;

            if (_backend.SupportsOverlap)
                return _crossfadeElapsed;

            var fadeOut = _fadeOutDuration;
            var phaseElapsed = _sequencer.Progress * (_sequencer.Phase == MusicFadePhase.FadingOut ? fadeOut : _transitionDuration);
            return _sequencer.Phase == MusicFadePhase.FadingOut
                ? phaseElapsed
                : fadeOut + phaseElapsed;
        }
    }

    /// <summary>Total duration of the current transition.</summary>
    public float TransitionDurationSeconds =>
        !_isCrossfading ? 0f : _backend.SupportsOverlap ? _transitionDuration : _fadeOutDuration + _transitionDuration;

    /// <summary>Track being faded out, if a transition is active.</summary>
    public string? TransitionFromTrack => _outgoing?.Name ?? (_isCrossfading ? (_transitionFrom ?? _active)?.Name : null);

    /// <summary>Track being faded in, if a transition is active.</summary>
    public string? TransitionToTrack => _pending?.Name ?? (_isCrossfading ? _active?.Name : null);

    /// <summary>
    /// Read-only snapshot of every duck source for diagnostics (live per-source ratios and targets).
    /// </summary>
    public IReadOnlyList<DuckSourceView> DuckSources => _duck.Sources;

    /// <summary>
    /// Runtime state and routing for the one bus table this manager resolves from — the same
    /// readback-and-writer surface <see cref="SfxManager.Buses"/> exposes, so a
    /// <see cref="MixStateManager"/> drives both mixers of the mix.
    /// </summary>
    public BusMixer Buses => _buses;

    /// <summary>
    /// Master bus volume (scales music and SFX alike), read live every frame so a settings
    /// slider applies without rebuilding the director; defaults to the constructor argument.
    /// A thin delegating writer into the mixer's <see cref="BusNames.Master"/> entry — the
    /// same storage the music bus resolves from.
    /// </summary>
    public float MasterVolume
    {
        get => _buses.VolumeOf(BusNames.Master);
        set => _buses.SetBus(BusNames.Master, value);
    }

    /// <param name="backend">The platform backend that actually plays tracks.</param>
    /// <param name="playlist">The playlist sequencer to drive.</param>
    /// <param name="music">Music mix settings: master, crossfade, contexts, ducks, cue variation.</param>
    /// <param name="random">Random source for cue variation; defaults to <see cref="Random.Shared"/>.</param>
    /// <param name="masterVolume">Master bus volume (scales music and SFX alike); defaults to full volume.</param>
    public MusicManager(IMusicBackend backend, MusicPlaylistController playlist, MusicConfig? music = null, Random? random = null, float masterVolume = 1f)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
        _music = music ?? new MusicConfig();
        // One resolver: the master and music buses resolve through the same mixer chain the
        // SFX path uses, seeded from the same flat table the config carries (D7: the only
        // constructor is unchanged; the table rides in on the existing MusicConfig transport).
        _buses = new BusMixer(_music.Buses);
        _transitionDuration = _music.CrossfadeSeconds;
        MasterVolume = masterVolume;
        _random = random ?? Random.Shared;
        _duck = new DuckMixer();
        foreach (var (name, source) in _music.DuckSources)
            _duck.SetSource(name, source);
        _playlist.TrackCompleted += track => TrackCompleted?.Invoke(track);
        _backend.TrackEnded += OnBackendTrackEnded;
    }

    /// <summary>True while a playlist is running or the backend is still producing sound.</summary>
    public bool IsPlaying => _playlist.IsPlaying || _backend.IsTrackPlaying;

    /// <summary>The track the playlist currently names (what the backend should be playing).</summary>
    public PlaylistTrack? CurrentTrack => _playlist.CurrentTrack;

    /// <summary>
    /// The name of the playlist the controller is running, or null when stopped. The re-trigger guard
    /// should compare against this — <see cref="PlaylistTrack.Name"/> is a track name and only equals
    /// the playlist name for a single-track playlist named after its track.
    /// </summary>
    public string? CurrentPlaylistName => _playlist.CurrentPlaylistName;

    /// <summary>
    /// Raised when <see cref="Play"/> names a playlist that cannot play (unknown name or no tracks),
    /// carrying the requested name. Without it a typo silently stops the running music; the report
    /// mirrors the SFX path's <see cref="BusMixer.BusFellBack"/>. Forwarded from
    /// <see cref="MusicPlaylistController.PlaylistFellBack"/>.
    /// </summary>
    public event Action<string>? PlaylistFellBack
    {
        add => _playlist.PlaylistFellBack += value;
        remove => _playlist.PlaylistFellBack -= value;
    }

    /// <summary>Seconds since the current track started (0 when nothing is playing).</summary>
    public float CurrentTrackElapsedSeconds => _backend.IsTrackPlaying ? _backend.CurrentTrackElapsedSeconds : 0f;

    /// <summary>Total length of the current track in seconds, or null when unknown.</summary>
    public float? CurrentTrackDurationSeconds => _backend.CurrentTrackDurationSeconds;

    /// <summary>Starts <paramref name="playlistName"/> from the top.</summary>
    /// <param name="playlistName">Name of the playlist to play.</param>
    /// <param name="fadeInSeconds">Fade in duration in seconds. When given it outranks the
    /// track's authored <see cref="PlaylistTrack.FadeInSeconds"/>; when null or omitted, the track
    /// fade applies and falls back to <see cref="AudioConfig.Music"/>.<c>CrossfadeSeconds</c>.</param>
    public void Play(string playlistName, float? fadeInSeconds = null)
    {
        _stoppingWithFade = false;
        _stopFadeElapsed = 0f;
        _stopFadeDuration = 0f;

        // A call-level fade is the consumer's runtime intent and outranks the track's authored
        // FadeInSeconds; assigning unconditionally also drops any stale call fade from an earlier
        // Play. When the call omits it, null lets the track fade apply in Update.
        _fadeInDurationOverride = fadeInSeconds;

        _playlist.Play(playlistName);
        // Immediately trigger the transition so the crossfade starts on this frame,
        // not on the next Update() call.
        Update(0f);

        // A call-level fade belongs to this call only: consumed inside Update if a transition
        // began, discarded otherwise, so it never leaks into a later transition.
        _fadeInDurationOverride = null;

        // If this is the first track (or starting fresh), force a fade-in from silence
        if (_backend.SupportsOverlap)
        {
            if (_active != null && !_isCrossfading)
            {
                // Ensure it starts from silent if a fade-in is requested
                if (fadeInSeconds.HasValue && fadeInSeconds.Value > 0f)
                {
                    _sequencer.BeginFadeIn();
                    _isCrossfading = true;
                    _crossfadeElapsed = 0f;
                    // Override by controlling via sequencer state is not directly possible;
                    // but for overlap backends we can simulate by starting volumes low and fading up
                    // Easier: use sequencer for initial fade in even on overlap? Or just start at 0 and let normal crossfade logic handle
                    // Actually better: reset and let BeginTransition with fade-in behavior
                }
            }
        }
        else
        {
            if (_backend.IsTrackPlaying == false && _active != null)
            {
                // First track fade in
                if (fadeInSeconds.HasValue && fadeInSeconds.Value > 0f)
                {
                    _sequencer.Cancel();
                    _sequencer.BeginFadeIn();
                    _isCrossfading = true;
                    if (_active != null)
                        _backend.SetActiveVolume(Compose(_sequencer.InGain, _active.Context));
                }
            }
        }
    }

    /// <summary>Skips to the next track (honours the playlist's repeat mode).</summary>
    public void Next() => _playlist.Next();

    /// <summary>Ducks the named source for its configured hold (or <paramref name="holdSeconds"/>).</summary>
    public void Pulse(string source, float holdSeconds = -1f) => _duck.Pulse(source, holdSeconds);

    /// <summary>
    /// Re-applies the duck sources currently in <see cref="MusicConfig.DuckSources"/> (e.g. after a
    /// hot reload replaced a <see cref="DuckSourceConfig"/>). Live active/pulse state survives, so a
    /// dip in progress keeps easing instead of snapping back to full volume.
    /// </summary>
    public void ReloadDuckSources() => _duck.ReloadSources(_music.DuckSources);

    /// <summary>Immediately releases all duck sources (no easing tail) - used on scene switches.</summary>
    public void ReleaseDucks() => _duck.ReleaseAll();

    /// <summary>True when the combined duck ratio has recovered to at least <paramref name="threshold"/>.</summary>
    public bool IsDuckReady(float threshold = 0.99f) => _duck.IsReady(threshold);

    /// <summary>Holds a duck source on or off (e.g. dialogue open/close).</summary>
    public void SetDuck(string source, bool active) => _duck.Set(source, active);

    /// <summary>
    /// Begins a ref-counted duck hold on the named source (see <see cref="DuckMixer.BeginDuck"/>):
    /// overlapping callers each hold once and the source releases only when the last hold ends.
    /// </summary>
    public void BeginDuck(string source) => _duck.BeginDuck(source);

    /// <summary>Ends one <see cref="BeginDuck"/> hold (see <see cref="DuckMixer.EndDuck"/>).</summary>
    public void EndDuck(string source) => _duck.EndDuck(source);

    /// <summary>Stops playback and clears all transition state.</summary>
    /// <param name="fadeOutSeconds">Optional fade out duration in seconds.</param>
    public void Stop(float? fadeOutSeconds = null)
    {
        var duration = fadeOutSeconds 
            ?? _fadeOutDurationOverride 
            ?? _active?.FadeOutSeconds 
            ?? _playlist.CurrentTrack?.FadeOutSeconds 
            ?? 0f;
        if (duration > 0f && (_active != null || _outgoing != null || _backend.IsTrackPlaying))
        {
            _stoppingWithFade = true;
            _stopFadeDuration = duration;
            _stopFadeElapsed = 0f;
            _sequencer.Cancel();
            _isCrossfading = false;
            _crossfadeElapsed = 0f;
            _pending = null;
            _fadeOutDurationOverride = null;
            return;
        }

        _stoppingWithFade = false;
        _stopFadeElapsed = 0f;
        _stopFadeDuration = 0f;
        _fadeInDurationOverride = null;
        _fadeOutDurationOverride = null;
        _playlist.Stop();
        _sequencer.Cancel();
        _isCrossfading = false;
        _crossfadeElapsed = 0f;
        _active = null;
        _outgoing = null;
        _pending = null;
        _transitionFrom = null;
        _backend.Stop();
        _backend.StopOutgoing();
    }

    /// <summary>Advances playback by <paramref name="deltaSeconds"/>; call once per frame.</summary>
    public void Update(float deltaSeconds)
    {
        _backend.Update(deltaSeconds);
        _duck.Update(deltaSeconds);

        if (_stoppingWithFade)
        {
            _stopFadeElapsed += deltaSeconds;
            var progress = _stopFadeDuration <= 0f ? 1f : Math.Clamp(_stopFadeElapsed / _stopFadeDuration, 0f, 1f);
            var fade = 1f - progress;
            var (fadeIn, fadeOutGain) = CrossfadeMath.EqualPower(progress);
            if (_backend.SupportsOverlap)
            {
                if (_active != null)
                    _backend.SetActiveVolume(Compose(1f - progress, _active.Context));
                if (_outgoing != null)
                    _backend.SetOutgoingVolume(Compose(fadeOutGain, _outgoing.Context));
            }
            else
            {
                if (_active != null)
                    _backend.SetActiveVolume(Compose(1f - progress, _active.Context));
            }

            if (progress >= 1f)
            {
                _stoppingWithFade = false;
                _stopFadeElapsed = 0f;
                _stopFadeDuration = 0f;
                _fadeInDurationOverride = null;
                _fadeOutDurationOverride = null;
                _playlist.Stop();
                _sequencer.Cancel();
                _isCrossfading = false;
                _crossfadeElapsed = 0f;
                _active = null;
                _outgoing = null;
                _pending = null;
                _transitionFrom = null;
                _backend.Stop();
                _backend.StopOutgoing();
                return;
            }
            return;
        }

        if (!_playlist.IsPlaying)
        {
            if (_isCrossfading || _backend.IsTrackPlaying)
            {
                _backend.Stop();
                _backend.StopOutgoing();
            }
            _isCrossfading = false;
            _active = null;
            _outgoing = null;
            _pending = null;
            return;
        }

        var desired = _playlist.CurrentTrack;
        if (desired is not null && !ReferenceEquals(desired, _pending ?? _active))
        {
            // The track's authored fade is the default only: a call-level fade from the Play
            // that requested this transition outranks it (issue #52).
            if (_fadeInDurationOverride is null && desired.FadeInSeconds.HasValue)
                _fadeInDurationOverride = desired.FadeInSeconds;
            if (desired.FadeOutSeconds.HasValue)
                _fadeOutDurationOverride = desired.FadeOutSeconds;
            BeginTransition(desired);
        }

        if (_isCrossfading)
        {
            if (_backend.SupportsOverlap) UpdateOverlapCrossfade(deltaSeconds);
            else UpdateSequencedCrossfade(deltaSeconds);
        }
        else
        {
            ApplyVolumes(fadeIn: 1f, fadeOut: 0f);
        }
    }

    private void UpdateOverlapCrossfade(float deltaSeconds)
    {
        _crossfadeElapsed += deltaSeconds;
        var inProgress = _transitionDuration <= 0f ? 1f : Math.Clamp(_crossfadeElapsed / _transitionDuration, 0f, 1f);
        var outProgress = (_transitionDuration <= 0f || _fadeOutDuration <= 0f) ? 1f : Math.Clamp(_crossfadeElapsed / _fadeOutDuration, 0f, 1f);
        var (fadeIn, _) = CrossfadeMath.EqualPower(inProgress);
        var (_, fadeOut) = CrossfadeMath.EqualPower(outProgress);
        ApplyVolumes(fadeIn, fadeOut);

        if (inProgress >= 1f && outProgress >= 1f)
        {
            _backend.StopOutgoing();
            _outgoing = null;
            _isCrossfading = false;
            _transitionFrom = null;
            ApplyVolumes(fadeIn: 1f, fadeOut: 0f);
        }
    }

    private void UpdateSequencedCrossfade(float deltaSeconds)
    {
        _sequencer.FadeOutDuration = _fadeOutDuration;
        _sequencer.FadeInDuration = _transitionDuration;
        var fadeOutCompleted = _sequencer.Advance(deltaSeconds);

        if (fadeOutCompleted && _pending != null)
        {
            // The outgoing track has faded to silence: swap to the successor.
            _backend.Stop();
            _active = _pending;
            _pending = null;
            _backend.PlayTrack(
                _active.Name,
                Compose(_sequencer.InGain, _active.Context),
                NextCuePitch());
        }

        if (_sequencer.Phase == MusicFadePhase.Steady)
        {
            _isCrossfading = false;
            _transitionFrom = null;
            ApplyVolumes(fadeIn: 1f, fadeOut: 0f);
        }
        else if (_sequencer.Phase == MusicFadePhase.FadingOut)
        {
            // Still the old track on the single instance: its volume follows the outgoing ramp.
            if (_active != null)
                _backend.SetActiveVolume(Compose(_sequencer.OutGain, _active.Context));
        }
        else
        {
            ApplyVolumes(fadeIn: _sequencer.InGain, fadeOut: 0f);
        }
    }

    private void BeginTransition(PlaylistTrack desired)
    {
        if (_backend.SupportsOverlap)
        {
            _outgoing = _active;
            _active = desired;
            _pending = null;
            _transitionFrom = _outgoing;
            _backend.PlayTrack(desired.Name, Compose(0f, desired.Context), NextCuePitch());
            _isCrossfading = true;
            _crossfadeElapsed = 0f;
            _transitionDuration = _fadeInDurationOverride ?? _music.CrossfadeSeconds;
            // The outgoing fade duration must be assigned HERE: this path previously left
            // _fadeOutDuration at whatever the last transition set, and the fresh-start path
            // leaves it at 0 - which UpdateOverlapCrossfade reads as "outProgress = 1", zeroing
            // the outgoing track's volume on the first frame (an audible cut instead of a
            // crossfade; dogfooded in monsterprojectweb, hospital -> overworld). Precedence
            // matches the file's chain: transition override > outgoing track's authored fade >
            // the fast-fade default.
            _fadeOutDuration = _fadeOutDurationOverride
                ?? _outgoing?.FadeOutSeconds
                ?? FastFadeOutSeconds;
            _fadeInDurationOverride = null;
            _fadeOutDurationOverride = null;
        }
        else if (_backend.IsTrackPlaying)
        {
            // Single-instance backend with something playing: fade it out first, switch later.
            _pending = desired;
            _transitionFrom = _active;
            _sequencer.BeginFadeOut();
            _isCrossfading = true;
            var dur = _fadeInDurationOverride ?? _music.CrossfadeSeconds;
            if (dur <= 0f)
            {
                _transitionDuration = 0f;
                _fadeOutDuration = 0f;
            }
            else
            {
                _transitionDuration = dur * 0.5f;
                _fadeOutDuration = dur * 0.5f;
            }
            _fadeInDurationOverride = null;
            _fadeOutDurationOverride = null;
        }
        else
        {
            _active = desired;
            _pending = null;
            _transitionFrom = null;
            _sequencer.BeginFadeIn();
            _backend.PlayTrack(desired.Name, Compose(_sequencer.InGain, desired.Context), NextCuePitch());
            _isCrossfading = true;
            _transitionDuration = _fadeInDurationOverride ?? _music.CrossfadeSeconds;
            _fadeOutDuration = 0f;
            _fadeInDurationOverride = null;
            _fadeOutDurationOverride = null;
        }
    }

    private void ApplyVolumes(float fadeIn, float fadeOut)
    {
        if (_active != null)
        {
            _backend.SetActiveVolume(Compose(fadeIn, _active.Context));
        }

        if (_outgoing != null)
        {
            _backend.SetOutgoingVolume(Compose(fadeOut, _outgoing.Context));
        }
    }

    /// <summary>The final volume: master × context scale × duck ratio × fade, clamped to [0, 1].</summary>
    private float Compose(float fade, string context)
    {
        var scale = 1f;
        if (!string.IsNullOrEmpty(context) && _music.Contexts.TryGetValue(context, out var authored))
            scale = authored;

        // One resolver: master -> music bus volume -> mute/pause (the mixer's effective
        // gains), then the music-only context scale, duck and fade (Gate M: contexts stay
        // music-only; the duck ratio is read live, D4).
        var busGain = _buses.Resolve(BusNames.Master).Gain * _buses.Resolve(BusNames.Music).Gain;
        return MusicVolumeMath.Effective(busGain, scale, _duck.EffectiveRatio, fade);
    }

    private float NextCuePitch() => _music.CueVariation?.Resolve(_random).Pitch ?? 0f;

    private void OnBackendTrackEnded(object? sender, EventArgs e)
    {
        var before = _playlist.CurrentTrack;
        _playlist.NotifyTrackCompleted();
        var after = _playlist.CurrentTrack;

        if (before != null && ReferenceEquals(before, after))
        {
            // Repeat=Track (or a single-track loop): the playlist keeps the same track, so no
            // Update-driven transition will fire — the director restarts playback itself.
            _outgoing = null;
            _active = after;
            _pending = null;
            _transitionFrom = null;
            _backend.PlayTrack(after.Name, Compose(0f, after.Context), NextCuePitch());
            _sequencer.FadeInDuration = _fadeInDurationOverride ?? _music.CrossfadeSeconds;
            _sequencer.BeginFadeIn();
            _isCrossfading = true;
            _crossfadeElapsed = 0f;
            _transitionDuration = _fadeInDurationOverride ?? _music.CrossfadeSeconds;
            _fadeOutDuration = 0f;
        }
    }
}
