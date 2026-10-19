using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins Plan C: player-tunable audio values live in a separate per-user store, layered over the
/// authored <c>Content/Audio/audio-config.json</c>, and Save must report real failure instead of
/// always claiming success. The authored file is never written by gameplay.
/// </summary>
public class PlayerSettingsTests
{
    private static AudioConfig Defaults() => new()
    {
        MasterVolume = 1f,
        Music = new MusicConfig
        {
            MasterVolume = 0.8f,
            CrossfadeSeconds = 1.2f,
            Contexts = new Dictionary<string, float> { ["Overworld"] = 0.5f },
            DuckSources = new Dictionary<string, DuckSourceConfig>
            {
                ["sfx"] = new() { Ratio = 0.3f }
            }
        },
        Sfx = new SfxConfig { MasterVolume = 0.9f }
    };

    [Fact]
    public void ApplyOverrides_keeps_authored_values_when_nothing_is_saved()
    {
        var defaults = Defaults();
        var effective = PlayerSettings.ApplyOverrides(defaults, new InMemoryPlayerSettingsStore());

        Assert.Equal(0.8f, effective.Music.MasterVolume);
        Assert.Equal(0.9f, effective.Sfx.MasterVolume);
        Assert.Equal(1.2f, effective.Music.CrossfadeSeconds);
    }

    [Fact]
    public void ApplyOverrides_layers_saved_values_over_authored_defaults()
    {
        var store = new InMemoryPlayerSettingsStore();
        Assert.True(store.Save(new PlayerAudioSettings
        {
            MusicMasterVolume = 0.25f,
            SfxMasterVolume = 0.5f,
            CrossfadeSeconds = 3f
        }));

        var effective = PlayerSettings.ApplyOverrides(Defaults(), store);

        Assert.Equal(0.25f, effective.Music.MasterVolume);
        Assert.Equal(0.5f, effective.Sfx.MasterVolume);
        Assert.Equal(3f, effective.Music.CrossfadeSeconds);
    }

    [Fact]
    public void ApplyOverrides_layers_saved_master_over_authored_default()
    {
        var store = new InMemoryPlayerSettingsStore();
        Assert.True(store.Save(new PlayerAudioSettings { MasterVolume = 0.4f }));

        var effective = PlayerSettings.ApplyOverrides(Defaults(), store);

        Assert.Equal(0.4f, effective.MasterVolume);
    }

    [Fact]
    public void ApplyOverrides_defaults_master_to_full_volume_when_absent_in_saved_settings()
    {
        // Backward compatibility: settings saved before the master bus existed have no
        // master_volume field; deserialization leaves the default (1f = full volume).
        var store = new InMemoryPlayerSettingsStore();
        Assert.True(store.Save(new PlayerAudioSettings { MusicMasterVolume = 0.5f }));

        var effective = PlayerSettings.ApplyOverrides(Defaults(), store);

        Assert.Equal(1f, effective.MasterVolume);
    }

    [Fact]
    public void ApplyOverrides_clamps_corrupt_saved_master()
    {
        var store = new InMemoryPlayerSettingsStore();
        store.Save(new PlayerAudioSettings { MasterVolume = 4f });

        var effective = PlayerSettings.ApplyOverrides(Defaults(), store);

        Assert.Equal(1f, effective.MasterVolume);
    }

    [Fact]
    public void ApplyOverrides_leaves_authored_defaults_unmutated()
    {
        var defaults = Defaults();
        var store = new InMemoryPlayerSettingsStore();
        store.Save(new PlayerAudioSettings { MusicMasterVolume = 0.1f });

        PlayerSettings.ApplyOverrides(defaults, store);

        Assert.Equal(0.8f, defaults.Music.MasterVolume);
    }

