namespace Duack;

/// <summary>
/// Layers the player's saved preferences over the authored mix. The authored config stays the
/// source of truth for everything a developer authors; the store only carries the handful of values
/// the settings overlay exposes, so updating the authored mix never discards a player's volume choice
/// and never writes back into shipped code or assets.
/// </summary>
public static class PlayerSettings
{
    /// <summary>
    /// Store the running game uses. Set once during startup (file store on desktop, localStorage on
    /// web) before the first <see cref="ApplyOverrides(AudioConfig)"/>. Defaults to a file store in
    /// per-user application data.
    /// </summary>
    public static IPlayerSettingsStore Store { get; set; } = new FilePlayerSettingsStore();

    /// <summary>Returns the effective config: authored defaults with the player's values layered on.</summary>
    public static AudioConfig ApplyOverrides(AudioConfig authored) => ApplyOverrides(authored, Store);

    /// <summary>
    /// Returns the effective config without touching <paramref name="authored"/>. Sections the
    /// player cannot edit (per-context scales, duck sources, loudness/compress targets) are carried
    /// over unchanged, and a store that fails to load simply yields the authored values.
    /// </summary>
    public static AudioConfig ApplyOverrides(AudioConfig authored, IPlayerSettingsStore store)
    {
        if (!store.TryLoad(out var saved) || saved == null) return authored;

        saved.Sanitize();

        var effective = new AudioConfig
        {
            MasterVolume = saved.MasterVolume,
            Music = new MusicConfig
            {
                MasterVolume = saved.MusicMasterVolume,
                CrossfadeSeconds = saved.CrossfadeSeconds,
                Contexts = new Dictionary<string, float>(authored.Music.Contexts),
                DuckSources = CloneDuckSources(authored.Music.DuckSources),
                CueVariation = authored.Music.CueVariation
            },
            Sfx = new SfxConfig
            {
                MasterVolume = saved.SfxMasterVolume,
                CueVariation = authored.Sfx.CueVariation
            },
        };

        effective.Sanitize();
        return effective;
    }

    /// <summary>
    /// Persists the player-tunable values of <paramref name="config"/>. The result is the store's
    /// verdict, which the overlay turns into its "Saved!" flash, so a failed write must not be
    /// reported as a success.
    /// </summary>
    public static bool SaveOverrides(AudioConfig config) => SaveOverrides(config, Store);

    /// <inheritdoc cref="SaveOverrides(AudioConfig)"/>
    public static bool SaveOverrides(AudioConfig config, IPlayerSettingsStore store)
    {
        var settings = PlayerAudioSettings.FromConfig(config);
        var saved = store.Save(settings);

        if (!saved)
        {
            // Console.WriteLine, not Debug.WriteLine: Debug is [Conditional("DEBUG")], so Release
            // drops the call and the code building the message with it - and Release is the build
            // the browser runs. This line was Debug-based and existed only in bin/Debug, which is how
            // the shipped web head reported "Save failed" with no explanation anywhere. The store
            // logs the underlying cause; this names which store was at fault. Pinned by
            // Tests/Build/SettingsDiagnosticsVisibilityTests.cs.
            Console.WriteLine($"[PlayerSettings] could not persist settings via {store.GetType().Name}");
        }

        return saved;
    }

    /// <summary>Drops the saved preferences so the next <see cref="ApplyOverrides(AudioConfig)"/> uses authored values.</summary>
    public static void ClearOverrides() => Store.Clear();

    private static Dictionary<string, DuckSourceConfig> CloneDuckSources(Dictionary<string, DuckSourceConfig> source)
    {
        var clone = new Dictionary<string, DuckSourceConfig>(source.Count);
        foreach (var (key, value) in source)
        {
            clone[key] = new DuckSourceConfig
            {
                Ratio = value.Ratio,
                AttackSeconds = value.AttackSeconds,
                HoldSeconds = value.HoldSeconds,
                ReleaseSeconds = value.ReleaseSeconds
            };
        }

        return clone;
    }
}

/// <summary>
/// Chooses the platform store. Kept as a pure function so the routing is testable: a normal desktop
/// test run reports <c>OperatingSystem.IsBrowser()</c> as false and could never exercise the browser
/// branch.
/// </summary>
public static class PlayerSettingsStoreSelector
{
    public static IPlayerSettingsStore Select(
        bool isBrowser,
        IPlayerSettingsStore fileStore,
        IPlayerSettingsStore browserStore) =>
        isBrowser ? browserStore : fileStore;

    /// <summary>Selects for the current platform.</summary>
    public static IPlayerSettingsStore Select(IPlayerSettingsStore fileStore, IPlayerSettingsStore browserStore) =>
        Select(OperatingSystem.IsBrowser(), fileStore, browserStore);
}
