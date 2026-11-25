using System;

namespace Duack;

/// <summary>
/// The distance-attenuation models the SFX layer understands. Both reach 1 at the reference
/// distance and 0 at the maximum, so the choice is the shape between them; the two presets
/// mirror Web Audio's naming spirit so backends stay comparable (SHARED section 12).
/// </summary>
public enum DistanceModel
{
    /// <summary>Straight-line fall-off between the reference and maximum distance.</summary>
    Linear,

    /// <summary>Free-field pressure spreading, g = dref / (dref + rolloff·(d − dref)).</summary>
    Inverse,
}

/// <summary>Normative 2D spatial math (SHARED section 12): the pan law, saturation, distance models.</summary>
public static class SpatialMath
{
    /// <summary>
    /// Constant-power pan (SHARED section 12): theta = (p + 1)·pi/4, so left = cos(theta) and
    /// right = sin(theta) keep left² + right² = 1 for every p — the law the web
    /// <c>StereoPannerNode</c> applies natively. The desktop platform pan is a linear taper
    /// instead; that reproduction difference is backend territory and deliberately not
    /// recombined here.
    /// </summary>
    public static (float Left, float Right) ConstantPower(float pan)
    {
        var theta = (pan + 1f) * MathF.PI / 4f;
        return (MathF.Cos(theta), MathF.Sin(theta));
    }

    /// <param name="x">Listener-space horizontal offset of the source (world units).</param>
    /// <param name="panDistance">Offset at which the curve has closed most of its travel.</param>
    /// <param name="panSaturation">Pan ceiling in [-1, 1] the curve approaches at large offsets.</param>
    /// <summary>
    /// Saturating pan for a listener-space offset: p = sat·tanh(x / panDistance), so the panning
    /// is audible well before the viewport edge and never exceeds the authored ceiling (a hard
    /// ±1 at the edge reads artificial). Non-finite or degenerate inputs — a NaN offset, a
    /// non-positive pan distance, a non-finite saturation — center the sound instead.
    /// </summary>
    public static float Pan(float x, float panDistance, float panSaturation)
    {
        if (!IsFinite(x) || !IsFinite(panSaturation) || panSaturation is < 0f or > 1f)
            return 0f;
        if (!IsFinite(panDistance) || panDistance <= 0f || panSaturation == 0f)
            return 0f;

        return panSaturation * MathF.Tanh(x / panDistance);
    }

    /// <param name="d">Listener-relative distance of the source (world units).</param>
    /// <param name="dref">Distance at and below which the cue plays at full volume.</param>
    /// <param name="dmax">Distance at and beyond which the cue is fully muted (voice release).</param>
    /// <param name="model">The fall-off shape between dref and dmax.</param>
    /// <param name="rolloff">Strength of the fall-off; 0 holds full volume until the mute floor.</param>
    /// <summary>
    /// Distance attenuation in [0, 1]: exactly 1 at d ≤ dref, exactly 0 at d ≥ dmax (the mute
    /// floor — a cue past it is not played at all), monotone in between. A non-finite distance
    /// is treated as sounding at the listener: a NaN position is a caller bug, not a cue at the
    /// mute floor.
    /// </summary>
    public static float DistanceGain(float d, float dref, float dmax, DistanceModel model, float rolloff)
    {
        if (!IsFinite(d)) return 1f;
        if (d >= dmax) return 0f;
        if (d <= dref) return 1f;
        if (rolloff <= 0f || !IsFinite(rolloff)) return 1f;

        return model switch
        {
            // Web Audio's "inverse", normalized so g(dref) = 1: free-field pressure spreading.
            DistanceModel.Inverse =>
                Math.Clamp(dref / (dref + rolloff * (d - dref)), 0f, 1f),
            _ => Math.Clamp(1f - rolloff * (d - dref) / Math.Max(dmax - dref, 1e-6f), 0f, 1f),
        };
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