    [Fact]
    public void ApplyOverrides_preserves_authored_sections_the_player_cannot_edit()
    {
        var store = new InMemoryPlayerSettingsStore();
        store.Save(new PlayerAudioSettings { MusicMasterVolume = 0.25f, SfxMasterVolume = 0.5f });

        var effective = PlayerSettings.ApplyOverrides(Defaults(), store);

        Assert.Equal(0.5f, effective.Music.Contexts["Overworld"]);
        Assert.Equal(0.3f, effective.Music.DuckSources["sfx"].Ratio);
    }

    [Fact]
    public void ApplyOverrides_clamps_corrupt_saved_values()
    {
        var store = new InMemoryPlayerSettingsStore();
        store.Save(new PlayerAudioSettings
        {
            MusicMasterVolume = 4f,
            SfxMasterVolume = -1f,
            CrossfadeSeconds = 900f
        });

        var effective = PlayerSettings.ApplyOverrides(Defaults(), store);

        Assert.Equal(1f, effective.Music.MasterVolume);
        Assert.Equal(0f, effective.Sfx.MasterVolume);
        Assert.Equal(10f, effective.Music.CrossfadeSeconds);
    }

    [Fact]
    public void SaveOverrides_returns_false_when_the_store_reports_failure()
    {
        // The "Saved!" lie: on a browser with storage blocked, the old code flashed success while
        // AudioConfig.Save had already returned. The result must now be the store's verdict.
        var config = Defaults();
        var failing = new InMemoryPlayerSettingsStore { FailWrites = true };

        Assert.False(PlayerSettings.SaveOverrides(config, failing));
    }

    [Fact]
    public void SaveOverrides_returns_true_when_the_store_accepts_the_write()
    {
        var store = new InMemoryPlayerSettingsStore();

        Assert.True(PlayerSettings.SaveOverrides(Defaults(), store));
    }

    [Fact]
    public void SaveOverrides_clamps_before_persisting()
    {
        var config = Defaults();
        config.Music.MasterVolume = 7f;
        var store = new InMemoryPlayerSettingsStore();

        Assert.True(PlayerSettings.SaveOverrides(config, store));
        Assert.True(store.TryLoad(out var saved));
        Assert.Equal(1f, saved!.MusicMasterVolume);
    }

    [Fact]
    public void SaveOverrides_stores_the_master_bus_volume()
    {
        var config = Defaults();
        config.MasterVolume = 0.3f;
        var store = new InMemoryPlayerSettingsStore();

        PlayerSettings.SaveOverrides(config, store);

        Assert.True(store.TryLoad(out var saved));
        Assert.Equal(0.3f, saved!.MasterVolume);
    }

    [Fact]
    public void SaveOverrides_stores_only_player_tunable_fields()
    {
        var config = Defaults();
        config.Music.MasterVolume = 0.4f;
        var store = new InMemoryPlayerSettingsStore();

        PlayerSettings.SaveOverrides(config, store);

        Assert.True(store.TryLoad(out var saved));
        Assert.Equal(0.4f, saved!.MusicMasterVolume);
        Assert.Equal(0.9f, saved.SfxMasterVolume);
        Assert.Equal(1.2f, saved.CrossfadeSeconds);
    }

    [Fact]
    public void ClearOverrides_restores_authored_defaults()
    {
        var store = new InMemoryPlayerSettingsStore();
        store.Save(new PlayerAudioSettings { MusicMasterVolume = 0.1f });

        store.Clear();

        var effective = PlayerSettings.ApplyOverrides(Defaults(), store);
        Assert.Equal(0.8f, effective.Music.MasterVolume);
    }

    [Fact]
    public void Selector_returns_the_browser_store_only_in_a_browser()
    {
        var file = new InMemoryPlayerSettingsStore { Name = "file" };
        var browser = new InMemoryPlayerSettingsStore { Name = "browser" };

        Assert.Same(browser, PlayerSettingsStoreSelector.Select(true, file, browser));
        Assert.Same(file, PlayerSettingsStoreSelector.Select(false, file, browser));
    }
}
