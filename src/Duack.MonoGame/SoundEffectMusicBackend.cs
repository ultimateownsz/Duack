using System;

namespace Duack.MonoGame;

/// <summary>
/// Desktop (MonoGame) <see cref="IMusicBackend"/>: caches decoded <see cref="IAudioClip"/>s and
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

    /// <summary>Raised when the active track plays to its natural end.</summary>
    public event EventHandler? TrackEnded;

    public SoundEffectMusicBackend(IAudioClipLibrary clips) =>
        _clips = clips ?? throw new ArgumentNullException(nameof(clips));

    /// <inheritdoc />
    public bool SupportsOverlap => true;

    /// <inheritdoc />
    public bool IsTrackPlaying => _active is { IsPlaying: true };

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

        _active = clip.CreateInstance();
        _active.IsLooped = false;
        _active.Volume = initialVolume;
        _active.Pitch = pitch;
        _active.Play();
        _activeStarted = true;
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
        _activeStarted = false;
        StopOutgoing();
    }

    /// <inheritdoc />
    public void Update(float deltaSeconds)
    {
        if (_activeStarted && _active is { IsPlaying: false })
        {
            // The instance stopped on its own (reached the end); report completion exactly once.
            _activeStarted = false;
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
    }
}