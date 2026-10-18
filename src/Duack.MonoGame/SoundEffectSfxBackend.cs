using System;
using System.Collections.Generic;

namespace Duack.MonoGame;

/// <summary>
/// MonoGame <see cref="ISfxBackend"/>: resolves a cue name through an <see cref="IAudioClipLibrary"/>
/// and plays it as a fresh non-looping instance with the requested pitch/volume (clamped to the
/// engine's ranges). Instances that have finished are reaped on the next play, and
/// <see cref="Dispose"/> stops everything still sounding - a long session must not leak instances.
/// All mixing decisions (including WHEN the music ducks) stay in the core <see cref="SfxDirector"/>;
/// this adapter only plays one cue now.
/// </summary>
public sealed class SoundEffectSfxBackend : ISfxBackend, IDisposable
{
    private readonly IAudioClipLibrary _clips;
    private readonly List<IAudioInstance> _playing = new();

    /// <param name="clips">Clip resolver (a <see cref="SoundEffectClipLibrary"/> in production).</param>
    public SoundEffectSfxBackend(IAudioClipLibrary clips) =>
        _clips = clips ?? throw new ArgumentNullException(nameof(clips));

    /// <inheritdoc />
    /// <remarks>Defaults mirror <see cref="SfxDirector.Play"/>: natural pitch, full volume.</remarks>
    public bool Play(string cueName, float pitch = 0f, float volume = 1f)
    {
        ReapFinished();

        var clip = _clips.Get(cueName);
        if (clip is null)
            return false; // missing clip: SfxDirector turns this into "no duck"

        var instance = clip.CreateInstance();
        instance.IsLooped = false;
        instance.Volume = Math.Clamp(volume, 0f, 1f);
        instance.Pitch = Math.Clamp(pitch, -1f, 1f);
        instance.Play();
        _playing.Add(instance);
        return true;
    }

    /// <summary>Stops and disposes every instance this backend started.</summary>
    public void Dispose()
    {
        foreach (var instance in _playing)
        {
            instance.Stop();
            instance.Dispose();
        }
        _playing.Clear();
    }

    private void ReapFinished()
    {
        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            if (_playing[i].IsPlaying) continue;
            _playing[i].Dispose();
            _playing.RemoveAt(i);
        }
    }
}
