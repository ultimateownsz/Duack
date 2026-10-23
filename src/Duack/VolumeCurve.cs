namespace Duack;

/// <summary>
/// Maps a linear slider position to an amplitude through a perceptual curve (fader law). Human loudness
/// perception is closer to logarithmic, so a linear slider feeding a linear amplitude puts almost
/// all of the audible change in the bottom of the travel. This quadratic approximation makes the
/// midpoint noticeably quieter than a linear slider would (0.5 slider -&gt; 0.25 amplitude), which
/// reads as a more natural volume control. Works for any volume control - music or sound effects -
/// and <see cref="ToSlider"/> is the exact inverse. See also <see cref="Gain"/> for studio knob terminology.
/// </summary>
public static class VolumeCurve
{
    /// <summary>Converts a slider position in [0, 1] to an amplitude in [0, 1].</summary>
    public static float ToAmplitude(float slider) => Gain.ToAmplitude(slider);

    /// <summary>Converts a slider position in [0, 1] to a linear gain in [0, 1].</summary>
    public static float ToGain(float slider) => Gain.ToGain(slider);

    /// <summary>
    /// Converts an amplitude in [0, 1] back to a slider position in [0, 1] (the inverse of
    /// <see cref="ToAmplitude"/>).
    /// </summary>
    public static float ToSlider(float amplitude) => Gain.ToSlider(amplitude);
}
