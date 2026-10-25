using Duack;

namespace Duack.Tests.CodeOnly;

public class DuckMixerTests
{
    private static DuckSourceConfig Source(float ratio = 0.3f, float attack = 0.06f, float hold = 0f, float release = 0.35f) =>
        new() { Ratio = ratio, AttackSeconds = attack, HoldSeconds = hold, ReleaseSeconds = release };

    [Fact]
    public void EffectiveRatio_starts_at_one()
    {
        var mixer = new DuckMixer();
        Assert.Equal(1f, mixer.EffectiveRatio, 3);
    }

    [Fact]
    public void Setting_an_unregistered_source_is_a_no_op()
    {
        var mixer = new DuckMixer();
        mixer.Set("missing", true);
        mixer.Update(1f);
        Assert.Equal(1f, mixer.EffectiveRatio, 3);
    }

    [Fact]
    public void Active_source_eases_toward_its_ratio()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0.06f));
        mixer.Set("sfx", true);

        mixer.Update(0.06f); // one time constant of decay
        Assert.InRange(mixer.EffectiveRatio, 0.53f, 0.60f); // = 1 - (1-0.3)*(1 - 1/e) ≈ 0.558
    }

    [Fact]
    public void Inactive_source_eases_back_to_one()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0.06f, release: 0.35f));
        mixer.Set("sfx", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f); // fully ducked
        Assert.Equal(0.3f, mixer.EffectiveRatio, 2);

        mixer.Set("sfx", false);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f); // fully recovered
        Assert.Equal(1f, mixer.EffectiveRatio, 2);
    }

    [Fact]
    public void Active_sources_combine_multiplicatively()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.5f, attack: 0.05f));
        mixer.SetSource("ui", Source(ratio: 0.5f, attack: 0.05f));
        mixer.Set("sfx", true);
        mixer.Set("ui", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f);

        Assert.Equal(0.25f, mixer.EffectiveRatio, 2); // 0.5 * 0.5
    }

    [Fact]
    public void Pulse_holds_then_releases()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0.05f, hold: 0.5f, release: 0.05f));
        mixer.Pulse("sfx");

        // Decays into the dip; hold (0.5s) is still active.
        mixer.Update(0.4f);
        Assert.Equal(0.3f, mixer.EffectiveRatio, 2);

        // Hold expired mid-frame: release begins.
        mixer.Update(0.4f);
        Assert.True(mixer.EffectiveRatio > 0.3f);

        // Fully recovered.
        mixer.Update(0.5f);
        Assert.Equal(1f, mixer.EffectiveRatio, 2);
    }

    [Fact]
    public void Pulse_with_explicit_hold_overrides_config()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0.05f, hold: 10f, release: 0.05f));
        mixer.Pulse("sfx", holdSeconds: 0.2f);
        mixer.Update(1f);
        Assert.Equal(1f, mixer.EffectiveRatio, 2); // should have released well before 10s
    }

    [Fact]
    public void Zero_time_constants_snap_instantly()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0f, attack: 0f, release: 0f));
        mixer.Set("sfx", true);
        mixer.Update(0.016f);
        Assert.Equal(0f, mixer.EffectiveRatio, 3);

        mixer.Set("sfx", false);
        mixer.Update(0.016f);
        Assert.Equal(1f, mixer.EffectiveRatio, 3);
    }

    [Fact]
    public void Zero_delta_does_not_change_the_ratio()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0.06f));
        mixer.Set("sfx", true);
        mixer.Update(0f);
        Assert.Equal(1f, mixer.EffectiveRatio, 3);
    }

    [Fact]
    public void Replacing_a_source_keeps_other_sources_intact()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.5f, attack: 0.05f));
        mixer.SetSource("ui", Source(ratio: 0.5f, attack: 0.05f));
        mixer.Set("sfx", true);
        mixer.Set("ui", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f);
        Assert.Equal(0.25f, mixer.EffectiveRatio, 2);

        mixer.SetSource("sfx", Source(ratio: 1f, attack: 0.05f)); // "sfx" no longer ducks
        mixer.Set("sfx", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f);
        Assert.Equal(0.5f, mixer.EffectiveRatio, 2); // only "ui" remains
    }

    [Fact]
    public void ReloadSources_preserves_active_state_and_applies_new_params()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0.05f));
        mixer.Set("sfx", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f); // fully ducked to 0.3
        Assert.Equal(0.3f, mixer.EffectiveRatio, 2);

        mixer.ReloadSources(new Dictionary<string, DuckSourceConfig>
        {
            ["sfx"] = Source(ratio: 0.5f, attack: 0.05f),
        });

        // State survives the reload: still active, still at the old ratio (no snap to 1).
        mixer.Update(0f);
        Assert.True(mixer.Sources.Single(s => s.Name == "sfx").IsActive);
        Assert.Equal(0.3f, mixer.EffectiveRatio, 2);

        // But eases toward the NEW ratio.
        for (int i = 0; i < 100; i++) mixer.Update(0.1f);
        Assert.Equal(0.5f, mixer.EffectiveRatio, 2);
    }

    [Fact]
    public void ReloadSources_adds_new_and_drops_removed_sources()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0.05f));
        mixer.Set("sfx", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f);
        Assert.Equal(0.3f, mixer.EffectiveRatio, 2);

        mixer.ReloadSources(new Dictionary<string, DuckSourceConfig>
        {
            ["ui"] = Source(ratio: 0.5f, attack: 0.05f),
        });
        mixer.Set("ui", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f);

        Assert.Equal(0.5f, mixer.EffectiveRatio, 2); // "sfx" dropped, "ui" active
        Assert.Equal(0.5f, mixer.Sources.Single(s => s.Name == "ui").CurrentRatio, 2);
    }

    [Fact]
    public void Zero_hold_pulse_is_a_no_op()
    {
        // Symptom (consumer adoption): every sound effect calls SfxManager.Play, which
        // pulses the "sfx" source with no explicit hold. The shipped source declares
        // hold_seconds: 0, so a resolved hold of 0 landed in the old code as
        // IsActive = true with PulseRemaining = 0 - and Update only releases a source while
        // PulseRemaining > 0. The duck latched on forever and the music stayed quiet for the
        // rest of the session (recovery needed a scene switch or ReleaseAll).
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0f, hold: 0f, release: 0f));

        mixer.Pulse("sfx");
        mixer.Update(0.016f);

        Assert.Equal(1f, mixer.EffectiveRatio, 3);
        Assert.False(mixer.Sources.Single().IsActive);
    }

    [Fact]
    public void Zero_hold_pulse_leaves_an_explicit_hold_alone()
    {
        // A zero-hold pulse must not cancel a source another caller deliberately held on
        // (e.g. the dialogue duck Set("dialogue", true) while a cue speaks).
        var mixer = new DuckMixer();
        mixer.SetSource("dialogue", Source(ratio: 0.25f, attack: 0f, hold: 0f, release: 0f));
        mixer.Set("dialogue", true);

        mixer.Pulse("dialogue");
        mixer.Update(0.016f);

        Assert.Equal(0.25f, mixer.EffectiveRatio, 3);
        Assert.True(mixer.Sources.Single().IsActive);
    }

    [Fact]
    public void Sources_exposes_live_state_for_diagnostics()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("sfx", Source(ratio: 0.4f, attack: 0.05f));
        mixer.Set("sfx", true);
        for (int i = 0; i < 100; i++) mixer.Update(0.1f);

        var view = mixer.Sources.Single();
        Assert.Equal("sfx", view.Name);
        Assert.True(view.IsActive);
        Assert.Equal(0.4f, view.CurrentRatio, 2);
        Assert.Equal(0.4f, view.TargetRatio, 2);
    }

    [Fact]
    public void GainReduction_and_Attenuation_track_effective_ratio()
    {
        var mixer = new DuckMixer();
        Assert.Equal(0f, mixer.GainReduction, 3);
        Assert.Equal(0f, mixer.Attenuation, 3);

        mixer.SetSource("sfx", Source(ratio: 0.3f, attack: 0f, release: 0f));
        mixer.Set("sfx", true);
        mixer.Update(0.016f);

        Assert.Equal(0.7f, mixer.GainReduction, 3);
        Assert.Equal(0.7f, mixer.Attenuation, 3);
    }
}
