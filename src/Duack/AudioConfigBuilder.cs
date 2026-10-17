namespace Duack;

/// <summary>
/// Fluent helper for assembling an <see cref="AudioConfig"/> in code (tests, samples, first-run
/// defaults) instead of hand-writing JSON. <see cref="Build"/> runs <see cref="AudioConfig.Sanitize"/>
/// so the result carries the same clamped values the file path would.
/// </summary>
public sealed class AudioConfigBuilder
{
    private readonly AudioConfig _config = new();

    /// <summary>Sets the global output master volume.</summary>
    public AudioConfigBuilder WithMasterVolume(float value)
    {
        _config.MasterVolume = value;
        return this;
    }

    /// <summary>Sets the music master volume.</summary>
    public AudioConfigBuilder WithMusicMasterVolume(float value)
    {
        _config.Music.MasterVolume = value;
        return this;
    }

    /// <summary>Sets the sound-effect master volume.</summary>
    public AudioConfigBuilder WithSfxMasterVolume(float value)
    {
        _config.Sfx.MasterVolume = value;
        return this;
    }

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

    /// <summary>
    /// Attaches code-authored <paramref name="playlists"/> to the config by embedding them under the
    /// <c>playlists</c> key, so <see cref="Build"/> then <see cref="AudioConfig.Serialize"/> emits a
    /// config the tooling can read back with <see cref="PlaylistJson.FromConfig"/>.
    /// </summary>
    public AudioConfigBuilder WithPlaylists(PlaylistConfig playlists)
    {
        PlaylistJson.Embed(_config, playlists);
        return this;
    }

    /// <summary>Returns the assembled config, clamped by <see cref="AudioConfig.Sanitize"/>.</summary>
    public AudioConfig Build()
    {
        _config.Sanitize();
        return _config;
    }
}
