namespace Duack;

/// <summary>
/// Additive positional capability for SFX backends: playing a cue with a pan and re-mixing a
/// live voice over time. Extends both existing seams — <see cref="ISfxBackend"/> (play) and
/// <see cref="ISfxVoiceControlBackend"/> (per-voice handles) — because a pan and a remix are
/// per-voice operations on the handles the control seam already hands out. A manager falls
/// back to mono-centered playback (composed volume, pan dropped, no remix) when the backend
/// does not implement this — the established additive-capability pattern, an acoustic
/// difference only, never an API difference. Pan is computed once in core
/// <see cref="SpatialMath"/>; a backend applies the number, it does not compute it.
/// </summary>
public interface ISfxPositionalBackend : ISfxBackend, ISfxVoiceControlBackend
{
    /// <summary>
    /// Plays a tracked voice carrying a pan in [-1, 1] (0 = centered) on top of the volume the
    /// core already composed (bus gains, duck, distance attenuation). Returns the same kind of
    /// opaque handle <see cref="ISfxVoiceControlBackend.PlayTracked"/> does — the playing voice
    /// itself — or null when refused.
    /// </summary>
    object? PlayTrackedAt(string cueName, float pan, float pitch = 0f, float volume = 1f);

    /// <summary>
    /// Re-mixes one live voice: a new pan and a new volume the core composed for its current
    /// position (a moving source, a moving listener). A stale or finished handle is a no-op —
    /// the same contract <see cref="ISfxVoiceControlBackend.StopVoice"/> keeps.
    /// </summary>
    void SetVoiceMix(object handle, float volume, float pan);
}
