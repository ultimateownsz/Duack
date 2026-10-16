using System;

namespace Duack.MonoGame;

/// <summary>
/// One playing instance of an <see cref="IAudioClip"/>. This is the seam that lets
/// <see cref="SoundEffectMusicBackend"/> be tested headlessly: the real implementation wraps a
/// MonoGame <c>SoundEffectInstance</c> (which needs an audio device), while tests supply a fake.
/// </summary>
public interface IAudioInstance : IDisposable
{
    /// <summary>Whether the instance replays from the start when it reaches the end.</summary>
    bool IsLooped { get; set; }

    /// <summary>Playback volume in [0, 1].</summary>
    float Volume { get; set; }

    /// <summary>Playback pitch in the engine's [-1, 1] units.</summary>
    float Pitch { get; set; }

    /// <summary>True while the instance is actively producing sound.</summary>
    bool IsPlaying { get; }

    /// <summary>Starts (or restarts) playback.</summary>
    void Play();

    /// <summary>Stops playback immediately.</summary>
    void Stop();
}

/// <summary>
/// A decoded, ready-to-play audio clip (a MonoGame <c>SoundEffect</c> in production). Created once
/// per track and cached by the library; each play calls <see cref="CreateInstance"/>.
/// </summary>
public interface IAudioClip : IDisposable
{
    /// <summary>Creates a fresh, independent playback instance of this clip.</summary>
    IAudioInstance CreateInstance();
}

/// <summary>
/// Resolves (and caches) a track name to a decoded <see cref="IAudioClip"/>. Returns null when the
/// track cannot be loaded (missing or mid-copy file) so a load failure can never crash the frame.
/// </summary>
public interface IAudioClipLibrary
{
    /// <summary>Returns the clip for <paramref name="trackName"/>, or null if it could not be loaded.</summary>
    IAudioClip? Get(string trackName);
}