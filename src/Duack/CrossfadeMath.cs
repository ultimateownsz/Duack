using System;

namespace Duack;

/// <summary>
/// Pure crossfade curves shared by both music backends. The desktop instance mixer overlaps two
/// tracks; the web head (a single <c>MediaPlayer</c>) fades out, switches, then fades in. Both drive
/// these curves so a transition sounds the same wherever overlapping is possible.
///
/// <para>
/// <see cref="EqualPower"/> is an equal-power (constant-power) fade: <c>InÂ² + OutÂ² == 1</c>, so the
/// two tracks never produce the ~-3 dB power dip a linear amplitude fade has. The S-curve
/// (<see cref="SmoothStep"/>) is applied to the progress first, which gives both gains zero slope at
/// the ends - the incoming track gradually fades in rather than jumping. This replaced a first-order
/// exponential ease on both gains, which was front-loaded and dipped in the middle.
/// </para>
/// </summary>
public static class CrossfadeMath
{
    /// <summary>
    /// Normalized 0..1 progress of a fade. A non-positive or non-finite duration counts as complete
    /// (an effect that never starts must still end), and non-finite elapsed time clamps to the ends.
    /// </summary>
    public static float Progress(float elapsedSeconds, float durationSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || !float.IsFinite(durationSeconds) || durationSeconds <= 0f)
            return 1f;

        return Math.Clamp(elapsedSeconds / durationSeconds, 0f, 1f);
    }

    /// <summary>Smoothstep ease (zero slope at 0 and 1), clamped; non-finite progress is treated as 1.</summary>
    public static float SmoothStep(float progress)
    {
        if (!float.IsFinite(progress))
            return 1f;

        var p = Math.Clamp(progress, 0f, 1f);
        return p * p * (3f - 2f * p);
    }

    /// <summary>
    /// Equal-power crossfade gains for <paramref name="progress"/> (the S-curve is applied
    /// internally). <c>In</c> rises 0 -> 1 and <c>Out</c> falls 1 -> 0 with <c>InÂ² + OutÂ² == 1</c>,
    /// so total power is constant across the whole fade.
    /// <para>
    /// The gains are clamped to [0, 1] because they are pushed straight into volume setters that
    /// REJECT out-of-range values (<c>SoundEffectInstance.Volume</c>, <c>MediaPlayer.Volume</c>).
    /// This is not cosmetic: <c>MathF.Cos(MathF.PI / 2f)</c> is <c>~-4.4e-8</c> (the float constant
    /// overshoots a true quarter turn), so the un-clamped <c>Out</c> gain at the end of a fade is a
    /// tiny NEGATIVE number - enough to throw <c>ArgumentOutOfRangeException</c> on desktop for
    /// every completed crossfade.
    /// </para>
    /// </summary>
    public static (float In, float Out) EqualPower(float progress)
    {
        var angle = SmoothStep(progress) * (MathF.PI / 2f);
        return (
            Math.Clamp(MathF.Sin(angle), 0f, 1f),
            Math.Clamp(MathF.Cos(angle), 0f, 1f));
    }
}
