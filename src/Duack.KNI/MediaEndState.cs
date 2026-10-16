using System;

namespace Duack.KNI;

/// <summary>
/// Decides whether an observed platform "stopped" state is a NATURAL track end (the track played to
/// completion and should be reported to the core so the playlist advances) or the delayed echo of a
/// stop/restart WE issued (a track switch, which must be ignored).
///
/// <para>
/// <b>Why this exists.</b> The platform player is a single HTML <c>&lt;audio&gt;</c> element whose
/// <c>MediaStateChanged</c> event carries no track identity and no reason. A context switch is
/// implemented as <c>Stop()</c> immediately followed by <c>Play(next)</c>, and the browser may
/// deliver the "stopped" signal for the OLD track only after the new one already started. Honouring
/// that stale signal would make the core believe the NEW track finished and skip it.
/// </para>
/// <para>
/// Two guards cover that: (1) <see cref="StoppedByUs"/> latches the state out of "running"
/// SYNCHRONOUSLY, so a stop signal that arrives after an explicit stop finds nothing to report; and
/// (2) <see cref="Started"/> opens a short settle window during which stop signals are swallowed,
/// because they can only be transition churn - tracks are minutes long, so a real end cannot occur
/// within a few frames of starting. Pure and headless-tested; time is advanced by the caller via
/// <see cref="Advance"/>, never read from a clock.
/// </para>
/// </summary>
public sealed class MediaEndState
{
    private readonly float _settleSeconds;
    private float _settleRemaining;

    public MediaEndState(float settleSeconds = 0.25f)
    {
        if (settleSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(settleSeconds));
        _settleSeconds = settleSeconds;
    }

    /// <summary>True while a track we started is considered live (not yet reported as ended or stopped).</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Records that WE started a track, opening the settle window.</summary>
    public void Started()
    {
        IsRunning = true;
        _settleRemaining = _settleSeconds;
    }

    /// <summary>Records that WE stopped the player, so the resulting signal must not be reported.</summary>
    public void StoppedByUs()
    {
        IsRunning = false;
        _settleRemaining = 0f;
    }

    /// <summary>Advances the settle window. Call once per frame.</summary>
    public void Advance(float deltaSeconds)
    {
        if (_settleRemaining > 0f)
        {
            _settleRemaining -= deltaSeconds;
            if (_settleRemaining < 0f)
                _settleRemaining = 0f;
        }
    }

    /// <summary>
    /// Called when the platform reports the player stopped. Returns true EXACTLY ONCE per started
    /// track when that stop is a natural end; returns false for echoes of our own stop and for
    /// transitions still inside the settle window.
    /// </summary>
    public bool ObserveStopped()
    {
        if (!IsRunning)
            return false;
        if (_settleRemaining > 0f)
            return false;

        IsRunning = false; // latch: report a given end at most once
        return true;
    }
}