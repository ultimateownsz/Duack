using System;
using Microsoft.Xna.Framework.Audio;

namespace Duack.MonoGame;

/// <summary>
/// <see cref="IAudioClip"/> backed by a MonoGame <see cref="SoundEffect"/>. The effect is owned by
/// this wrapper and disposed with it; each <see cref="CreateInstance"/> returns an independent
/// playback instance.
///
/// <para>
/// When constructed with an offset decoder, the clip also implements the seek capability
/// (<see cref="ISeekableAudioClip"/>): an offset instance plays from a freshly decoded buffer
/// that starts at the offset — a <see cref="SoundEffect"/> cannot seek, so the decode is the
/// workaround. Such a buffer is transient (not in the clip cache), so it is paired with its
/// instance and released when the backend disposes it, keeping steady-state memory unchanged.
/// A failed offset decode returns null and the backend keeps playing what it has.
/// </para>
/// </summary>
internal sealed class SoundEffectAudioClip : IAudioClip, ISeekableAudioClip
{
    private readonly SoundEffect _effect;
    private readonly Func<TimeSpan, Stream?>? _offsetStreamFor;
    private readonly Dictionary<SoundEffectAudioInstance, SoundEffect> _transient = new();
    private bool _disposed;

    public SoundEffectAudioClip(string name, SoundEffect effect, Func<TimeSpan, Stream?>? offsetStreamFor = null)
    {
        Name = name;
        _effect = effect;
        _offsetStreamFor = offsetStreamFor;
    }

    /// <summary>The track name this clip was resolved from (for diagnostics).</summary>
    public string Name { get; }

    IAudioInstance IAudioClip.CreateInstance() => new SoundEffectAudioInstance(_effect.CreateInstance());

    /// <inheritdoc />
    public IAudioInstance? CreateInstance(TimeSpan startOffset)
    {
        if (_offsetStreamFor?.Invoke(startOffset) is not { } stream) return null;
        SoundEffect transient;
        try
        {
            transient = SoundEffect.FromStream(stream);
        }
        catch (Exception)
        {
            stream.Dispose();
            return null;
        }
        stream.Dispose();
        var instance = new SoundEffectAudioInstance(transient.CreateInstance());
        _transient[instance] = transient;
        return instance;
    }

    /// <inheritdoc />
    public void Release(IAudioInstance instance)
    {
        if (instance is SoundEffectAudioInstance key && _transient.Remove(key, out var effect))
            effect.Dispose();
    }

    /// <summary>The platform clip exposes its decoded length.</summary>
    public float? DurationSeconds => (float)_effect.Duration.TotalSeconds;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _effect.Dispose();
    }
}