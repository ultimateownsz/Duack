using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Duack;

/// <summary>Per-source ducking parameters: how far the music dips and how fast.</summary>
public class DuckSourceConfig
{
    /// <summary>Target music ratio while this source is active (0 = silent, 1 = no ducking).</summary>
    public float Ratio { get; set; } = 0.3f;

    /// <summary>Time constant for easing the music down to <see cref="Ratio"/>.</summary>
    public float AttackSeconds { get; set; } = 0.06f;

    /// <summary>How long the music stays ducked before releasing. 0 = release immediately.</summary>
    public float HoldSeconds { get; set; }

    /// <summary>Time constant for easing the music back to full volume.</summary>
    public float ReleaseSeconds { get; set; } = 0.35f;
}

/// <summary>Music mix: master volume, per-context scale, crossfade, and duck sources.</summary>
public class MusicConfig
{
    /// <summary>
    /// Master volume for music in the range [0, 1]. Default is 1.
    /// </summary>
    public float MasterVolume { get; set; } = 1f;

    /// <summary>Fade time when switching between tracks.</summary>
    public float CrossfadeSeconds { get; set; } = 1.2f;

    /// <summary>Per-music-context volume scale (keys are context names).</summary>
    public Dictionary<string, float> Contexts { get; set; } = new();

    /// <summary>Named duck sources (keys are the names referenced from code: "sfx", "dialogue", ...).</summary>
    public Dictionary<string, DuckSourceConfig> DuckSources { get; set; } = new();

    /// <summary>
    /// Optional per-play pitch/volume jitter for music cues. Null (omitted from JSON) means no jitter.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CueVariation? CueVariation { get; set; }
}

/// <summary>Sound-effect mix settings.</summary>
public class SfxConfig
{
    /// <summary>
    /// Master volume for sound effects in the range [0, 1]. Default is 1.
    /// </summary>
    public float MasterVolume { get; set; } = 1f;

    /// <summary>
    /// Optional per-play pitch/volume jitter for sound-effect cues (footsteps, hits). Null (omitted
    /// from JSON) means no jitter.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CueVariation? CueVariation { get; set; }
}

/// <summary>
/// The audio mix configuration, authored in code (see <see cref="AudioConfigBuilder"/>).
/// The mix (masters, per-context scales, crossfade, duck sources, and cue variation) is a plain
/// object graph.
/// </summary>
public class AudioConfig
{
    /// <summary>Global output master volume applied to both music and sound effects.</summary>
    public float MasterVolume { get; set; } = 1f;

    /// <summary>
    /// Music configuration.
    /// </summary>
    public MusicConfig Music { get; set; } = new();
    /// <summary>
    /// Sound effects configuration.
    /// </summary>
    public SfxConfig Sfx { get; set; } = new();

    /// <summary>Clamps values to safe ranges.</summary>
    public void Sanitize()
    {
        MasterVolume = System.Math.Clamp(MasterVolume, 0f, 1f);
        Music.MasterVolume = System.Math.Clamp(Music.MasterVolume, 0f, 1f);
        Sfx.MasterVolume = System.Math.Clamp(Sfx.MasterVolume, 0f, 1f);
        Music.CrossfadeSeconds = System.Math.Clamp(Music.CrossfadeSeconds, 0.1f, 10f);

        foreach (var context in Music.Contexts.Keys.ToList())
            Music.Contexts[context] = System.Math.Clamp(Music.Contexts[context], 0f, 1f);

        foreach (var source in Music.DuckSources.Values)
        {
            source.Ratio = System.Math.Clamp(source.Ratio, 0f, 1f);
            source.AttackSeconds = System.Math.Max(0f, source.AttackSeconds);
            source.HoldSeconds = System.Math.Max(0f, source.HoldSeconds);
            source.ReleaseSeconds = System.Math.Max(0f, source.ReleaseSeconds);
        }

        if (Music.CueVariation is { } musicVariation)
            Music.CueVariation = new CueVariation(musicVariation.PitchRange, musicVariation.VolumeRange);
        if (Sfx.CueVariation is { } sfxVariation)
            Sfx.CueVariation = new CueVariation(sfxVariation.PitchRange, sfxVariation.VolumeRange);
    }
}
