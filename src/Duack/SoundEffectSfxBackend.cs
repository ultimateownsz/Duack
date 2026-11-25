using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// Clip-based <see cref="ISfxBackend"/>: resolves a cue name through an <see cref="IAudioClipLibrary"/>
/// and plays it as a fresh non-looping instance with the requested pitch/volume (clamped to the
/// engine's ranges) — and, for a positional play, with the pan on any instance that can carry
/// one (<see cref="IPanningVoice"/>; instances without it play centered, the additive fallback).
/// Instances that have finished are reaped on the next play, and <see cref="Dispose"/> stops
/// everything still sounding - a long session must not leak instances. All mixing decisions
/// (pan and volume composition included) stay in the core <see cref="SfxManager"/>; this
/// adapter only applies the numbers it is handed.
/// </summary>
public sealed class SoundEffectSfxBackend : ISfxBackend, ISfxVoiceControlBackend, ISfxPositionalBackend, IDisposable
{
    private readonly IAudioClipLibrary _clips;
    private readonly List<IAudioInstance> _playing = new();

    /// <param name="clips">Clip resolver (a <see cref="SoundEffectClipLibrary"/> in production).</param>
    public SoundEffectSfxBackend(IAudioClipLibrary clips) =>
        _clips = clips ?? throw new ArgumentNullException(nameof(clips));

    /// <inheritdoc />
    /// <remarks>Defaults mirror <see cref="SfxManager.Play"/>: natural pitch, full volume.</remarks>
    public bool Play(string cueName, float pitch = 0f, float volume = 1f) =>
        PlayTracked(cueName, pitch, volume) is not null;

    /// <inheritdoc />
    public object? PlayTracked(string cueName, float pitch = 0f, float volume = 1f)
    {
        ReapFinished();

        var clip = _clips.Get(cueName);
        if (clip is null)
            return null; // missing clip: SfxManager turns this into "no duck"

        var instance = clip.CreateInstance();
        instance.IsLooped = false;
        instance.Volume = Math.Clamp(volume, 0f, 1f);
        instance.Pitch = Math.Clamp(pitch, -1f, 1f);
        instance.Play();
        _playing.Add(instance);
        return instance; // the started voice's own handle
    }

    /// <summary>
    /// Plays a tracked voice carrying a pan. The pan lands on the instance when it implements
    /// <see cref="IPanningVoice"/> (the desktop platform instance does); otherwise the play is
    /// identical to <see cref="PlayTracked"/> — centered, pan dropped.
    /// </summary>
    public object? PlayTrackedAt(string cueName, float pan, float pitch = 0f, float volume = 1f)
    {
        ReapFinished();

        var clip = _clips.Get(cueName);
        if (clip is null) return null;

        var instance = clip.CreateInstance();
        if (instance is IPanningVoice panning)
            panning.Pan = Math.Clamp(pan, -1f, 1f);
        instance.IsLooped = false;
        instance.Volume = Math.Clamp(volume, 0f, 1f);
        instance.Pitch = Math.Clamp(pitch, -1f, 1f);
        instance.Play();
        _playing.Add(instance);
        return instance;
    }

    /// <summary>
    /// Re-mixes one live voice (a moving source, a moving listener). A handle this backend
    /// never handed out, or one already reaped, is a no-op — the StopVoice contract.
    /// </summary>
    public void SetVoiceMix(object handle, float volume, float pan)
    {
        var instance = FindTracked(handle);
        if (instance is null) return;

        instance.Volume = Math.Clamp(volume, 0f, 1f);
        if (instance is IPanningVoice panning)
            panning.Pan = Math.Clamp(pan, -1f, 1f);
    }

    private IAudioInstance? FindTracked(object handle)
    {
        foreach (var instance in _playing)
            if (ReferenceEquals(instance, handle))
                return instance;
        return null;
    }

    /// <inheritdoc />
    public void StopVoice(object handle)
    {
        // Handle identity: the instance itself. Untracked or stale handles (other backends,
        // already-reaped instances) simply do not match _playing.
        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_playing[i], handle)) continue;
            if (_playing[i].IsPlaying)
            {
                _playing[i].Stop();
                _playing[i].Dispose();
            }
            _playing.RemoveAt(i);
            return;
        }
    }

    /// <summary>Stops and disposes every instance this backend started (a hard cut, not a fade).</summary>
    public void Stop()
    {
        foreach (var instance in _playing)
        {
            instance.Stop();
            instance.Dispose();
        }
        _playing.Clear();
    }

    /// <summary>Stops and disposes every instance this backend started.</summary>
    public void Dispose() => Stop();

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
