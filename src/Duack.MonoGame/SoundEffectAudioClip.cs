using System;
using Microsoft.Xna.Framework.Audio;

namespace Duack.MonoGame;

/// <summary>
/// <see cref="IAudioClip"/> backed by a MonoGame <see cref="SoundEffect"/>. The effect is owned by
/// this wrapper and disposed with it; each <see cref="CreateInstance"/> returns an independent
/// playback instance.
/// </summary>
internal sealed class SoundEffectAudioClip : IAudioClip
{
    private readonly SoundEffect _effect;
    private bool _disposed;

    public SoundEffectAudioClip(string name, SoundEffect effect)
    {
        Name = name;
        _effect = effect;
    }

    /// <summary>The track name this clip was resolved from (for diagnostics).</summary>
    public string Name { get; }

    public IAudioInstance CreateInstance() => new SoundEffectAudioInstance(_effect.CreateInstance());

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _effect.Dispose();
    }
}