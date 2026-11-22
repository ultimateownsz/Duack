using System;
using System.Collections.Generic;
using System.Linq;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins slice C duck extensions: ref-counted <see cref="DuckMixer.BeginDuck"/>/<see
/// cref="DuckMixer.EndDuck"/> holds (overlapping speakers cannot release early) and
/// per-source duck <c>Target</c>s — "music" (default) composes the music's
/// <see cref="DuckMixer.EffectiveRatio"/>, a bus target composes only under <see
/// cref="DuckMixer.RatioFor"/>, keeping music ducking unaffected.
/// </summary>
public class DuckHoldAndTargetTests
{
    private static DuckMixer Mixer(string source, float ratio = 0.5f, string target = "music")
    {
        var mixer = new DuckMixer();
        mixer.SetSource(source, new DuckSourceConfig
        {
            Ratio = ratio,
            AttackSeconds = 0f,
            ReleaseSeconds = 0f,
            Target = target,
        });
        return mixer;
    }

    private static void AssertActive(DuckMixer mixer, float expectedRatio, string target, int precision = 3)
    {
        mixer.Update(1f);
        Assert.Equal(expectedRatio, target == "music" ? mixer.RatioFor(target) : mixer.RatioFor(target), precision);
    }

    [Fact]
    public void BeginDuck_Once_ActivesTarget()
    {
        var mixer = Mixer("dialogue", target: "ambience");
        mixer.BeginDuck("dialogue");
        AssertActive(mixer, 0.5f, "ambience");
    }

    [Fact]
    public void BeginDuck_Twice_EndOnce_StillActive()
    {
        var mixer = Mixer("dialogue", target: "ambience");
        mixer.BeginDuck("dialogue");
        mixer.BeginDuck("dialogue");

        mixer.EndDuck("dialogue");
        mixer.Update(1f);
        Assert.Equal(0.5f, mixer.RatioFor("ambience"), 3);

        mixer.EndDuck("dialogue");
        AssertActive(mixer, 1f, "ambience");
    }

    [Fact]
    public void EndDuck_OverReleasesAreHarmless()
    {
        var mixer = Mixer("dialogue");
        mixer.EndDuck("dialogue");
        mixer.EndDuck("dialogue");
        AssertActive(mixer, 1f, "music");
    }

    [Fact]
    public void ReleaseAll_ClearsHolds()
    {
        var mixer = Mixer("dialogue");
        mixer.BeginDuck("dialogue");
        mixer.BeginDuck("dialogue");

        mixer.ReleaseAll();

        AssertActive(mixer, 1f, "music");
        mixer.BeginDuck("dialogue");
        mixer.EndDuck("dialogue");
        AssertActive(mixer, 1f, "music");
    }

    [Fact]
    public void BusTarget_DoesNotDuckMusic()
    {
        var mixer = new DuckMixer();
        mixer.SetSource("dialogue", new DuckSourceConfig
        {
            Ratio = 0.5f,
            AttackSeconds = 0f,
            Target = "ambience",
        });
        mixer.SetSource("sfx", new DuckSourceConfig
        {
            Ratio = 0.3f,
            AttackSeconds = 0f,
        });
        mixer.BeginDuck("dialogue");
        mixer.Set("sfx", true);

        mixer.Update(1f);

        Assert.Equal(0.3f, mixer.EffectiveRatio, 3);
        Assert.Equal(0.5f, mixer.RatioFor("ambience"), 3);
    }

    [Fact]
    public void TargetDefaultsToMusic()
    {
        var mixer = Mixer("sfx", ratio: 0.3f);
        mixer.BeginDuck("sfx");
        mixer.Update(1f);
        Assert.Equal(0.3f, mixer.EffectiveRatio, 3);
        Assert.Equal("music", Assert.Single(mixer.Sources).Target);
    }
}
