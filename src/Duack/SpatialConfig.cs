namespace Duack;

/// <summary>
/// Sound-effect spatial settings: how the listener-relative position of a source maps to pan
/// and gain. Distances are in the same world units a positional play passes. Authored via
/// <see cref="AudioConfigBuilder.WithSpatial"/>; the curve defaults are revisitable until the
/// maintainer debrief says which of them are taste (see the vNext slice STATUS).
/// </summary>
public class SpatialConfig
{
    /// <summary>Distance at which a source begins to attenuate (gain 1 at or below this).</summary>
    public float ReferenceDistance { get; set; } = 1f;

    /// <summary>The mute floor: at or beyond this distance a source is not played at all.</summary>
    public float MaxDistance { get; set; } = 32f;

    /// <summary>Strength of the fall-off between the reference and maximum distance.</summary>
    public float Rolloff { get; set; } = 1f;

    /// <summary>Offset at which the pan curve has closed most of its travel (world units).</summary>
    public float PanDistance { get; set; } = 12f;

    /// <summary>Pan ceiling in [-1, 1] the curve approaches at large offsets (never fully ±1).</summary>
    public float PanSaturation { get; set; } = 0.7f;

    /// <summary>The fall-off shape between the reference and maximum distance.</summary>
    public DistanceModel Model { get; set; } = DistanceModel.Inverse;

    /// <summary>Clamps all values to safe ranges (the config/builder path calls this).</summary>
    public void Sanitize()
    {
        ReferenceDistance = Sanitize(ReferenceDistance, fallback: 1f, minimum: 0.01f);
        // The max distance must stay one step past the reference: the mute floor of a config
        // that collapses both together would mute everything, including at the listener.
        MaxDistance = Sanitize(MaxDistance, fallback: 32f, minimum: ReferenceDistance + 0.01f);
        Rolloff = Sanitize(Rolloff, fallback: 1f, minimum: 0f);
        PanDistance = Sanitize(PanDistance, fallback: 12f, minimum: 0.01f);
        PanSaturation = Sanitize(PanSaturation, fallback: 0.7f, minimum: 0f, maximum: 1f);
    }

    /// <summary>
    /// A non-finite value is reset to the field's default; a finite one is clamped to the
    /// field's floor (and ceiling, where one exists).
    /// </summary>
    private static float Sanitize(float value, float fallback, float minimum, float? maximum = null)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
        var clamped = Math.Max(value, minimum);
        return maximum is { } ceiling && clamped > ceiling ? ceiling : clamped;
    }
}
