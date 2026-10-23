using System;

namespace Duack;

/// <summary>
/// Gain and perceptual fader-law conversions. A mixer knob or fader position in [0, 1] is mapped to
/// linear gain/amplitude in [0, 1] through a perceptual (quadratic) curve.
/// <para>
/// Human loudness perception is closer to logarithmic, so a linear slider/fader feeding linear
/// amplitude puts almost all of the audible change in the bottom of the travel. This fader law
/// makes the midpoint noticeably quieter than a linear slider would (0.5 slider -&gt; 0.25 gain),
/// which reads as a more natural volume/gain control. Works for any volume or mixer gain knob,
/// and <see cref="ToSlider"/> is the exact inverse.
/// </para>
/// </summary>
public static class Gain
{
    /// <summary>Converts a slider/fader position in [0, 1] to a linear gain/amplitude in [0, 1].</summary>
    public static float ToAmplitude(float slider)
    {
        var t = Normalize(slider);
        return t * t;
    }

    /// <summary>Converts a slider/fader position in [0, 1] to a linear gain in [0, 1].</summary>
    public static float ToGain(float slider) => ToAmplitude(slider);

    /// <summary>Converts a slider/fader position in [0, 1] to a linear gain in [0, 1].</summary>
    public static float FromSlider(float slider) => ToAmplitude(slider);

    /// <summary>
    /// Converts a linear gain/amplitude in [0, 1] back to a slider/fader position in [0, 1] (the inverse of
    /// <see cref="ToAmplitude"/> / <see cref="ToGain"/>).
    /// </summary>
    public static float ToSlider(float gain) => MathF.Sqrt(Normalize(gain));

    private static float Normalize(float value) =>
        float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);
}
