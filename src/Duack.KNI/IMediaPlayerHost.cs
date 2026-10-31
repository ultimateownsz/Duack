using System;

namespace Duack.KNI;

/// <summary>
/// Thin seam over the platform music player (static <c>Microsoft.Xna.Framework.Media.MediaPlayer</c>
/// in production). Keeping it injectable lets <see cref="MediaPlayerMusicBackend"/> and its natural-end
/// detection be unit-tested headlessly, without a browser or an audio device.
/// </summary>
public interface IMediaPlayerHost
{
    /// <summary>Raised whenever the platform media state changes (play / pause / stop).</summary>
    event EventHandler<EventArgs>? StateChanged;

    /// <summary>True while the player is stopped (never started, finished, or stopped by us).</summary>
    bool IsStopped { get; }

    /// <summary>True while the player is actively producing sound.</summary>
    bool IsPlaying { get; }

    /// <summary>Playback volume in [0, 1].</summary>
    float Volume { get; set; }

    /// <summary>Whether the platform loops the current track forever.</summary>
    bool IsRepeating { get; set; }

    /// <summary>Starts playing the track at <paramref name="relativeUri"/> (resolved against the
    /// document base URL on Blazor). Throws if the track cannot be started.</summary>
    void Play(string relativeUri);

    /// <summary>Stops the current track immediately.</summary>
    void Stop();

    /// <summary>Pauses the current track, preserving position.</summary>
    void Pause();

    /// <summary>Resumes a paused track.</summary>
    void Resume();
}