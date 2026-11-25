namespace Duack;

/// <summary>
/// Optional pan capability for <see cref="IAudioInstance"/> implementations: a stereo position
/// in [-1, 1] the platform applies to that voice (0 = centered). Instances without it simply
/// play centered — the same additive fallback an <see cref="ISfxBackend"/> without the
/// <see cref="ISfxPositionalBackend"/> seam gets. The desktop platform pan is a linear taper,
/// not the constant-power law the web panner applies; that reproduction difference is recorded
/// on the slice, not recombined here.
/// </summary>
public interface IPanningVoice
{
    /// <summary>Stereo pan of this voice in [-1, 1]; implementations clamp on set.</summary>
    float Pan { get; set; }
}
