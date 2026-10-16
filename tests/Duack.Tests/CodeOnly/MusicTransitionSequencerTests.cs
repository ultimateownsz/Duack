using Duack;
using Xunit;

namespace Duack.Tests.CodeOnly;

/// <summary>
/// Pins the deferred music transition used by the web head, where one <c>MediaPlayer</c> cannot
/// overlap two tracks: the current track fades out, the caller switches, then the new track fades in.
/// The desktop instance mixer overlaps and does not use this sequencer, but both must consume the
/// same <see cref="CrossfadeMath"/> curves so a transition sounds the same wherever it can happen.
/// </summary>
public class MusicTransitionSequencerTests
{
    private const float Duration = 1.2f;
    private const float Half = Duration / 2f;

    [Fact]
    public void Starts_steady_at_full_volume()
    {
        var sequencer = new MusicTransitionSequencer();

        Assert.Equal(MusicFadePhase.Steady, sequencer.Phase);
        Assert.Equal(1f, sequencer.InGain);
        Assert.Equal(0f, sequencer.OutGain);
        Assert.False(sequencer.IsFadingOut);
    }

    [Fact]
    public void BeginFadeIn_silences_then_restores_over_the_full_duration()
    {
        var sequencer = new MusicTransitionSequencer();
        sequencer.BeginFadeIn();

        Assert.Equal(MusicFadePhase.FadingIn, sequencer.Phase);
        Assert.Equal(0f, sequencer.InGain, 4);

        sequencer.Advance(Half, Duration);
        Assert.Equal(0.7071f, sequencer.InGain, 3);

        sequencer.Advance(Half, Duration);
        Assert.Equal(MusicFadePhase.Steady, sequencer.Phase);
        Assert.Equal(1f, sequencer.InGain, 4);
    }

    [Fact]
    public void BeginFadeOut_ramps_the_outgoing_track_down_and_reports_completion_once()
    {
        var sequencer = new MusicTransitionSequencer();
        sequencer.BeginFadeOut();

        Assert.Equal(MusicFadePhase.FadingOut, sequencer.Phase);
        Assert.True(sequencer.IsFadingOut);
        Assert.Equal(1f, sequencer.OutGain, 4);

        // Halfway through the fade-out the outgoing track is at the equal-power midpoint, not silent.
        Assert.False(sequencer.Advance(Half / 2f, Duration));
        Assert.Equal(0.7071f, sequencer.OutGain, 3);

        Assert.True(sequencer.Advance(Half / 2f, Duration));
        Assert.Equal(MusicFadePhase.FadingIn, sequencer.Phase);
        Assert.Equal(0f, sequencer.InGain, 4);
    }

    [Fact]
    public void A_switch_cycle_takes_the_full_configured_duration()
    {
        var sequencer = new MusicTransitionSequencer();
        sequencer.BeginFadeOut();

        var elapsed = 0f;
        var fadeOutCompleted = false;
        while (!fadeOutCompleted && elapsed < Duration)
        {
            elapsed += Half;
            fadeOutCompleted = sequencer.Advance(Half, Duration);
        }

        Assert.True(fadeOutCompleted);
        Assert.Equal(Half, elapsed, 4);

        sequencer.Advance(Half, Duration);
        Assert.Equal(MusicFadePhase.Steady, sequencer.Phase);
        Assert.Equal(Duration, elapsed + Half, 4);
    }

    [Fact]
    public void BeginFadeOut_is_idempotent_so_a_new_target_does_not_restart_the_ramp()
    {
        var sequencer = new MusicTransitionSequencer();
        sequencer.BeginFadeOut();
        sequencer.Advance(Half / 2f, Duration);
        var midway = sequencer.OutGain;

        sequencer.BeginFadeOut();

        Assert.Equal(midway, sequencer.OutGain, 4);
    }

    [Fact]
    public void BeginFadeOut_while_fading_in_continues_from_the_current_level()
    {
        var sequencer = new MusicTransitionSequencer();
        sequencer.BeginFadeIn();
        sequencer.Advance(Half, Duration);
        var level = sequencer.InGain;

        sequencer.BeginFadeOut();

        Assert.Equal(level, sequencer.OutGain, 3);
    }

    [Fact]
    public void Cancel_returns_to_steady_immediately()
    {
        var sequencer = new MusicTransitionSequencer();
        sequencer.BeginFadeOut();
        sequencer.Advance(Half / 2f, Duration);

        sequencer.Cancel();

        Assert.Equal(MusicFadePhase.Steady, sequencer.Phase);
        Assert.Equal(1f, sequencer.InGain);
        Assert.Equal(0f, sequencer.OutGain);
    }

    [Fact]
    public void Advance_is_a_no_op_while_steady()
    {
        var sequencer = new MusicTransitionSequencer();

        Assert.False(sequencer.Advance(Half, Duration));
        Assert.Equal(MusicFadePhase.Steady, sequencer.Phase);
        Assert.Equal(1f, sequencer.InGain);
    }
}
