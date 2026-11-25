using System;
using System.Collections.Generic;
using System.Linq;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the flat bus table (issue #36 work item 2): one table in <see cref="AudioConfig"/> is
/// THE storage for the master/music/sfx volumes and the user buses. The wrapper setters on
/// <see cref="AudioConfigBuilder"/> write the same single table entry a direct table write
/// hits (one truth: no volume is stored twice anywhere in the graph), the pre-registered
/// entries use the lowercase Ordinal names, and <see cref="AudioConfigBuilder.Build"/> /
/// <see cref="AudioConfig.Sanitize"/> clamp every table volume to [0, 1]. The table reaches
/// the runtime through the existing <see cref="SfxConfig"/> transport (D7): both
/// <see cref="SfxConfig"/> and <see cref="MusicConfig"/> expose the very same dictionary
/// instance, never a copy.
/// </summary>
public class BusTableTests
{
    [Fact]
    public void Plain_config_pre_registers_master_music_sfx_at_full_volume()
    {
        var config = new AudioConfig();

        Assert.Equal(
            new[] { "master", "music", "sfx" },
            config.Buses.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(config.Buses.Values, bus => Assert.Equal(1f, bus.Volume));
        Assert.Equal(BusMixer.SfxBusName, BusNames.Sfx);
    }

    [Fact]
    public void Wrapper_setters_write_the_pre_registered_entries()
    {
        var built = new AudioConfigBuilder()
            .WithMasterVolume(0.9f)
            .WithMusicMasterVolume(0.5f)
            .WithSfxMasterVolume(0.25f)
            .Build();

        Assert.Equal(0.9f, built.Buses[BusNames.Master].Volume);
        Assert.Equal(0.5f, built.Buses[BusNames.Music].Volume);
        Assert.Equal(0.25f, built.Buses[BusNames.Sfx].Volume);
    }

    [Fact]
    public void WithBus_adds_user_entries_to_the_same_table()
    {
        var built = new AudioConfigBuilder()
            .WithBus("ambience", 0.4f)
            .WithBus("ui", 0.9f)
            .Build();

        Assert.Equal(0.4f, built.Buses["ambience"].Volume);
        Assert.Equal(0.9f, built.Buses["ui"].Volume);
        // The pre-registered three plus the two user buses: one table, no side dictionary.
        Assert.Equal(5, built.Buses.Count);
    }

    [Fact]
    public void Wrapper_and_direct_writes_hit_one_storage_not_two()
    {
        var built = new AudioConfigBuilder().WithMasterVolume(0.7f).Build();

        // The runtime transports are the same dictionary instance, never a copy.
        Assert.Same(built.Buses, built.Sfx.Buses);
        Assert.Same(built.Buses, built.Music.Buses);

        // A direct table write is what every reader sees; nothing holds a second copy.
        built.Buses[BusNames.Sfx].Volume = 0.2f;
        Assert.Equal(0.2f, built.Sfx.Buses![BusNames.Sfx].Volume);

        // Authoring the pre-registered name through WithBus replaces the same single entry.
        var reauthored = new AudioConfigBuilder()
            .WithMasterVolume(0.7f)
            .WithBus(BusNames.Master, 0.3f)
            .Build();
        Assert.Equal(0.3f, reauthored.Buses[BusNames.Master].Volume);
        Assert.Single(reauthored.Buses, bus => bus.Key == BusNames.Master);
    }

    [Fact]
    public void Replacing_sections_keeps_the_one_table()
    {
        // Hand-built configs compose through object initializers; the wiring must keep the
        // section configs on the same table so no path can fork the storage.
        var config = new AudioConfig
        {
            Music = new MusicConfig { CrossfadeSeconds = 2f },
            Sfx = new SfxConfig()
        };

        Assert.Same(config.Buses, config.Music.Buses);
        Assert.Same(config.Buses, config.Sfx.Buses);

        config.Buses[BusNames.Music].Volume = 0.5f;
        Assert.Equal(0.5f, config.Music.Buses![BusNames.Music].Volume);
    }

    [Fact]
    public void Build_sanitizes_all_table_volumes()
    {
        var built = new AudioConfigBuilder()
            .WithMasterVolume(2f)
            .WithMusicMasterVolume(-1f)
            .WithBus("ambience", 5f)
            .Build();

        Assert.Equal(1f, built.Buses[BusNames.Master].Volume);
        Assert.Equal(0f, built.Buses[BusNames.Music].Volume);
        Assert.Equal(1f, built.Buses["ambience"].Volume);
    }

    [Fact]
    public void Sanitize_clamps_a_directly_authored_table()
    {
        var config = new AudioConfig();
        config.Buses[BusNames.Sfx].Volume = 3f;
        config.Buses["ui"] = new BusConfig { Volume = -2f };

        config.Sanitize();

        Assert.Equal(1f, config.Buses[BusNames.Sfx].Volume);
        Assert.Equal(0f, config.Buses["ui"].Volume);
    }

    [Fact]
    public void Table_names_are_lowercase_Ordinal()
    {
        var built = new AudioConfigBuilder()
            .WithBus("ambience", 0.5f)
            .WithBus("Ambience", 0.6f)
            .Build();

        // Ordinal lookup: casing is significant, so both spellings are distinct entries.
        Assert.Equal(0.5f, built.Buses["ambience"].Volume);
        Assert.Equal(0.6f, built.Buses["Ambience"].Volume);
    }
}
