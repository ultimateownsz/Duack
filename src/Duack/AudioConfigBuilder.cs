namespace Duack;

/// <summary>
/// Fluent helper for assembling an <see cref="AudioConfig"/> in code (tests, samples, first-run
/// defaults) instead of hand-writing JSON. <see cref="Build"/> runs <see cref="AudioConfig.Sanitize"/>
/// so the result carries the same clamped values the file path would.
/// </summary>
public sealed class AudioConfigBuilder
{
    private readonly AudioConfig _config = new();

    /// <summary>
    /// Sets the global output master volume: a thin write into the pre-registered
    /// <see cref="BusNames.Master"/> entry of the one bus table.
    /// </summary>
    public AudioConfigBuilder WithMasterVolume(float value)
    {
        Bus(BusNames.Master).Volume = value;
        return this;
    }

    /// <summary>
    /// Sets the music master volume: a thin write into the pre-registered
    /// <see cref="BusNames.Music"/> entry of the one bus table.
    /// </summary>
    public AudioConfigBuilder WithMusicMasterVolume(float value)
    {
        Bus(BusNames.Music).Volume = value;
        return this;
    }

    /// <summary>
    /// Sets the sound-effect master volume: a thin write into the pre-registered
    /// <see cref="BusNames.Sfx"/> entry of the one bus table.
    /// </summary>
    public AudioConfigBuilder WithSfxMasterVolume(float value)
    {
        Bus(BusNames.Sfx).Volume = value;
        return this;
    }

    /// <summary>
    /// Defines (or replaces) a named user bus with its volume in [0, 1]. Cues routed to the bus
    /// via <see cref="SfxManager.Play(string, float, float, bool, string?)"/> scale by its
    /// effective gain; un-routed cues stay on the fixed SFX bus. Writing a pre-registered name
    /// (<see cref="BusNames.Master"/>/<see cref="BusNames.Music"/>/<see cref="BusNames.Sfx"/>)
    /// replaces that single table entry — there is no second storage.
    /// </summary>
    public AudioConfigBuilder WithBus(string name, float volume)
    {
        _config.Buses[name] = new BusConfig { Volume = volume };
        return this;
    }

    /// <summary>The named table entry, adding it at full volume if it is not present yet.</summary>
    private BusConfig Bus(string name) =>
        _config.Buses.TryGetValue(name, out var bus) ? bus : _config.Buses[name] = new BusConfig();

    /// <summary>Sets the music crossfade time in seconds.</summary>
    public AudioConfigBuilder WithCrossfadeSeconds(float value)
    {
        _config.Music.CrossfadeSeconds = value;
        return this;
    }

    /// <summary>Sets the volume scale for a named music context.</summary>
    public AudioConfigBuilder WithContext(string name, float volume)
    {
        _config.Music.Contexts[name] = volume;
        return this;
    }

    /// <summary>Sets the ducking parameters for a named source (e.g. "sfx", "dialogue").</summary>
    public AudioConfigBuilder WithDuckSource(string name, DuckSourceConfig source)
    {
        _config.Music.DuckSources[name] = source;
        return this;
    }

    /// <summary>Sets the per-play pitch/volume jitter for music cues.</summary>
    public AudioConfigBuilder WithMusicCueVariation(CueVariation variation)
    {
        _config.Music.CueVariation = variation;
        return this;
    }

    /// <summary>Sets the per-play pitch/volume jitter for sound-effect cues.</summary>
    public AudioConfigBuilder WithSfxCueVariation(CueVariation variation)
    {
        _config.Sfx.CueVariation = variation;
        return this;
    }

    /// <summary>Returns the assembled config, clamped by <see cref="AudioConfig.Sanitize"/>.</summary>
    public AudioConfig Build()
    {
        _config.Sanitize();
        return _config;
    }
}
