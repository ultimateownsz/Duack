namespace Duack;

/// <summary>Non-durable store used by tests and as a last-resort fallback.</summary>
public class InMemoryPlayerSettingsStore : IPlayerSettingsStore
{
    private PlayerAudioSettings? _settings;

    public InMemoryPlayerSettingsStore() { }

    public InMemoryPlayerSettingsStore(PlayerAudioSettings? settings) => _settings = settings;

    /// <summary>Test hook: label so a selector test can tell the two stores apart.</summary>
    public string? Name { get; init; }

    /// <summary>Test hook: makes every write report failure, standing in for blocked browser storage.</summary>
    public bool FailWrites { get; set; }

    public bool TryLoad(out PlayerAudioSettings? settings)
    {
        settings = _settings;
        return settings != null;
    }

    public bool Save(PlayerAudioSettings settings)
    {
        if (FailWrites) return false;

        var copy = new PlayerAudioSettings
        {
            MusicMasterVolume = settings.MusicMasterVolume,
            SfxMasterVolume = settings.SfxMasterVolume,
            CrossfadeSeconds = settings.CrossfadeSeconds
        };
        copy.Sanitize();
        _settings = copy;
        return true;
    }

    public void Clear() => _settings = null;
}
