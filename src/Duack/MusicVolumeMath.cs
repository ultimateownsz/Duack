namespace Duack;

/// <summary>
/// The music volume the game's <c>MusicController</c> applies per frame. Music volume is owned here,
/// not by the engine: <c>FlatRedBallService.Default.Audio.MusicVolume</c> only feeds FRB2's own
/// music backend, and this project plays music through <c>SoundEffectInstance</c> (desktop) or
/// <c>MediaPlayer</c> (web) instead, so nothing reads that field.
/// </summary>
public static class MusicVolumeMath
{
    /// <summary>
    /// Combines the authored/player master volume, the per-context scale, the ducking ratio and the
    /// track fade into the final gain. Clamped to [0, 1] for both backends, and a non-finite factor
    /// (hand-edited config) yields silence rather than an exception or an out-of-range volume.
    /// </summary>
    public static float Effective(float master, float contextScale, float duckRatio, float fade)
    {
        if (!float.IsFinite(master) ||
            !float.IsFinite(contextScale) ||
            !float.IsFinite(duckRatio) ||
            !float.IsFinite(fade))
        {
            return 0f;
        }

        return Math.Clamp(master * contextScale * duckRatio * fade, 0f, 1f);
    }
}
