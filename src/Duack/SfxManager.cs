using System;
using System.Collections.Generic;

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
    private readonly MarbleBag<string> _bag;
    private readonly Random _random;

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
        var limits = voiceLimits ?? sfxConfig?.VoiceLimits;
        _voices = limits is null ? null : new VoiceLimiter(limits);
        _random = random ?? Random.Shared;
        MasterVolume = masterVolume;
        SfxBusVolume = sfxBusVolume;
        _bag = new MarbleBag<string>(library.Sfx, random, avoidImmediateRepeat: true);
    }

    /// <summary>
    /// Master bus volume (scales music and SFX alike), read live on every play so a settings
    /// slider applies immediately; defaults to the constructor argument.
    /// </summary>
    public float MasterVolume { get; set; }

    /// <summary>SFX bus volume, read live on every play; defaults to the constructor argument.</summary>
    public float SfxBusVolume { get; set; }

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
    public bool Play(string cueName, float pitch = 0f, float volume = 1f, bool duck = true) =>
        PlayDetailed(cueName, pitch, volume, duck) == SfxPlayResult.Played;

    /// <summary>
    /// As <see cref="Play"/>, but reports the specific reason for a refusal (unknown cue, priority
    /// rejection, backend refusal) instead of collapsing every failure into false. A successful
    /// play may steal an active voice, which is reported by the limiter's readback.
    /// </summary>
    public SfxPlayResult PlayDetailed(
        string cueName, float pitch = 0f, float volume = 1f, bool duck = true, int priority = 0)
    {
        if (!_library.IsSfx(cueName)) return Record(SfxPlayResult.UnknownCue);

        // Per-play jitter from the authored cue variation, resolved fresh on every play so a live
        // settings change applies without rebuilding the manager; added on top of the caller's
        // pitch and scaling the caller's volume, then composed with the buses.
        var variation = _sfxConfig?.CueVariation;
        var cuePlay = variation?.Resolve(_random) ?? new CuePlay(0f, 1f);
        var finalPitch = variation is null ? pitch : Math.Clamp(pitch + cuePlay.Pitch, -1f, 1f);
        var finalVolume = Math.Clamp(volume * cuePlay.VolumeScale, 0f, 1f) * MasterVolume * SfxBusVolume;
        finalVolume = Math.Clamp(finalVolume, 0f, 1f);

        var admission = _voices?.Acquire(cueName, finalVolume, priority: priority);
        if (admission is { } attempt && attempt.Outcome != VoicePlayOutcome.Played)
            return Record(SfxPlayResult.RejectedBelowPriority);

        if (!_backend.Play(cueName, finalPitch, finalVolume))
        {
            if (admission is { } reserved) _voices!.Release(reserved);
            return Record(SfxPlayResult.BackendRefused);
        }

        if (duck) _music.Pulse(_duckSource);
        return Record(SfxPlayResult.Played);
    }

    /// <summary>Ages reserved voices so their slots are released when the assumed lifetime elapses.</summary>
    public void Update(float deltaSeconds) => _voices?.Update(deltaSeconds);

    /// <summary>Stops every one-shot this manager's backend is currently playing and clears reserved voices.</summary>
    public void Stop()
    {
        _backend.Stop();
        _voices?.Clear();
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
