using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the duck-release-on-scene-switch behaviour: a scene switch (overworld→battle) must not
/// inherit the previous scene's duck hold, otherwise the incoming track fades in under a stale
/// dip and the ducking feels delayed.
/// </summary>
public class DuckMixerReleaseTests
{
    private static MusicConfig DemoMusic() => new AudioConfigBuilder()
        .WithDuckSource("sfx", new DuckSourceConfig
        {
            Ratio = 0.3f, AttackSeconds = 0.06f, HoldSeconds = 0.4f, ReleaseSeconds = 0.35f
        })
        .Build().Music;

    [Fact]
    public void ReleaseAll_immediately_resets_every_source_to_unducked()
    {
        var duck = new DuckMixer();
        foreach (var (name, source) in DemoMusic().DuckSources)
            duck.SetSource(name, source);

        // Simulate a dialog cue pulsing the duck.
        duck.Pulse("sfx");
        duck.Update(0.01f); // one frame of attack
        Assert.True(duck.EffectiveRatio < 1f, "precondition: duck should be active after pulse");

        // Scene switch releases everything immediately — no easing tail.
        duck.ReleaseAll();

        Assert.Equal(1f, duck.EffectiveRatio);
        Assert.All(duck.Sources, s => Assert.False(s.IsActive));
    }

    [Fact]
    public void ReleaseAll_on_an_unducked_mixer_is_a_no_op()
    {
        var duck = new DuckMixer();
        duck.SetSource("sfx", new DuckSourceConfig { Ratio = 0.3f });

        duck.ReleaseAll();

        Assert.Equal(1f, duck.EffectiveRatio);
    }
}
