using System;

namespace Duack;

/// <summary>Normative 2D spatial math (SHARED §12): pan saturation, distance models, mute floor.</summary>
public static class SpatialMath
{
    /// <summary>
    /// Constant-power pan (SHARED §12): theta = (p + 1)·pi/4, left gain = cos(theta),
    /// right gain = sin(theta), left^2 + right^2 = 1 for every p.
    /// </summary>
    public static (float Left, float Right) ConstantPower(float pan)
    {
        var theta = (pan + 1f) * MathF.PI / 4f;
        return (MathF.Cos(theta), MathF.Sin(theta));
    }

    /// <summary>
    /// Saturated pan for a listener-space x offset in world units: p = psat·tanh(x_norm / sigma)
    /// with x_norm = x / panDistance, so a source at the pan distance (or beyond) carries the
    /// full <paramref name="panSaturation"/> of pan and the law stays monotone in between.
    /// </summary>
    public static float Pan(float offsetX, float panDistance, float panSaturation, float panSigma)
    {
        if (panDistance <= 0f) return 0f; // degenerate config: never pan
        if (panSigma <= 0f) return 0f;
        var xNorm = offsetX / panDistance;
        return panSaturation * MathF.Tanh(xNorm / panSigma);
    }

    /// <summary>
    /// Distance attenuation in [0, 1] for a listener-relative distance d: every model yields
    /// gain 1 at d &lt;= dref and clamps to the mute floor g = 0 at d &gt;= dmax. A rolloff of 0
    /// holds gain 1 except exactly at dmax; a rolloff of 1 gives linear or 1/d behavior.
    /// </summary>
    public static float DistanceGain(float d, float dref, float dmax, DistanceModel model, float rolloff)
    {
        if (d <= dref) return 1f;
        if (d >= dmax) return 0f;

        switch (model)
        {
            case DistanceModel.Linear:
                return Math.Max(0f, 1f - rolloff * (d - dref) / Math.Max(dmax - dref, 1e-6f));
            case DistanceModel.Inverse:
                // Web Audio "inverse", normalized so g(dref) = 1: dref / (dref + r·(d - dref))
                return dref / (dref + rolloff * (d - dref));
            case DistanceModel.Exponential:
                // Web Audio "exponential" shape, clamped to reach exactly 0 at dmax
                var g = MathF.Pow(dref / Math.Max(d, dref), rolloff);
                return Math.Max(0f, g);
            default:
                return 1f;
        }
    }
}
