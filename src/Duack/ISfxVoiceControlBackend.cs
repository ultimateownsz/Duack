namespace Duack;

/// <summary>
/// Optional additive seam for sfx backends that can stop an individual started voice.
/// Implementing it lets <see cref="SfxManager"/> make voice stealing audible: without it,
/// a stolen voice only loses its bookkeeping slot and keeps sounding. The base
/// <see cref="ISfxBackend.Play"/> contract stays unchanged, and a manager falls back to
/// slot-only stealing when the backend does not implement this interface — only the acoustic
/// result differs (ADR-8 pattern), not the observable config/result API.
/// </summary>
public interface ISfxVoiceControlBackend
{
    /// <summary>
    /// Plays a cue exactly as <see cref="ISfxBackend.Play"/> and returns an opaque handle for the
    /// started voice, or null when the backend refused (same meaning as a false from
    /// <see cref="ISfxBackend.Play"/>). The handle stays valid until the voice finishes or is
    /// stopped; handing it to <see cref="StopVoice"/> afterwards is a no-op.
    /// </summary>
    object? PlayTracked(string cueName, float pitch = 0f, float volume = 1f);

    /// <summary>Stops the voice identified by <paramref name="handle"/>. No-op when the voice
    /// already finished or the handle is unknown/stale.</summary>
    void StopVoice(object handle);
}
