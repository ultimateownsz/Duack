using System.Text.Json.Serialization;

namespace Duack;

/// <summary>
/// Per-play jitter for an audio cue (a one-shot sound effect such as a footstep/hit, or a music
/// stinger): a pitch range and a volume range applied on top of the cue's base settings so repeated
/// plays do not sound machine-gunned. All values are pure - <see cref="Resolve"/> takes the
/// <see cref="Random"/> it should use, so a seeded generator makes the jitter reproducible in tests.
/// </summary>
public readonly struct CueVariation
{
    /// <summary>No jitter: every play is at the base pitch and volume.</summary>
    public static CueVariation None => default;

    /// <summary>Half-width of the pitch jitter, in engine pitch units (0 = unchanged, 1 = one octave).</summary>
    public float PitchRange { get; }

    /// <summary>Half-width of the volume jitter, as a linear scale around 1 (0.2 = +/-20%).</summary>
    public float VolumeRange { get; }

    /// <param name="pitchRange">Half-width of the pitch jitter; negative or non-finite values become 0.</param>
    /// <param name="volumeRange">Half-width of the volume jitter; negative or non-finite values become 0.</param>
    [JsonConstructor]
    public CueVariation(float pitchRange, float volumeRange)
    {
        PitchRange = pitchRange > 0f && float.IsFinite(pitchRange) ? pitchRange : 0f;
        VolumeRange = volumeRange > 0f && float.IsFinite(volumeRange) ? volumeRange : 0f;
    }

    /// <summary>
    /// Resolves the pitch and volume scale for a single play. Pitch is clamped to the engine's
    /// [-1, 1] range and the volume scale is floored at 0.
    /// </summary>
    public CuePlay Resolve(Random? random = null)
    {
        var r = random ?? Random.Shared;

        var pitch = PitchRange == 0f
            ? 0f
            : (float)((r.NextDouble() * 2.0 - 1.0) * PitchRange);
        pitch = Math.Clamp(pitch, -1f, 1f);

        var volume = VolumeRange == 0f
            ? 1f
            : (float)(1.0 + (r.NextDouble() * 2.0 - 1.0) * VolumeRange);
        if (volume < 0f)
            volume = 0f;

        return new CuePlay(pitch, volume);
    }
}

/// <summary>The one-shot pitch/volume an audio cue should play with, produced by <see cref="CueVariation.Resolve"/>.</summary>
public readonly struct CuePlay
{
    public CuePlay(float pitch, float volumeScale)
    {
        Pitch = pitch;
        VolumeScale = volumeScale;
    }

    /// <summary>Engine pitch to play with (0 = unchanged, +/-1 = one octave).</summary>
    public float Pitch { get; }

    /// <summary>Linear volume scale relative to the effect's base volume.</summary>
    public float VolumeScale { get; }
}
