using System;
using System.Collections.Generic;
using System.Linq;

namespace Duack;

/// <summary>
/// Plays one-shot cues by name against a discovered <see cref="AudioLibrary"/> and keeps the music
/// mix out of the way while they speak: every successful play pulses the music manager's duck
/// source (default <c>sfx</c>). Unknown cues and backend refusals report failure and never duck.
/// <see cref="PlayNext"/> draws from a shuffle bag over the discovered sfx, so a demo or ambient
/// layer never plays the same cue back-to-back.
///
/// <para>
/// Pitch is passed through in engine units (MonoGame: -1..1, 0 = natural), which is what makes "the
/// same sound, differently pitched" a one-line call (see the sample's marble bag of hit pitches).
/// When <see cref="SfxConfig.CueVariation"/> is authored, each play resolves a <see cref="CuePlay"/>
/// jitter on top of the caller's pitch and volume (pitch jitter added, volume jitter scaling) so
/// repeated cues do not sound machine-gunned.
/// </para>
/// </summary>
public sealed class SfxManager
{
    private readonly AudioLibrary _library;
    private readonly ISfxBackend _backend;
    private readonly MusicManager _music;
    private readonly SfxConfig? _sfxConfig;
    private readonly VoiceLimiter? _voices;
    private readonly string _duckSource;
    private readonly BusMixer _buses;
    private readonly MarbleBag<string> _bag;
    private readonly Random _random;
    private readonly ISfxVoiceControlBackend? _control;
    private readonly ISfxPositionalBackend? _positional;
    private readonly Dictionary<SfxSpatialVoice, SfxSpatialVoice> _positionalVoices = new();
    private readonly SpatialConfig _spatial;
    private readonly float _pruneAfterSeconds;
    private float _listenerX;
    private float _listenerY;
    private readonly Dictionary<long, object> _handles = new();

    /// <summary>Handle marker for a successful play on a backend without voice control.</summary>
    private static readonly object NoHandle = new();

