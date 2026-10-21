using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
/// Per-track loudness normalization targets for the audio preprocessing pipeline
/// (loudnorm: I = integrated loudness in LUFS, TP = true peak in dBTP, LRA = loudness range in LU).
/// </summary>
public class LoudnormConfig
{
    /// <summary>
    /// Integrated loudness in LUFS.
    /// </summary>
    public double I { get; set; }

    /// <summary>
    /// True peak in dBTP.
    /// </summary>
    public double Tp { get; set; }

    /// <summary>
    /// Loudness range in LU.
    /// </summary>
    public double Lra { get; set; }
}

/// <summary>
/// Optional per-track compressor settings for the audio preprocessing pipeline
/// (threshold/makeup in dB, ratio as a multiplier, attack/release in ms).
/// </summary>
public class CompressConfig
{
    /// <summary>
    /// Threshold in dB.
    /// </summary>
    public double Threshold { get; set; }

    /// <summary>
    /// Ratio as a multiplier.
    /// </summary>
    public double Ratio { get; set; }

    /// <summary>
    /// Attack time in ms.
    /// </summary>
    public double AttackMs { get; set; }

    /// <summary>
    /// Release time in ms.
    /// </summary>
    public double ReleaseMs { get; set; }

    /// <summary>
    /// Makeup gain in dB.
    /// </summary>
    public double MakeupDb { get; set; }
}

/// <summary>One build-time track's loudness/compress targets (the "tracks" section).</summary>
public class TrackConfig
{
    /// <summary>
    /// Loudness normalization targets for the audio preprocessing pipeline (loudnorm: I, TP, LRA).
    /// </summary>
    public LoudnormConfig Loudnorm { get; set; } = new();

    /// <summary>Null when the track has no compressor settings.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompressConfig? Compress { get; set; }
}

/// <summary>
/// The audio mix configuration, authored in code (see <see cref="AudioConfigBuilder"/>).
/// <para>
/// This is the CODE surface: the mix (masters, per-context scales, crossfade, duck sources, cue
/// variation) is a plain object graph with no serialization logic attached. The JSON door for the
/// <c>audio-config.json</c> wire format lives in the separate <c>Duack.Config</c> package, which is
/// provisional in this preview (code only) and may be published/documented later.
/// </para>
/// <para>
/// The file may also carry a build-only "tracks" section consumed by tools; that section is ignored
/// at runtime by the game but preserved on save.
/// </para>
/// </summary>
public class AudioConfig
{
    /// <summary>The schema version this build writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Schema stamp for migrations. A missing stamp reads as <see cref="CurrentSchemaVersion"/>;
    /// <see cref="Sanitize"/> normalizes a zero/legacy stamp up to 1 and preserves a higher
    /// (future) stamp rather than silently downgrading it.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

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

    /// <summary>
    /// Build-only per-track loudness/compress targets (the "tracks" section). Null when the file
    /// has none; omitted from output when null so a plain mix never emits a tracks section.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TrackConfig>? Tracks { get; set; }

    /// <summary>
    /// Build-only JSON sections the runtime model does not describe. Captured on load and
    /// re-emitted on save so a save never drops them.
    /// <para>
    /// The code-only surface never touches this: it exists so the JSON door can carry unknown
    /// sections through a load/save cycle without losing them.
    /// </para>
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>Clamps values to safe ranges and normalizes the schema stamp.</summary>
    public void Sanitize()
    {
        if (SchemaVersion < 1)
            SchemaVersion = 1;

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
