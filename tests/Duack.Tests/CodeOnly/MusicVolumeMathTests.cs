using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins who owns music volume. <c>MusicController</c> applies the mix per frame on both heads
/// (<c>SoundEffectInstance.Volume</c> on desktop, <c>MediaPlayer.Volume</c> on web), so the engine's
/// <c>Audio.MusicVolume</c> plays no part: that engine field only feeds FRB2's own music backend,
/// which this project never uses. The formula lived inline in three places and only the web copy
/// clamped, so it is pinned here as the single source of truth.
/// </summary>
public class MusicVolumeMathTests
{
    [Fact]
    public void Effective_multiplies_master_context_duck_and_fade()
    {
        Assert.Equal(0.25f, MusicVolumeMath.Effective(1f, 0.5f, 0.5f, 1f), 4);
    }

    [Fact]
    public void Effective_is_silent_when_any_factor_is_zero()
    {
        Assert.Equal(0f, MusicVolumeMath.Effective(0f, 1f, 1f, 1f));
        Assert.Equal(0f, MusicVolumeMath.Effective(1f, 0f, 1f, 1f));
        Assert.Equal(0f, MusicVolumeMath.Effective(1f, 1f, 0f, 1f));
        Assert.Equal(0f, MusicVolumeMath.Effective(1f, 1f, 1f, 0f));
    }

    [Fact]
    public void Effective_clamps_to_one()
    {
        // Only reachable with hand-edited config values, but the desktop backend threw on an
        // out-of-range Volume, so the clamp is part of the contract on both heads.
        Assert.Equal(1f, MusicVolumeMath.Effective(2f, 2f, 1f, 1f));
    }

    [Fact]
    public void Effective_clamps_a_negative_product()
    {
        Assert.Equal(0f, MusicVolumeMath.Effective(1f, 1f, -1f, 1f));
    }

    [Fact]
    public void Effective_treats_non_finite_input_as_silence()
    {
        Assert.Equal(0f, MusicVolumeMath.Effective(float.NaN, 1f, 1f, 1f));
        Assert.Equal(0f, MusicVolumeMath.Effective(1f, float.PositiveInfinity, 1f, 1f));
    }
}
