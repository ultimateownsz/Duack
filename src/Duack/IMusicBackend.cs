using System;

namespace Duack;

/// <summary>
/// Backend abstraction for music playback. Adapters implement this over engine-specific
/// audio APIs (MonoGame <c>SoundEffectInstance</c>, KNI <c>MediaPlayer</c>, etc.). The core
/// <see cref="MusicManager"/> owns all mixing logic (crossfade, ducking, context scales,
/// master volume); backends are thin and only apply the volume they are told, play tracks,
/// and report completion.
/// </summary>
public interface IMusicBackend
{
    /// <summary>True if the backend can overlap two tracks simultaneously for crossfading.</summary>
    bool SupportsOverlap { get; }

    /// <summary>
    /// Starts playing the specified track. The backend should begin playback at the given
    /// initial volume and apply <paramref name="pitch"/> (cue variation) if supported.
    /// When a track is already playing and <see cref="SupportsOverlap"/> is true, that track
    /// becomes the outgoing (fading-out) track.
    /// </summary>
    void PlayTrack(string trackName, float initialVolume, float pitch);

    /// <summary>Sets the final volume (0..1) of the currently active (fading-in) track.</summary>
    void SetActiveVolume(float volume);

    /// <summary>
    /// Sets the final volume (0..1) of the outgoing (fading-out) track. Ignored if
    /// <see cref="SupportsOverlap"/> is false or no outgoing track exists.
    /// </summary>
    void SetOutgoingVolume(float volume);

    /// <summary>Stops any outgoing (fading-out) track immediately.</summary>
    void StopOutgoing();

    /// <summary>Stops all music playback.</summary>
    void Stop();

    /// <summary>Pauses playback without tearing down the active track (best-effort resume).</summary>
    void Pause();

    /// <summary>Resumes a paused track.</summary>
    void Resume();

    /// <summary>Current playback position of the active track (best-effort; zero if unsupported).</summary>
    TimeSpan Position { get; }

    /// <summary>Total duration of the active track (best-effort; zero if unsupported).</summary>
    TimeSpan Duration { get; }

    /// <summary>Seeks the active track to <paramref name="position"/> (best-effort; a no-op when <see cref="SupportsSeek"/> is false).</summary>
    void Seek(TimeSpan position);

    /// <summary>
    /// True when <see cref="Seek"/> actually moves the audible position (the web platform player
    /// can; a <c>SoundEffectInstance</c> cannot). Callers check this before relying on a resume
    /// from a captured position; Seek on a backend without it is a reported no-op.
    /// </summary>
    bool SupportsSeek { get; }

    /// <summary>True if playback is currently paused.</summary>
    bool IsPaused { get; }

    /// <summary>True if any track is currently playing (active or, if overlapping, outgoing).</summary>
    bool IsTrackPlaying { get; }

    /// <summary>
    /// Seconds since the currently active track started playing (0 when nothing is playing).
    /// Resets on <see cref="PlayTrack"/> and on <see cref="Stop"/>.
    /// </summary>
    float CurrentTrackElapsedSeconds { get; }

    /// <summary>Total length of the current track in seconds, or null when the backend cannot report it.</summary>
    float? CurrentTrackDurationSeconds { get; }

    /// <summary>
    /// Fired when the currently active track completes playback (reaches end). The core
    /// <see cref="MusicManager"/> uses this to advance playlists via
    /// <see cref="MusicPlaylistController.NotifyTrackCompleted"/>.
    /// </summary>
    event EventHandler? TrackEnded;

    /// <summary>Per-frame update hook for backends that need polling (e.g. state checks).</summary>
    void Update(float deltaSeconds);
}