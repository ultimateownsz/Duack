using System;
using Microsoft.Xna.Framework.Audio;

namespace Duack.MonoGame;

/// <summary>
/// <see cref="IAudioInstance"/> backed by a MonoGame <see cref="SoundEffectInstance"/>.
/// Implements <see cref="IPanningVoice"/> on the platform's own pan property (a linear taper —
/// the desktop reproduction difference the core docs record).
/// </summary>
internal sealed class SoundEffectAudioInstance : IAudioInstance, IPanningVoice
{
    private readonly SoundEffectInstance _instance;
    private bool _disposed;

    public SoundEffectAudioInstance(SoundEffectInstance instance) => _instance = instance;

    public bool IsLooped
    {
        get => _instance.IsLooped;
        set => _instance.IsLooped = value;
    }

    public float Volume
    {
        get => _instance.Volume;
        set => _instance.Volume = value;
    }

    public float Pitch
    {
        get => _instance.Pitch;
        set => _instance.Pitch = value;
    }

    public float Pan
    {
        get => _instance.Pan;
        set => _instance.Pan = value;
    }

    public bool IsPlaying => _instance.State == SoundState.Playing;

    public void Play() => _instance.Play();

    public void Stop() => _instance.Stop();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _instance.Dispose();
    }
}