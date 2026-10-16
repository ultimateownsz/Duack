namespace Duack;

/// <summary>
/// Backend abstraction for music playback. Adapters implement this over engine-specific
/// audio APIs (MonoGame SoundEffectInstance, KNI MediaPlayer, etc.). The core
/// <see cref="MusicDirector"/> owns all mixing logic (crossfade, ducking, gain math);
/// backends are thin and report only playback state and completion.
/// </summary>
public interface IMusicBackend
{
    /// <summary>True if the backend can overlap two tracks simultaneously for crossfading.</summary>
    bool SupportsOverlap { get; }

    /// <summary>
    /// Starts playing the specified track. The backend should begin playback at the given
    /// initial volume and apply <paramref name="pitch"/> (cue variation) if supported.
    /// </summary>
    void PlayTrack(string trackName, float initialVolume = 1f, float pitch = 0f);

    /// <summary>Sets the gain applied to the currently active (fading-in) track.</summary>
    void SetActiveGain(float gain);

    /// <summary>
    /// Sets the gain applied to the outgoing (fading-out) track. Ignored if
    /// <see cref="SupportsOverlap"/> is false or no outgoing track exists.
    /// </summary>
    void SetOutgoingGain(float gain);

    /// <summary>Stops any outgoing (fading-out) track immediately.</summary>
    void StopOutgoing();

    /// <summary>Stops all music playback.</summary>
    void Stop();

    /// <summary>True if any track is currently playing (active or, if overlapping, outgoing).</summary>
    bool IsTrackPlaying { get; }

    /// <summary>
    /// Fired when the currently active track completes playback (reaches end). The core
    /// <see cref="MusicDirector"/> uses this to advance playlists via
    /// <see cref="MusicPlaylistController.NotifyTrackCompleted"/>.
    /// </summary>
    event EventHandler? TrackEnded;

    /// <summary>Per-frame update hook for backends that need polling (e.g. state checks).</summary>
    void Update(float deltaSeconds);
}