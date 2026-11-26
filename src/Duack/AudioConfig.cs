using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Duack;

/// <summary>
/// The well-known bus names pre-registered in every <see cref="AudioConfig.Buses"/> table:
/// lowercase, compared <see cref="StringComparer.Ordinal"/> (see
/// <see cref="BusMixer.SfxBusName"/> for the runtime's fixed SFX bus, which matches
/// <see cref="Sfx"/>).
/// </summary>
public static class BusNames
{
    /// <summary>The global output bus (scales music and SFX alike).</summary>
    public const string Master = "master";

    /// <summary>The music bus.</summary>
    public const string Music = "music";

    /// <summary>The fixed sound-effect bus.</summary>
    public const string Sfx = "sfx";
}

/// <summary>One bus in the flat bus table (see <see cref="AudioConfig.Buses"/>).</summary>
public class BusConfig
{
    /// <summary>Bus volume in [0, 1]; clamped there by <see cref="AudioConfig.Sanitize"/>.</summary>
    public float Volume { get; set; } = 1f;
}

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

    /// <summary>
    /// What this source ducks: <c>music</c> (the default, as always) or the name of a user
    /// bus (e.g. <c>ambience</c>), whose cues scale by this ratio while the hold is active.
    /// </summary>
    public string Target { get; set; } = "music";
}

/// <summary>Music mix: crossfade, per-context scales, and duck sources.</summary>
public class MusicConfig
{
    /// <summary>
    /// Reference to the flat bus table this config belongs to (see
    /// <see cref="AudioConfig.Buses"/>); the music bus entry is the music master volume.
    /// Wired by <see cref="AudioConfig"/>, so within a config graph it is the very same
    /// dictionary instance, never a copy. Null when a <see cref="MusicConfig"/> is constructed
    /// standalone; the music bus volume then resolves to full volume.
    /// </summary>
    public Dictionary<string, BusConfig>? Buses { get; set; }

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
    /// Reference to the flat bus table this config belongs to (see
    /// <see cref="AudioConfig.Buses"/>), and the transport the runtime reads: the pre-registered
    /// <c>sfx</c> entry is the SFX master volume and the remaining entries are the user buses
    /// <see cref="SfxManager"/> seeds its <see cref="BusMixer"/> from. Wired by
    /// <see cref="AudioConfig"/>, so within a config graph it is the very same dictionary
    /// instance, never a copy. Null when an <see cref="SfxConfig"/> is constructed standalone.
    /// </summary>
    public Dictionary<string, BusConfig>? Buses { get; set; }

    /// <summary>
    /// Optional per-play pitch/volume jitter for sound-effect cues (footsteps, hits). Null (omitted
    /// from JSON) means no jitter.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CueVariation? CueVariation { get; set; }

    /// <summary>Optional limits on simultaneous SFX voices. Null means unlimited.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VoiceLimitConfig? VoiceLimits { get; set; }

    /// <summary>
    /// Optional 2D spatial settings (pan saturation, distance fall-off), authored via
    /// <see cref="AudioConfigBuilder.WithSpatial"/>. Null means positional plays use a fresh
    /// default-constructed <see cref="SpatialConfig"/> rather than no spatial behavior.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SpatialConfig? Spatial { get; set; }
}

/// <summary>
/// The audio mix configuration, authored in code (see <see cref="AudioConfigBuilder"/>).
/// The mix (the flat bus table, per-context scales, crossfade, duck sources, and cue variation)
/// is a plain object graph.
/// </summary>
public class AudioConfig
{
    private Dictionary<string, BusConfig> _buses = CreateDefaultBuses();
    private Dictionary<string, MixStateConfig> _mixStates = new(StringComparer.Ordinal);
    private MusicConfig _music = new();
    private SfxConfig _sfx = new();

    /// <summary>Wires the section configs onto the one bus table (see <see cref="Buses"/>).</summary>
    public AudioConfig()
    {
        _music.Buses = _buses;
        _sfx.Buses = _buses;
    }

    /// <summary>
    /// THE flat bus table: one <see cref="BusConfig"/> entry per bus — the pre-registered
    /// <see cref="BusNames.Master"/>/<see cref="BusNames.Music"/>/<see cref="BusNames.Sfx"/>
    /// entries plus the user buses authored via
    /// <see cref="AudioConfigBuilder.WithBus(string, float)"/>. Names are lowercase and compared
    /// <see cref="StringComparer.Ordinal"/>; volumes are clamped to [0, 1] by
    /// <see cref="Sanitize"/>. Replacing the dictionary rewires <see cref="Music"/> and
    /// <see cref="Sfx"/> onto it so the graph keeps exactly one table.
    /// </summary>
    public Dictionary<string, BusConfig> Buses
    {
        get => _buses;
        set
        {
            _buses = value;
            _music.Buses = value;
            _sfx.Buses = value;
        }
    }

    /// <summary>
    /// Music configuration.
    /// </summary>
    public MusicConfig Music
    {
        get => _music;
        set { _music = value; value.Buses = _buses; }
    }

    /// <summary>
    /// Sound effects configuration.
    /// </summary>
    public SfxConfig Sfx
    {
        get => _sfx;
        set { _sfx = value; value.Buses = _buses; }
    }

    /// <summary>
    /// The named mix states (see <see cref="MixStateConfig"/>), keyed by state name
    /// (lowercase, <see cref="StringComparer.Ordinal"/>) and authored via
    /// <see cref="AudioConfigBuilder.WithMixState(string, Action{MixStateBuilder})"/>. Each
    /// state is a vector of per-bus volume targets over the same flat table
    /// <see cref="Buses"/> names — authored config, never captured runtime state (ADR-10) —
    /// applied at runtime by a <see cref="MixStateManager"/>.
    /// </summary>
    public Dictionary<string, MixStateConfig> MixStates
    {
        get => _mixStates;
        set => _mixStates = value ?? new Dictionary<string, MixStateConfig>(StringComparer.Ordinal);
    }

    /// <summary>Creates the table pre-registered with master/music/sfx at full volume.</summary>
    public static Dictionary<string, BusConfig> CreateDefaultBuses() => new(StringComparer.Ordinal)
    {
        [BusNames.Master] = new BusConfig(),
        [BusNames.Music] = new BusConfig(),
        [BusNames.Sfx] = new BusConfig(),
    };

    /// <summary>Clamps values to safe ranges.</summary>
    public void Sanitize()
    {
        foreach (var bus in _buses.Values)
            bus.Volume = System.Math.Clamp(bus.Volume, 0f, 1f);

        foreach (var state in _mixStates.Values)
            foreach (var bus in state.Buses.Keys.ToList())
                state.Buses[bus] = System.Math.Clamp(float.IsNaN(state.Buses[bus]) ? 0f : state.Buses[bus], 0f, 1f);

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
        Sfx.VoiceLimits?.Sanitize();
        Sfx.Spatial?.Sanitize();
    }
}
