using System.Text.Json.Serialization;

namespace Duack;

/// <summary>
/// The player-tunable slice of the audio mix, stored per user outside the authored
/// <c>Content/Audio/audio-config.json</c>. Authored defaults own everything else (per-context
/// scales, duck sources, loudness/compress targets); this only carries what the settings overlay
/// lets a player move.
/// </summary>
public class PlayerAudioSettings
{
    /// <summary>The schema version this build writes.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Schema stamp for migrations. A missing stamp reads as <see cref="CurrentSchemaVersion"/>;
    /// <see cref="Sanitize"/> normalizes a zero/legacy stamp up to 1 and preserves a higher
    /// (future) stamp rather than silently downgrading it.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Music master volume, clamped to [0, 1].</summary>
    public float MusicMasterVolume { get; set; } = 1f;

    /// <summary>Sound-effect master volume, clamped to [0, 1].</summary>
    public float SfxMasterVolume { get; set; } = 1f;

    /// <summary>Master bus volume (scales music and SFX alike), clamped to [0, 1].</summary>
    public float MasterVolume { get; set; } = 1f;

    /// <summary>Fade time when switching between music tracks, clamped to [0.1, 10] seconds.</summary>
    /// <remarks>
    /// Desktop only: the KNI head plays music through a single native <c>MediaPlayer</c>, so it
    /// hard-stops and fades in instead of crossfading. The overlay hides the row there.
    /// </remarks>
    public float CrossfadeSeconds { get; set; } = 1.2f;

    /// <summary>Copies the tunable values out of an authored/effective config.</summary>
    public static PlayerAudioSettings FromConfig(AudioConfig config) => new()
    {
        MasterVolume = config.MasterVolume,
        MusicMasterVolume = config.Music.MasterVolume,
        SfxMasterVolume = config.Sfx.MasterVolume,
        CrossfadeSeconds = config.Music.CrossfadeSeconds
    };

    /// <summary>
    /// Clamps to the same bounds <see cref="AudioConfig.Sanitize"/> uses. NaN is mapped to the low
    /// bound because <see cref="Math.Clamp(float, float, float)"/> passes NaN straight through and
    /// a NaN volume would silence a channel with no way to recover it from the overlay.
    /// </summary>
    public void Sanitize()
    {
        if (SchemaVersion < 1)
            SchemaVersion = 1;

        MasterVolume = Clamp(MasterVolume, 0f, 1f);
        MusicMasterVolume = Clamp(MusicMasterVolume, 0f, 1f);
        SfxMasterVolume = Clamp(SfxMasterVolume, 0f, 1f);
        CrossfadeSeconds = Clamp(CrossfadeSeconds, 0.1f, 10f);
    }

    private static float Clamp(float value, float min, float max) =>
        float.IsFinite(value) ? Math.Clamp(value, min, max) : min;
}

/// <summary>Source-generated serialization metadata for <see cref="PlayerAudioSettings"/>.</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
[JsonSerializable(typeof(PlayerAudioSettings))]
internal partial class PlayerAudioSettingsJsonContext : JsonSerializerContext;
