using System;
using Microsoft.Xna.Framework.Audio;

namespace Duack.MonoGame;

/// <summary>
/// <see cref="IAudioInstance"/> backed by a MonoGame <see cref="SoundEffectInstance"/>.
/// </summary>
internal sealed class SoundEffectAudioInstance : IAudioInstance
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

    public bool IsPlaying => _instance.State == SoundState.Playing;

    public void Play() => _instance.Play();

    public void Stop() => _instance.Stop();

    public void Pause() => _instance.Pause();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _instance.Dispose();
    }
}