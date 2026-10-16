namespace Duack;

/// <summary>
/// Maps a linear slider position to an amplitude through a perceptual curve. Human loudness
/// perception is closer to logarithmic, so a linear slider feeding a linear amplitude puts almost
/// all of the audible change in the bottom of the travel. This quadratic approximation makes the
/// midpoint noticeably quieter than a linear slider would (0.5 slider -&gt; 0.25 amplitude), which
/// reads as a more natural volume control. <see cref="ToSlider"/> is the exact inverse.
/// </summary>
public static class VolumeCurve
{
    /// <summary>Converts a slider position in [0, 1] to an amplitude in [0, 1].</summary>
    public static float ToAmplitude(float slider)
    {
        var t = Normalize(slider);
        return t * t;
    }

    /// <summary>
    /// Converts an amplitude in [0, 1] back to a slider position in [0, 1] (the inverse of
    /// <see cref="ToAmplitude"/>).
    /// </summary>
    public static float ToSlider(float amplitude) => MathF.Sqrt(Normalize(amplitude));

    private static float Normalize(float value) =>
        float.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);
}
