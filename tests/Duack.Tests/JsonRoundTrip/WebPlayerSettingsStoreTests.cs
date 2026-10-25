using Duack;
using Xunit;

namespace Duack.Tests.JsonRoundTrip;

/// <summary>
/// Pins the browser half of Plan C. The store itself is pure logic over a string key/value
/// abstraction so it runs headlessly; <c>IJSRuntime</c> is a thin adapter over
/// <c>localStorage</c> in the KNI flavor. The load-bearing behavior is that every failure mode
/// (missing entry, corrupt JSON, a browser that refuses writes) reports false instead of
/// throwing, because the settings overlay turns that verdict into its "Saved!" flash and the old
/// <c>AudioConfig.Save</c> path reported success unconditionally while having written nothing.
/// </summary>
public class WebPlayerSettingsStoreTests
{
    private sealed class FakeStorage : IKeyValueStorage
    {
        public Dictionary<string, string> Items { get; } = new();
        public bool FailWrites { get; set; }
        public bool FailReads { get; set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public int Removes { get; private set; }

        public string? Get(string key)
        {
            Reads++;
            if (FailReads) throw new InvalidOperationException("storage blocked");
            return Items.TryGetValue(key, out var value) ? value : null;
        }

        public bool Set(string key, string value)
        {
            Writes++;
            if (FailWrites) throw new InvalidOperationException("quota exceeded");
            Items[key] = value;
            return true;
        }

        public void Remove(string key)
        {
            Removes++;
            Items.Remove(key);
        }
    }

    private static PlayerAudioSettings Sample() => new()
    {
        MusicMasterVolume = 0.35f,
        SfxMasterVolume = 0.65f,
        CrossfadeSeconds = 2.5f
    };

    [Fact]
    public void Key_is_namespaced_so_it_cannot_collide_with_other_saved_data()
    {
        Assert.StartsWith("duack:", WebPlayerSettingsStore.DefaultKey);
    }

    [Fact]
    public void TryLoad_is_false_when_the_key_is_absent()
    {
        var store = new WebPlayerSettingsStore(new FakeStorage());

        Assert.False(store.TryLoad(out var settings));
        Assert.Null(settings);
    }

    [Fact]
    public void Saved_settings_round_trip_through_storage()
    {
        var storage = new FakeStorage();
        var store = new WebPlayerSettingsStore(storage);

        Assert.True(store.Save(Sample()));

        var reopened = new WebPlayerSettingsStore(storage);
        Assert.True(reopened.TryLoad(out var loaded));
        Assert.Equal(0.35f, loaded!.MusicMasterVolume, 3);
        Assert.Equal(0.65f, loaded.SfxMasterVolume, 3);
        Assert.Equal(2.5f, loaded.CrossfadeSeconds, 3);
    }

    [Fact]
    public void Save_writes_only_the_tunable_fields()
    {
        var storage = new FakeStorage();
        new WebPlayerSettingsStore(storage).Save(Sample());

        var json = storage.Items[WebPlayerSettingsStore.DefaultKey];
        Assert.Contains("music_master_volume", json);
        Assert.Contains("sfx_master_volume", json);
        Assert.Contains("crossfade_seconds", json);
        // Authored sections the player cannot edit must not leak into the player's blob.
        Assert.DoesNotContain("duck_sources", json);
        Assert.DoesNotContain("contexts", json);
    }

    [Fact]
    public void Master_bus_volume_round_trips_through_storage()
    {
        var storage = new FakeStorage();
        var store = new WebPlayerSettingsStore(storage);

        Assert.True(store.Save(new PlayerAudioSettings { MasterVolume = 0.42f }));

        var reopened = new WebPlayerSettingsStore(storage);
        Assert.True(reopened.TryLoad(out var loaded));
        Assert.Equal(0.42f, loaded!.MasterVolume, 3);
    }

    [Fact]
    public void Master_bus_defaults_to_full_volume_when_absent_from_saved_json()
    {
        // Backward compatibility: a blob saved before the master bus existed has no
        // master_volume key; deserialization must leave the default (1f = full volume).
        var storage = new FakeStorage();
        storage.Items[WebPlayerSettingsStore.DefaultKey] =
            """{"schema_version":1,"music_master_volume":0.5,"sfx_master_volume":0.6,"crossfade_seconds":2.0}""";

        var reopened = new WebPlayerSettingsStore(storage);
        Assert.True(reopened.TryLoad(out var loaded));
        Assert.Equal(1f, loaded!.MasterVolume);
        Assert.Equal(0.5f, loaded.MusicMasterVolume, 3);
    }

