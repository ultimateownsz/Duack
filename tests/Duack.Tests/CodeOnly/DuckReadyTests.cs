using System;
using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the duck-readiness gate used to synchronize scene transitions with the audio
/// crossfade: a visual scene change can be deferred until the duck has recovered so the
/// incoming track starts at full volume.
/// </summary>
public class DuckReadyTests
{
    private static MusicConfig DemoMusic() => new AudioConfigBuilder()
        .WithDuckSource("sfx", new DuckSourceConfig
        {
            Ratio = 0.3f, AttackSeconds = 0.06f, HoldSeconds = 0.4f, ReleaseSeconds = 0.35f
        })
        .Build().Music;

    [Fact]
    public void IsDuckReady_returns_true_when_no_duck_is_active()
    {
        var duck = new DuckMixer();
        duck.SetSource("sfx", new DuckSourceConfig { Ratio = 0.3f });

        Assert.True(duck.IsReady(0.99f));
    }

    [Fact]
    public void IsDuckReady_returns_false_while_a_duck_is_active_and_recovers_after_release()
    {
        var duck = new DuckMixer();
        duck.SetSource("sfx", new DuckSourceConfig { Ratio = 0.3f, AttackSeconds = 0.06f, HoldSeconds = 0.4f, ReleaseSeconds = 0.35f });

        duck.Pulse("sfx");
        duck.Update(0.01f); // one frame of attack - duck is now active

        Assert.False(duck.IsReady(0.99f));

        // After enough time for hold + release, duck should recover.
        for (var i = 0; i < 200; i++)
            duck.Update(0.016f);

        Assert.True(duck.IsReady(0.99f));
    }

    [Fact]
    public void IsDuckReady_returns_true_immediately_after_ReleaseAll()
    {
        var duck = new DuckMixer();
        duck.SetSource("sfx", new DuckSourceConfig { Ratio = 0.3f, AttackSeconds = 0.06f, HoldSeconds = 0.4f, ReleaseSeconds = 0.35f });

        duck.Pulse("sfx");
        duck.Update(0.01f);
        Assert.False(duck.IsReady(0.99f));

        duck.ReleaseAll();
        Assert.True(duck.IsReady(0.99f));
    }
}
