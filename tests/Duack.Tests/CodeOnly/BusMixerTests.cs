using System;
using System.Collections.Generic;
using System.Linq;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins slice C user buses: <see cref="SfxConfig.Buses"/> composition,
/// <see cref="BusMixer"/> routing (un-routed cues land on the SFX bus, unknown names fall
/// back and report), per-bus mute/pause and hot volume updates, global master mute, and the
/// readback view.
/// </summary>
public class BusMixerTests
{

    private static SfxConfig ConfigBus(string name, float volume)
    {
        var sfx = new SfxConfig();
        sfx.Buses[name] = volume;
        return sfx;
    }

    [Fact]
    public void Builder_WithBus_SetsConfiguredBusVolume()
    {
        var config = new AudioConfigBuilder().WithBus("ambience", 0.4f).WithBus("ui", 0.9f).Build();
        Assert.Equal(0.4f, config.Sfx.Buses["ambience"]);
        Assert.Equal(0.9f, config.Sfx.Buses["ui"]);
    }

    [Fact]
    public void Sanitize_ClampsBusVolumes()
    {
        var sfx = ConfigBus("ambience", 2f);
        sfx.Buses["ui"] = -0.5f;
        new AudioConfig { Sfx = sfx }.Sanitize();
        Assert.Equal(1f, sfx.Buses["ambience"]);
        Assert.Equal(0f, sfx.Buses["ui"]);
    }

    [Fact]
    public void Resolve_NullRequest_RoutesToSfxBusWithoutFallback()
    {
        var mixer = new BusMixer(ConfigBus("ambience", 0.4f).Buses);
        var r = mixer.Resolve(null);
        Assert.Equal("sfx", r.Bus);
        Assert.False(r.FellBack);
    }

    [Fact]
    public void Resolve_KnownBus_UsesItsVolume()
    {
        var mixer = new BusMixer(ConfigBus("ambience", 0.4f).Buses);
        Assert.Equal(0.4f, mixer.Resolve("ambience").Gain, 4);
    }

    [Fact]
    public void Resolve_UnknownBus_FallsBackToSfxAndReports()
    {
        var mixer = new BusMixer(ConfigBus("ambience", 0.4f).Buses);
        string? reported = null;
        mixer.BusFellBack += bus => reported = bus;

        var r = mixer.Resolve("ambientt");

        Assert.Equal("sfx", r.Bus);
        Assert.True(r.FellBack);
        Assert.Equal("ambientt", reported);
    }

    [Fact]
    public void Mute_And_Pause_ZeroTheGain()
    {
        var mixer = new BusMixer(ConfigBus("ambience", 0.4f).Buses);

        mixer.SetMute("ambience", true);
        Assert.Equal(0f, mixer.Resolve("ambience").Gain);

        mixer.SetMute("ambience", false);
        mixer.SetPaused("ambience", true);
        Assert.Equal(0f, mixer.Resolve("ambience").Gain);

        mixer.SetPaused("ambience", false);
        Assert.Equal(0.4f, mixer.Resolve("ambience").Gain, 4);
    }

    [Fact]
    public void MasterMute_ZeroesEveryBus()
    {
        var mixer = new BusMixer(ConfigBus("ambience", 0.4f).Buses);
        mixer.MasterMuted = true;
        Assert.Equal(0f, mixer.Resolve("ambience").Gain);
        Assert.Equal(0f, mixer.Resolve("sfx").Gain);
        Assert.Equal(0f, mixer.Resolve(null).Gain);
        Assert.Equal(0f, mixer.Resolve("ambientt").Gain);
    }

    [Fact]
    public void HotVolumeUpdate_AppliesImmediately_AndRaisesChanged()
    {
        var mixer = new BusMixer(ConfigBus("ambience", 0.4f).Buses);
        var changed = 0;
        mixer.Changed += () => changed++;

        mixer.SetBus("ambience", 0.7f);

        Assert.Equal(0.7f, mixer.Resolve("ambience").Gain, 4);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void SetBus_SameValueOnKnownBus_SecondCallDoesNotRaiseChanged()
    {
        var mixer = new BusMixer(ConfigBus("ambience", 0.4f).Buses);
        var changed = 0;
        mixer.Changed += () => changed++;

        // First: hot update (state change). Second: same value — a per-frame hot-update loop
        // must not re-notify every frame.
        mixer.SetBus("ambience", 0.7f);
        mixer.SetBus("ambience", 0.7f);

        Assert.Equal(1, changed);
    }

    [Fact]
    public void SetBus_UnknownName_IntentionallyCreatesTheBus_AndRaisesChanged()
    {
        // Decision W2 (a) in review-structure.md: SetBus is the authoring surface and creates
        // the bus (authored-config hot updates follow the DuckMixer.ReloadSources pattern);
        // Resolve is the routing surface and falls back loudly. The asymmetry is deliberate.
        var mixer = new BusMixer(null);
        var changed = 0;
        mixer.Changed += () => changed++;

        mixer.SetBus("ambience", 0.5f);

        Assert.Equal(0.5f, mixer.Resolve("ambience").Gain, 4);
        Assert.False(mixer.Resolve("ambience").FellBack);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Views_ExposeVolumeMutePauseAndEffectiveGain()
    {
        var buses = new AudioConfigBuilder().WithBus("ambience", 0.4f).WithBus("ui", 0.9f).Build().Sfx.Buses;
        var mixer = new BusMixer(buses);
        mixer.SetMute("ui", true);

        var view = mixer.Views.Single(v => v.Name == "ambience");
        Assert.Equal(0.4f, view.Volume, 4);
        Assert.False(view.Muted);
        Assert.False(view.Paused);
        Assert.Equal(0.4f, view.EffectiveGain, 4);

        var ui = mixer.Views.Single(v => v.Name == "ui");
        Assert.True(ui.Muted);
        Assert.Equal(0f, ui.EffectiveGain);
    }
}