    [Fact]
    public void Save_clamps_before_writing()
    {
        var storage = new FakeStorage();
        var store = new WebPlayerSettingsStore(storage);

        Assert.True(store.Save(new PlayerAudioSettings
        {
            MusicMasterVolume = 5f,
            SfxMasterVolume = -3f,
            CrossfadeSeconds = 999f
        }));

        Assert.True(store.TryLoad(out var loaded));
        Assert.Equal(1f, loaded!.MusicMasterVolume);
        Assert.Equal(0f, loaded.SfxMasterVolume);
        Assert.Equal(10f, loaded.CrossfadeSeconds);
    }

    [Fact]
    public void Save_reports_failure_when_storage_throws()
    {
        var storage = new FakeStorage { FailWrites = true };
        var store = new WebPlayerSettingsStore(storage);

        Assert.False(store.Save(Sample()));
    }

    [Fact]
    public void TryLoad_reports_failure_when_storage_throws()
    {
        // A blocked read (private mode, disabled cookies) must fall back to authored defaults
        // rather than taking the game down at boot.
        var store = new WebPlayerSettingsStore(new FakeStorage { FailReads = true });

        Assert.False(store.TryLoad(out var settings));
        Assert.Null(settings);
    }

    [Fact]
    public void TryLoad_reports_failure_for_a_corrupt_entry()
    {
        var storage = new FakeStorage();
        storage.Items[WebPlayerSettingsStore.DefaultKey] = "{not json";
        var store = new WebPlayerSettingsStore(storage);

        Assert.False(store.TryLoad(out var settings));
        Assert.Null(settings);
    }

    [Fact]
    public void TryLoad_reports_failure_for_an_empty_entry()
    {
        var storage = new FakeStorage();
        storage.Items[WebPlayerSettingsStore.DefaultKey] = "   ";
        var store = new WebPlayerSettingsStore(storage);

        Assert.False(store.TryLoad(out _));
    }

    [Fact]
    public void A_corrupt_entry_does_not_wedge_the_next_save()
    {
        var storage = new FakeStorage();
        storage.Items[WebPlayerSettingsStore.DefaultKey] = "{not json";
        var store = new WebPlayerSettingsStore(storage);

        store.TryLoad(out _);

        Assert.True(store.Save(Sample()));
        Assert.True(store.TryLoad(out var loaded));
        Assert.Equal(0.35f, loaded!.MusicMasterVolume, 3);
    }

    [Fact]
    public void Load_clamps_a_hand_edited_entry()
    {
        var storage = new FakeStorage();
        storage.Items[WebPlayerSettingsStore.DefaultKey] =
            """{"music_master_volume":9,"sfx_master_volume":-4,"crossfade_seconds":0}""";
        var store = new WebPlayerSettingsStore(storage);

        Assert.True(store.TryLoad(out var loaded));
        Assert.Equal(1f, loaded!.MusicMasterVolume);
        Assert.Equal(0f, loaded.SfxMasterVolume);
        Assert.Equal(0.1f, loaded.CrossfadeSeconds);
    }

    [Fact]
    public void Clear_removes_the_entry()
    {
        var storage = new FakeStorage();
        var store = new WebPlayerSettingsStore(storage);
        store.Save(Sample());

        store.Clear();

        Assert.False(store.TryLoad(out _));
        Assert.DoesNotContain(WebPlayerSettingsStore.DefaultKey, storage.Items.Keys);
    }

    [Fact]
    public void Clear_never_throws_even_when_storage_does()
    {
        var store = new WebPlayerSettingsStore(new FakeStorage());

        store.Clear();
    }

    [Fact]
    public void Effective_config_uses_saved_values_and_falls_back_cleanly()
    {
        var storage = new FakeStorage();
        var store = new WebPlayerSettingsStore(storage);
        var authored = new AudioConfig
        {
            Music = new MusicConfig { MasterVolume = 0.8f, CrossfadeSeconds = 1.2f },
            Sfx = new SfxConfig { MasterVolume = 0.9f }
        };

        var fresh = PlayerSettings.ApplyOverrides(authored, store);
        Assert.Equal(0.8f, fresh.Music.MasterVolume);

        store.Save(new PlayerAudioSettings { MusicMasterVolume = 0.2f, SfxMasterVolume = 0.4f });
        var saved = PlayerSettings.ApplyOverrides(authored, store);
        Assert.Equal(0.2f, saved.Music.MasterVolume, 3);
        Assert.Equal(0.4f, saved.Sfx.MasterVolume, 3);
    }
}
