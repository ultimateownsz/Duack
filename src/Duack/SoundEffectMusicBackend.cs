using System;

namespace Duack;

/// <summary>
/// Clip-based <see cref="IMusicBackend"/>: caches decoded <see cref="IAudioClip"/>s and
/// plays them through <see cref="IAudioInstance"/>s. It supports overlap, so the core
/// <see cref="MusicManager"/> crossfades the outgoing track against the incoming one.
///
/// <para>
/// All mixing decisions (master / context / duck / fade composition, crossfade curves, playlist
/// sequencing) live in the core; this adapter only maps the manager's requests onto the engine.
/// It plays tracks NON-looped: looping is a playlist concern (<c>PlaylistRepeatMode</c>) handled by
/// the core, so a track reaching its end is reported as a completion via the <see cref="IMusicBackend.TrackEnded"/>
/// event.
/// </para>
/// </summary>
public sealed class SoundEffectMusicBackend : IMusicBackend
{
    private readonly IAudioClipLibrary _clips;

    private IAudioInstance? _active;
    private IAudioInstance? _outgoing;
    private bool _activeStarted;
    private float _elapsed;

    /// <summary>Raised when the active track plays to its natural end.</summary>
    public event EventHandler? TrackEnded;

    public SoundEffectMusicBackend(IAudioClipLibrary clips) =>
        _clips = clips ?? throw new ArgumentNullException(nameof(clips));

    /// <inheritdoc />
    public bool SupportsOverlap => true;

    private bool _paused;
    private IAudioClip? _activeClip;

    /// <inheritdoc />
    public bool IsTrackPlaying => _active is { IsPlaying: true };

    /// <summary>
    /// The clip clock: <c>_elapsed</c> is advanced by <see cref="Update"/> and reset on every
    /// <see cref="PlayTrack"/>/<see cref="Stop"/>, so it tracks the active instance's audible
    /// position as long as updates run every frame; it freezes while paused, matching the web
    /// backend's player position. A <c>SoundEffectInstance</c> exposes no real position, so this
    /// is the best reading available on clip backends.
    /// </summary>
    public TimeSpan Position => _active is null ? TimeSpan.Zero : TimeSpan.FromSeconds(_elapsed);

    /// <summary>The active clip's duration, when the clip library exposes one.</summary>
    public TimeSpan Duration => _activeClip?.DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;

    /// <inheritdoc />
    public bool SupportsSeek => _activeClip is ISeekableAudioClip;

    /// <summary>
    /// Seeks the active track by rebuilding its playback instance from a clip decoded at the
    /// offset — a <c>SoundEffectInstance</c> cannot seek, but the fully decoded buffer it came
    /// from can (the skip is a decode-and-discard for OGG, a byte slice for WAV). The replacement
    /// starts before the old instance stops, so there is no audible gap; the instance's current
    /// volume/pitch carry over (an in-flight crossfade keeps fading) and a paused backend stays
    /// paused. Fail-soft: an unseekable clip or a failed offset decode reports and changes
    /// nothing. Position readback is exact after the seek — the clock is set, not accumulated.
    /// </summary>
    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (_activeClip is not ISeekableAudioClip seekable)
        {
            Console.WriteLine($"[Duack] Seek({position}) ignored: clip backends cannot seek a " +
                              "SoundEffectInstance; check IMusicBackend.SupportsSeek before relying on a resume position.");
            return;
        }

        var instance = seekable.CreateInstance(position);
        if (instance is null)
        {
            Console.WriteLine($"[Duack] Seek({position}) failed: the clip could not be decoded " +
                              "from the offset; playback continues where it was.");
            return;
        }

        if (_active != null)
        {
            instance.Volume = _active.Volume;
            instance.Pitch = _active.Pitch;
        }

        var old = _active;
        _active = instance;
        _elapsed = (float)position.TotalSeconds;
        if (_paused) instance.Pause();
        else
        {
            instance.Play();
            _activeStarted = true;
        }

        // The replacement is already audible, so tearing the old instance down overlaps two
        // playing instances for the swap frame only - inaudible next to a resume seek.
        if (old != null)
        {
            old.Stop();
            old.Dispose();
            seekable.Release(old);
        }
    }

    /// <inheritdoc />
    public bool IsPaused => _paused;

    /// <inheritdoc />
    public float CurrentTrackElapsedSeconds => _active is null ? 0f : _elapsed;

    /// <inheritdoc />
    public float? CurrentTrackDurationSeconds => null;

    /// <inheritdoc />
    public void PlayTrack(string trackName, float initialVolume, float pitch)
    {
        var clip = _clips.Get(trackName);
        if (clip == null)
        {
            // Load failed (missing/mid-copy file): stay silent rather than throw. A later PlayTrack
            // for the same name retries through the library's cache.
            return;
        }

        // Demote the current track to the outgoing (fading-out) slot; drop any older one.
        _outgoing?.Stop();
        _outgoing?.Dispose();
        _outgoing = _active;

        _activeClip = clip;
        _active = clip.CreateInstance();
        _active.IsLooped = false;
        _active.Volume = initialVolume;
        _active.Pitch = pitch;
        _active.Play();
        _activeStarted = true;
        _elapsed = 0f;
    }

    /// <inheritdoc />
    public void SetActiveVolume(float volume)
    {
        if (_active != null)
            _active.Volume = volume;
    }

    /// <inheritdoc />
    public void SetOutgoingVolume(float volume)
    {
        if (_outgoing != null)
            _outgoing.Volume = volume;
    }

    /// <inheritdoc />
    public void StopOutgoing()
    {
        _outgoing?.Stop();
        _outgoing?.Dispose();
        _outgoing = null;
    }

    /// <inheritdoc />
    public void Stop()
    {
        _active?.Stop();
        _active?.Dispose();
        _active = null;
        _activeClip = null;
        _activeStarted = false;
        _elapsed = 0f;
        _paused = false;
        StopOutgoing();
    }

    /// <inheritdoc />
    public void Pause()
    {
        if (_active is { IsPlaying: true })
        {
            _active.Pause();
            _paused = true;
        }
    }

    /// <inheritdoc />
    public void Resume()
    {
        if (_paused && _active != null)
        {
            _active.Play();
            _paused = false;
        }
    }

    /// <inheritdoc />
    public void Update(float deltaSeconds)
    {
        // The clip clock freezes while paused, like the web backend's player position, so a
        // context resume that reads Position after a pause does not skip ahead by the pause.
        if (!_paused && _active is not null)
            _elapsed += deltaSeconds;
        if (!_paused && _activeStarted && _active is { IsPlaying: false })
        {
            // The instance stopped on its own (reached the end); report completion exactly once.
            // A paused SoundEffectInstance also reports IsPlaying == false, hence the guard.
            _activeStarted = false;
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}