    /// <param name="library">Discovered cues; only names in <see cref="AudioLibrary.Sfx"/> are playable.</param>
    /// <param name="backend">Engine playback adapter.</param>
    /// <param name="music">Director whose duck source is pulsed while a cue speaks.</param>
    /// <param name="duckSource">Duck-source name in <see cref="MusicConfig.DuckSources"/> (default <c>sfx</c>).</param>
    /// <param name="random">Randomness source for <see cref="PlayNext"/> and cue variation; defaults to <see cref="Random.Shared"/>.</param>
    /// <param name="masterVolume">Master bus volume (scales music and SFX alike); defaults to full volume.</param>
    /// <param name="sfxBusVolume">SFX bus volume; defaults to full volume.</param>
    /// <param name="sfxConfig">SFX mix settings; carries the optional <see cref="SfxConfig.CueVariation"/> jitter applied per play and the authored voice limits.</param>
    /// <param name="voiceLimits">Explicit voice limits; falls back to <see cref="SfxConfig.VoiceLimits"/> when omitted.</param>
    public SfxManager(
        AudioLibrary library,
        ISfxBackend backend,
        MusicManager music,
        string duckSource = "sfx",
        Random? random = null,
        float masterVolume = 1f,
        float sfxBusVolume = 1f,
        SfxConfig? sfxConfig = null,
        VoiceLimitConfig? voiceLimits = null)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _music = music ?? throw new ArgumentNullException(nameof(music));
        _duckSource = duckSource ?? throw new ArgumentNullException(nameof(duckSource));
        _sfxConfig = sfxConfig;
        // One resolver for every bus: the mixer owns the whole table (master/music/sfx plus the
        // user buses) and the Views readback reads the duck system live (D4).
        _buses = new BusMixer(sfxConfig?.Buses, target => _music.DuckRatioFor(target));
        var limits = voiceLimits ?? sfxConfig?.VoiceLimits;
        _voices = limits is null ? null : new VoiceLimiter(limits);
        _control = _backend as ISfxVoiceControlBackend;
        _positional = _backend as ISfxPositionalBackend;
        // A standalone SfxConfig never went through the builder: sanitize defensively so even
        // a hand-broken config degrades to a centered, ordinary mix instead of NaN pan values.
        _spatial = sfxConfig?.Spatial ?? new SpatialConfig();
        _spatial.Sanitize();
        _pruneAfterSeconds = Math.Max(0.1f, 2f * (sfxConfig?.VoiceLimits?.DefaultVoiceSeconds ?? 0.5f));
        _random = random ?? Random.Shared;
        MasterVolume = masterVolume;
        SfxBusVolume = sfxBusVolume;
        _bag = new MarbleBag<string>(library.Sfx, random, avoidImmediateRepeat: true);
    }

    /// <summary>
    /// Master bus volume (scales music and SFX alike), read live on every play so a settings
    /// slider applies immediately; defaults to the constructor argument. A thin delegating
    /// writer into the mixer's <see cref="BusNames.Master"/> entry — the same storage
    /// <see cref="Buses"/> resolves from.
    /// </summary>
    public float MasterVolume
    {
        get => _buses.VolumeOf(BusNames.Master);
        set => _buses.SetBus(BusNames.Master, value);
    }

    /// <summary>
    /// SFX bus volume, read live on every play; defaults to the constructor argument. A thin
    /// delegating writer into the mixer's fixed <see cref="BusNames.Sfx"/> entry.
    /// </summary>
    public float SfxBusVolume
    {
        get => _buses.VolumeOf(BusNames.Sfx);
        set => _buses.SetBus(BusNames.Sfx, value);
    }

    /// <summary>
    /// Runtime state and routing for the one bus table: the fixed master/music/sfx entries and
    /// the user buses - volumes, mute, pause and the master mute, live duck contribution in the
    /// <see cref="BusMixer.Views"/> readback, with <see cref="BusMixer.BusFellBack"/> reporting
    /// unknown bus names.
    /// </summary>
    public BusMixer Buses => _buses;

    /// <summary>
    /// Raised when a play's composed effective gain resolves to zero — the cue is reported instead
    /// of audibly disappearing. No backend call and no music duck happen for such a play.
    /// </summary>
    public event Action<string>? ZeroEffectiveGain;

    /// <summary>The discovered one-shot cue names (see <see cref="AudioLibrary.Sfx"/>).</summary>
    public IReadOnlyList<string> Names => _library.Sfx;

    /// <summary>
    /// Outcome of the most recent <see cref="Play"/> or <see cref="PlayDetailed"/> call, so a
    /// caller that only needs a bool can still report why a cue did not speak.
    /// </summary>
    public SfxPlayResult LastPlayResult { get; private set; } = SfxPlayResult.UnknownCue;

    /// <summary>Number of SFX voices currently reserved by the limiter (0 when no limits are authored).</summary>
    public int ActiveVoiceCount => _voices?.ActiveVoiceCount ?? 0;

    /// <summary>
    /// Plays <paramref name="cueName"/> and ducks the music for the configured hold while it
    /// speaks. Returns false — without touching the backend and without ducking — when the cue is
    /// not in the library, a voice slot is refused, or the backend refuses to play it. Pass
    /// <paramref name="duck"/> = false to play without ducking the music.
    /// </summary>
    public bool Play(string cueName, float pitch = 0f, float volume = 1f, bool duck = true, string? bus = null) =>
        PlayDetailed(cueName, pitch, volume, duck, bus) == SfxPlayResult.Played;

    /// <summary>
    /// As <see cref="Play"/>, but reports the specific reason for a refusal (unknown cue, priority
    /// rejection, backend refusal) instead of collapsing every failure into false. A successful
    /// play may steal an active voice, which is reported by the limiter's readback.
    /// </summary>
    public SfxPlayResult PlayDetailed(
        string cueName, float pitch = 0f, float volume = 1f, bool duck = true, string? bus = null, int priority = 0) =>
        StartVoice(cueName, pitch, volume, duck, bus, priority, positional: false, pan: 0f, distanceGain: 1f).Result;

    /// <summary>
    /// Plays one shot positioned in the world against the listener (see <see cref="SetListener"/>).
    /// The core composes the whole chain in order: per-play variation, master gain, resolved bus
    /// gain, live duck ratio, then the distance attenuation — so a positional play rides the same
    /// unified bus path as everything else, and a play at or past the mute floor composes to zero:
    /// reported through <see cref="ZeroEffectiveGain"/>, never heard, consuming no limiter slot.
    /// Returns a <see cref="SfxSpatialVoice"/> to move the source while it sounds, or null when
    /// the play did not start a remixable positional voice — refused, muted, or a backend without
    /// the <see cref="ISfxPositionalBackend"/> seam (which still plays the composed volume, mono-
    /// centered: the pan is dropped, the position is not).
    /// </summary>
    public SfxSpatialVoice? PlayAt(
        string cueName, float sourceX, float sourceY, float pitch = 0f, float volume = 1f, bool duck = true, string? bus = null, int priority = 0)
    {
        var (distanceGain, pan) = SpatialAt(sourceX, sourceY);
        var (result, handle, baseVolume) = StartVoice(cueName, pitch, volume, duck, bus, priority, positional: true, pan, distanceGain);
        if (result != SfxPlayResult.Played || handle is null || handle == NoHandle || _positional is null)
            return null;

        var (x, y) = SanitizeSourcePosition(sourceX, sourceY);
        var voice = new SfxSpatialVoice(this, handle, bus, baseVolume, x, y);
        _positionalVoices.Add(voice, voice);
        return voice;
    }

    /// <summary>
    /// Re-anchors every positional play: the listener is a primitive pair (ADR-11 — no engine
    /// types; wiring stays engine-side). Live voices re-mix for the new position immediately.
    /// Non-finite coordinates anchor at the origin.
    /// </summary>
    public void SetListener(float x, float y)
    {
        _listenerX = IsFinite(x) ? x : 0f;
        _listenerY = IsFinite(y) ? y : 0f;
        RemixAll();
    }

    /// <summary>Readback of the listener anchor the positional math last used.</summary>
    public (float X, float Y) ListenerPosition => (_listenerX, _listenerY);

    /// <summary>
    /// One composition for every play: per-play variation -> clamp -> master gain -> resolved bus
    /// gain -> live duck ratio -> (positional) distance attenuation -> clamp. A play whose total
    /// gain composes to zero reports its cause instead of staying silent: no backend call, no
    /// music duck, no voice slot consumed, nothing audible to duck for — which is how the
    /// spatial mute floor releases, rather than reserves, its voice.
    /// </summary>
    private (SfxPlayResult Result, object? Handle, float BaseVolume) StartVoice(
        string cueName, float pitch, float volume, bool duck, string? bus, int priority,
        bool positional, float pan, float distanceGain)
    {
        if (!_library.IsSfx(cueName)) return (Record(SfxPlayResult.UnknownCue), null, 0f);

        // Per-play jitter from the authored cue variation, resolved fresh on every play so a live
        // settings change applies without rebuilding the manager; added on top of the caller's
        // pitch and scaling the caller's volume, then composed with the buses.
        var variation = _sfxConfig?.CueVariation;
        var cuePlay = variation?.Resolve(_random) ?? new CuePlay(0f, 1f);
        var finalPitch = variation is null ? pitch : Math.Clamp(pitch + cuePlay.Pitch, -1f, 1f);
        var baseVolume = Math.Clamp(volume * cuePlay.VolumeScale, 0f, 1f);
        // One resolver: master -> bus volume -> mute/pause (all inside the mixer's effective
        // gains) -> per-play volume, then the resolved bus's live duck ratio (D5). The fixed
        // (un-routed) path lands on the "sfx" bus, so a duck source targeting "sfx" dips it too
        // — one semantic, one path (the Gate D fold). The positional distance gain multiplies
        // the resolved gain — same chain, one more factor — never a side channel into "volume".
        var resolution = _buses.Resolve(bus);
        var finalVolume = Math.Clamp(baseVolume
                                     * _buses.Resolve(BusNames.Master).Gain * resolution.Gain
                                     * _music.DuckRatioFor(resolution.Bus)
                                     * distanceGain, 0f, 1f);

        if (finalVolume <= 0f)
        {
            ZeroEffectiveGain?.Invoke(cueName);
            return (Record(SfxPlayResult.Played), null, baseVolume);
        }

        var admission = _voices?.Acquire(cueName, finalVolume, priority: priority);
        if (admission is { } attempt && attempt.Outcome != VoicePlayOutcome.Played)
            return (Record(SfxPlayResult.RejectedBelowPriority), null, baseVolume);

        // Make stealing audible when the backend can stop one voice (#38): the stolen
        // bookkeeping slot already freed; silence the victim before the new voice starts. On a
        // plain backend the map is empty and stealing stays slot-only (the documented fallback).
        if (admission is { } stolenTry && stolenTry.StoleSequence != 0 &&
            _control is not null && _handles.Remove(stolenTry.StoleSequence, out var victim))
            _control.StopVoice(victim);

        object? handle;
        if (positional && _positional is not null)
            handle = _positional.PlayTrackedAt(cueName, pan, finalPitch, finalVolume);
        else
            handle = _control?.PlayTracked(cueName, finalPitch, finalVolume)
                     ?? (_control is null && _backend.Play(cueName, finalPitch, finalVolume)
                         ? NoHandle
                         : null);
        if (handle is null)
        {
            if (admission is { } reserved) _voices!.Release(reserved);
            return (Record(SfxPlayResult.BackendRefused), null, baseVolume);
        }
        if (admission is { } admitted) _handles[admitted.VoiceSequence] = handle;

        if (duck) _music.Pulse(_duckSource);
        return (Record(SfxPlayResult.Played), handle, baseVolume);
    }

    /// <summary>Distance gain and pan for a source position under the current listener and config.</summary>
    private (float DistanceGain, float Pan) SpatialAt(float sourceX, float sourceY)
    {
        var (x, y) = SanitizeSourcePosition(sourceX, sourceY);
        var dx = x - _listenerX;
        var dy = y - _listenerY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        return (SpatialMath.DistanceGain(distance, _spatial.ReferenceDistance, _spatial.MaxDistance, _spatial.Model, _spatial.Rolloff),
                SpatialMath.Pan(dx, _spatial.PanDistance, _spatial.PanSaturation));
    }

    /// <summary>Any non-finite coordinate drops the source onto the listener (a caller bug is centered sound, never NaN pan).</summary>
    private (float X, float Y) SanitizeSourcePosition(float sourceX, float sourceY) =>
        IsFinite(sourceX) && IsFinite(sourceY) ? (sourceX, sourceY) : (_listenerX, _listenerY);

    internal void RemixVoice(SfxSpatialVoice voice, float sourceX, float sourceY)
    {
        if (!_positionalVoices.ContainsKey(voice)) return; // stopped, pruned, or never followed
        var (x, y) = SanitizeSourcePosition(sourceX, sourceY);
        voice.SourceX = x;
        voice.SourceY = y;
        RemixVoiceNow(voice);
    }

    private void RemixAll()
    {
        foreach (var voice in _positionalVoices.Keys)
            RemixVoiceNow(voice);
    }

    private void RemixVoiceNow(SfxSpatialVoice voice)
    {
        if (_positional is null) return;
        var dx = voice.SourceX - _listenerX;
        var dy = voice.SourceY - _listenerY;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        var gain = SpatialMath.DistanceGain(distance, _spatial.ReferenceDistance, _spatial.MaxDistance, _spatial.Model, _spatial.Rolloff);
        var pan = SpatialMath.Pan(dx, _spatial.PanDistance, _spatial.PanSaturation);
        var resolution = _buses.Resolve(voice.Bus);
        var mixVolume = Math.Clamp(voice.BaseVolume
                                   * _buses.Resolve(BusNames.Master).Gain * resolution.Gain
                                   * _music.DuckRatioFor(resolution.Bus)
                                   * gain, 0f, 1f);
        _positional.SetVoiceMix(voice.BackendHandle, mixVolume, pan);
    }

    internal void StopSpatialVoice(SfxSpatialVoice voice)
    {
        _positionalVoices.Remove(voice);
        _positional?.StopVoice(voice.BackendHandle);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>
    /// Ages reserved voices so their slots are released when the assumed lifetime elapses, and
    /// ages positional voices' remix records: a record outlives its one-shot twice the assumed
    /// lifetime is dropped (a longer-than-assumed cue keeps sounding — it just stops following).
    /// </summary>
    public void Update(float deltaSeconds)
    {
        _voices?.Update(deltaSeconds);
        PruneAgedSpatialVoices(deltaSeconds);
    }

    private void PruneAgedSpatialVoices(float deltaSeconds)
    {
        if (_positionalVoices.Count == 0 || deltaSeconds <= 0f) return;
        List<SfxSpatialVoice>? stale = null;
        foreach (var voice in _positionalVoices.Keys)
        {
            voice.AgeSeconds += deltaSeconds;
            if (voice.AgeSeconds < _pruneAfterSeconds) continue;
            stale ??= new List<SfxSpatialVoice>();
            stale.Add(voice);
        }
        if (stale is null) return;
        foreach (var voice in stale) _positionalVoices.Remove(voice);
    }

    /// <summary>Stops every one-shot this manager's backend is currently playing and clears reserved voices.</summary>
    public void Stop()
    {
        _backend.Stop();
        _voices?.Clear();
        _handles.Clear();
        _positionalVoices.Clear();
    }

    private SfxPlayResult Record(SfxPlayResult result)
    {
        LastPlayResult = result;
        return result;
    }

    /// <summary>
    /// Draws the next cue from the shuffle bag (no immediate repeats) and plays it. Returns false
    /// when the library discovered no sfx at all.
    /// </summary>
    public bool PlayNext(float pitch = 0f, float volume = 1f, bool duck = true) =>
        _bag.TryDraw(out var cue) && Play(cue, pitch, volume, duck);
}
