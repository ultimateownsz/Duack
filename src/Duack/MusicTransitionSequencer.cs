using System;

namespace Duack;

/// <summary>Phase of a web (single-track) music transition.</summary>
public enum MusicFadePhase
{
    /// <summary>Nothing fading; the current track plays at the full mix volume.</summary>
    Steady,

    /// <summary>The current track is ramping to silence before the next one starts (fade out / fade down).</summary>
    FadingOut,

    /// <summary>The new track is ramping up from silence (fade in / fade up).</summary>
    FadingIn,

    /// <summary>Studio alias for <see cref="FadingOut"/>.</summary>
    FadingDown = FadingOut,

    /// <summary>Studio alias for <see cref="FadingIn"/>.</summary>
    FadingUp = FadingIn,
}

/// <summary>
/// Pure state machine for a deferred music transition, used by the web head where one
/// <c>MediaPlayer</c> cannot overlap two tracks: the current track fades out, the caller switches,
/// then the new track fades in. The desktop head does not need this - it overlaps two instances.
///
/// <para>
/// A fade-out is half the configured crossfade duration and the fade-in that follows it is the other
/// half, so a full switch costs <c>CrossfadeSeconds</c> - the same wall-clock time the desktop overlap
/// takes. A standalone <see cref="BeginFadeIn"/> (the first track of a session) uses the whole
/// duration, since there is no outgoing track to share it with.
/// </para>
///
/// <para>
/// The caller owns target selection: it points at the newest context and calls
/// <see cref="BeginFadeOut"/> (a no-op while already fading out, so a re-request does not restart the
/// ramp). <see cref="Advance"/> returns <c>true</c> exactly once, on the frame the fade-out
/// completes; at that point the caller stops the old track and starts the new one, and the sequencer
/// is already in <see cref="MusicFadePhase.FadingIn"/>.
/// </para>
/// </summary>
public sealed class MusicTransitionSequencer
{
    public MusicFadePhase Phase { get; private set; } = MusicFadePhase.Steady;

    /// <summary>Progress through the current phase, 0..1.</summary>
    public float Progress { get; private set; } = 1f;

    /// <summary>Incoming gain to apply to the (possibly new) active track.</summary>
    public float InGain { get; private set; } = 1f;

    /// <summary>Outgoing gain to apply to the track being replaced.</summary>
    public float OutGain { get; private set; }

    public bool IsFadingOut => Phase == MusicFadePhase.FadingOut;

    /// <summary>Duration of the fade-out phase (seconds). Set by the caller before <see cref="BeginFadeOut"/>.</summary>
    public float FadeOutDuration { get; set; } = 0.6f;

    /// <summary>Duration of the fade-in phase (seconds). Set by the caller before <see cref="BeginFadeIn"/>.</summary>
    public float FadeInDuration { get; set; } = 0.6f;

    /// <summary>Studio alias for <see cref="IsFadingOut"/>.</summary>
    public bool IsFadingDown => IsFadingOut;

    /// <summary>Level the outgoing track started from, so a fade-out interrupted mid-fade-in does not jump.</summary>
    private float _outStart = 1f;

    /// <summary>True when the active fade-in follows a fade-out (so it uses the split half duration).</summary>
    private bool _splitFadeIn;

    /// <summary>Fades a track in from silence over the full duration (first track, or a restart).</summary>
    public void BeginFadeIn()
    {
        Phase = MusicFadePhase.FadingIn;
        Progress = 0f;
        _splitFadeIn = false;
        Recompute();
    }

    /// <summary>Studio alias for <see cref="BeginFadeIn"/>.</summary>
    public void BeginFadeUp() => BeginFadeIn();

    /// <summary>Starts fading the current track out. Idempotent while already fading out.</summary>
    public void BeginFadeOut()
    {
        if (Phase == MusicFadePhase.FadingOut)
            return;

        _outStart = Phase == MusicFadePhase.FadingIn ? InGain : 1f;
        Phase = MusicFadePhase.FadingOut;
        Progress = 0f;
        _splitFadeIn = false;
        Recompute();
    }

    /// <summary>Studio alias for <see cref="BeginFadeOut"/>.</summary>
    public void BeginFadeDown() => BeginFadeOut();

    /// <summary>
    /// Advances the active phase by <paramref name="deltaSeconds"/>. Returns <c>true</c> exactly once,
    /// when a fade-out completes and the caller should switch tracks.
    /// </summary>
    public bool Advance(float deltaSeconds)
    {
        if (Phase == MusicFadePhase.Steady)
            return false;

        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
            return false;

        var fadeOutCompleted = false;
        var remaining = deltaSeconds;
        // Consume the whole delta, crossing phase boundaries (fade-out -> fade-in -> steady)
        // so a single large Update() can complete a multi-phase transition.
        while (remaining > 0f && Phase != MusicFadePhase.Steady)
        {
            var phaseDuration = Phase == MusicFadePhase.FadingOut
                ? FadeOutDuration
                : (_splitFadeIn ? FadeInDuration : FadeInDuration);

            if (!float.IsFinite(phaseDuration) || phaseDuration <= 0f)
            {
                Progress = 1f;
                remaining = 0f;
            }
            else
            {
                var needed = (1f - Progress) * phaseDuration;
                if (needed <= 0f)
                {
                    Progress = 1f;
                }
                else if (remaining >= needed)
                {
                    remaining -= needed;
                    Progress = 1f;
                }
                else
                {
                    Progress += remaining / phaseDuration;
                    remaining = 0f;
                }
            }

            if (Progress >= 1f)
            {
                if (Phase == MusicFadePhase.FadingOut)
                {
                    Phase = MusicFadePhase.FadingIn;
                    Progress = 0f;
                    _splitFadeIn = true;
                    fadeOutCompleted = true;
                }
                else
                {
                    Phase = MusicFadePhase.Steady;
                    Progress = 1f;
                    _splitFadeIn = false;
                }
            }
        }

        Recompute();
        return fadeOutCompleted;
    }

    /// <summary>
    /// Backward-compatible overload: treats <paramref name="durationSeconds"/> as the total transition
    /// time, splitting it equally between fade-out and fade-in (the historical behaviour).
    /// </summary>
    public bool Advance(float deltaSeconds, float durationSeconds)
    {
        if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
        {
            FadeOutDuration = 0f;
            FadeInDuration = 0f;
        }
        else
        {
            FadeOutDuration = durationSeconds / 2f;
            // A standalone fade-in (not following a fade-out) uses the whole duration.
            FadeInDuration = _splitFadeIn ? durationSeconds / 2f : durationSeconds;
        }
        return Advance(deltaSeconds);
    }

    /// <summary>Abandons any transition and returns to the steady state at full volume.</summary>
    public void Cancel()
    {
        Phase = MusicFadePhase.Steady;
        Progress = 1f;
        _splitFadeIn = false;
        Recompute();
    }

    private void Recompute()
    {
        switch (Phase)
        {
            case MusicFadePhase.FadingOut:
                OutGain = _outStart * CrossfadeMath.EqualPower(Progress).Out;
                InGain = 0f;
                break;
            case MusicFadePhase.FadingIn:
                InGain = CrossfadeMath.EqualPower(Progress).In;
                OutGain = 0f;
                break;
            default:
                InGain = 1f;
                OutGain = 0f;
                break;
        }
    }
}
