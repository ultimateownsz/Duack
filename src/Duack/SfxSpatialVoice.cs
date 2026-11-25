namespace Duack;

/// <summary>
/// A live positional voice started by <see cref="SfxManager.PlayAt"/>: move it with
/// <see cref="SetPosition"/> (the manager recomposes pan and distance gain against the current
/// listener through the backend's <see cref="ISfxPositionalBackend"/> seam) and stop it with
/// <see cref="Stop"/>. Going out of range silences the voice at the mute floor — it does not
/// stop it — and the manager stops following a voice's position after its assumed one-shot
/// lifetime has passed, so finished cues never pin their record down.
/// Positions are the same world-space coordinates <see cref="SfxManager.SetListener"/> takes.
/// </summary>
public sealed class SfxSpatialVoice
{
    private readonly SfxManager _owner;

    internal SfxSpatialVoice(SfxManager owner, object backendHandle, string? bus, float baseVolume, float sourceX, float sourceY)
    {
        _owner = owner;
        BackendHandle = backendHandle;
        Bus = bus;
        BaseVolume = baseVolume;
        SourceX = sourceX;
        SourceY = sourceY;
    }

    internal object BackendHandle { get; }
    internal string? Bus { get; }
    internal float BaseVolume { get; }
    internal float SourceX { get; set; }
    internal float SourceY { get; set; }
    internal float AgeSeconds { get; set; }

    /// <summary>Moves the source and re-mixes the voice for its new position. A no-op once the voice stopped.</summary>
    public void SetPosition(float sourceX, float sourceY) => _owner.RemixVoice(this, sourceX, sourceY);

    /// <summary>Stops the voice: silence now, the voice slot released by the limiter's lifetime bookkeeping.</summary>
    public void Stop() => _owner.StopSpatialVoice(this);
}
