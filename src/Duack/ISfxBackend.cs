namespace Duack;

/// <summary>
/// Backend abstraction for one-shot sound effects. Adapters implement this over engine-specific
/// playback (MonoGame <c>SoundEffectInstance</c>, a host's sample player, ...); the core
/// <see cref="SfxDirector"/> owns cue validation and the automatic music ducking and only calls
/// through after a cue is known, keeping the adapter a thin play-this-now layer.
/// </summary>
public interface ISfxBackend
{
    /// <summary>
    /// Starts the one-shot cue <paramref name="cueName"/> with the given pitch (engine units,
    /// 0 = natural) and volume (0..1). Returns false when the cue cannot be played (missing
    /// clip, engine refusal).
    /// </summary>
    bool Play(string cueName, float pitch, float volume);
}
