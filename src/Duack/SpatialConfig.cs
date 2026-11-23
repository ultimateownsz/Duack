using System;

namespace Duack;

/// <summary>
/// The distance-attenuation models the SFX layer understands, mirroring the Web Audio
/// <c>PannerNode</c> distance models so both backends stay comparable.
/// </summary>
public enum DistanceModel
{
    /// <summary>Linear fall-off between the reference and maximum distance.</summary>
    Linear,

    /// <summary>Web Audio "inverse": free-field pressure spreading, g = dref / (dref + r·(d − dref)).</summary>
    Inverse,

    /// <summary>Web Audio "exponential": g = (dref / d)<sup>r</sup>, reaching 0 exactly at dmax.</summary>
    Exponential,
}

/// <summary>
/// Sound-effect spatial settings: how distance and listener-relative position map to pan and
/// gain. Distances are in the same world units a positional play passes.
/// </summary>
public class SpatialConfig
{
    /// <summary>Distance at which a source begins to attenuate (gain 1 at or below this).</summary>
    public float ReferenceDistance { get; set; } = 1f;

    /// <summary>Distance at which a source is fully muted (gain 0); a distance beyond this releases the voice.</summary>
    public float MaxDistance { get; set; } = 32f;

    /// <summary>Strength of the fall-off between the reference and maximum distance.</summary>
    public float Rolloff { get; set; } = 1f;

    /// <summary>How far a source may sit off-centre before its pan saturates (world units).</summary>
    public float PanDistance { get; set; } = 12f;

    /// <summary>Pan value in [-1, 1] a source at the pan distance (or beyond) carries (debrief-tunable).</summary>
    public float PanSaturation { get; set; } = 0.7f;

    /// <summary>Sharpness of the pan approach to the saturation value (p = psat·tanh(x_norm / σ)).</summary>
    public float PanSigma { get; set; } = 0.5f;

    /// <summary>Which distance-attenuation curve to use.</summary>
    public DistanceModel Model { get; set; } = DistanceModel.Linear;

    /// <summary>Clamps all values to safe ranges (the config/builder path calls this).</summary>
    public void Sanitize()
    {
        ReferenceDistance = Math.Max(ReferenceDistance, 0.01f);
        MaxDistance = Math.Max(MaxDistance, ReferenceDistance + 0.01f);
        PanDistance = Math.Max(PanDistance, 0.01f);
        PanSaturation = Math.Clamp(PanSaturation, 0f, 1f);
        PanSigma = Math.Max(PanSigma, 0.01f);
        Rolloff = Math.Max(Rolloff, 0f);
    }
}
