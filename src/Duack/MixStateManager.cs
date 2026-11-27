using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// Applies named mix states (see <see cref="MixStateConfig"/>) to the runtime bus mixers.
/// <see cref="ApplyState"/> writes a state's target vector instantly;
/// <see cref="TransitionTo(string, float)"/> eases every named bus toward its target with
/// the one-pole smoothing of the shared plan (g(t) = g_target + (g(t0) − g_target)·e^(−t/τ),
/// τ = duration / ln(1/SettleEpsilon), so the ease settles exactly at its duration), with
/// telemetry matching the existing crossfade pattern (<see cref="IsTransitioning"/>,
/// elapsed/duration, from/to). State targets are bus volume inputs to the same single
/// effective-gain resolver: ducks stay live on top of them (read at snapshot time, never
/// embedded) and the player's settings sliders still layer over the state via their own bus
/// writes. A state only names buses: buses it omits are untouched.
///
/// <para>
/// Constructed with the authored state table and every <see cref="BusMixer"/> the game's
/// managers own — the two mixers stay two states by design (volumes snapshot at
/// construction), so a state must write both to move the whole mix. Call
/// <see cref="Update(float)"/> once per frame with the same frame delta the managers get
/// (unscaled, like fades and ducks).
/// </para>
/// </summary>
public sealed class MixStateManager
{
    /// <summary>
    /// The one-pole ease snaps to its target within this epsilon (inaudible ~0.0009 dB) —
    /// the same settle epsilon the duck sources use.
    /// </summary>
    public const float SettleEpsilon = 0.0001f;

    private readonly Dictionary<string, MixStateConfig> _states;
    private readonly BusMixer[] _mixers;
    private readonly Dictionary<string, float> _starts = new(StringComparer.Ordinal);
    private MixStateConfig? _transitionTargets;

    /// <summary>
    /// Raised when <see cref="ApplyState"/> or <see cref="TransitionTo"/> named a state that
    /// is not authored — the mistake surfaces instead of staying silent (the
    /// no-silent-failure principle applied to states). The mix is untouched.
    /// </summary>
    public event Action<string>? UnknownState;

    /// <summary>The name of the state that is settled (last applied or transitioned to).</summary>
    public string? CurrentState { get; private set; }

    /// <summary>True while a timed transition is being realized.</summary>
    public bool IsTransitioning { get; private set; }

    /// <summary>Elapsed time in the current transition (0 when none is active).</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>Total duration of the current transition (0 when none is active).</summary>
    public float DurationSeconds { get; private set; }

    /// <summary>
    /// The state the current transition started from, if a transition is active. When a
    /// transition interrupts an in-flight one, this is the interrupted transition's target
    /// (the mix was heading there and is now leaving it), not the last settled state.
    /// </summary>
    public string? FromState { get; private set; }

    /// <summary>The state the current transition is easing toward, if a transition is active.</summary>
    public string? ToState { get; private set; }

    /// <param name="states">The authored state table (see <see cref="AudioConfig.MixStates"/>).</param>
    /// <param name="mixers">Every runtime bus mixer the states drive (music and SFX).</param>
    public MixStateManager(IEnumerable<KeyValuePair<string, MixStateConfig>>? states, params BusMixer[] mixers)
    {
        _states = states is null
            ? new Dictionary<string, MixStateConfig>(StringComparer.Ordinal)
            : new Dictionary<string, MixStateConfig>(states, StringComparer.Ordinal);
        _mixers = mixers ?? Array.Empty<BusMixer>();
    }

    /// <summary>
    /// Writes the named state's target vector instantly on every mixer. Buses the state does
    /// not name keep their current volume. Returns false — touching nothing, reporting
    /// through <see cref="UnknownState"/> — when the state is not authored.
    /// </summary>
    public bool ApplyState(string name)
    {
        if (!_states.TryGetValue(name, out var state))
        {
            UnknownState?.Invoke(name);
            return false;
        }

        WriteTargets(state);
        CurrentState = name;
        ClearTransition();
        return true;
    }

    /// <summary>
    /// Begins easing every bus the named state targets toward its target over
    /// <paramref name="seconds"/> (unscaled), snapping exactly at the duration. A duration of
    /// zero (or less) applies the state instantly instead. Returns false — touching nothing,
    /// reporting through <see cref="UnknownState"/> — when the state is not authored. A
    /// transition can be started while another is running: it restarts the ease from the
    /// current volumes toward the new state's targets.
    /// </summary>
    public bool TransitionTo(string name, float seconds)
    {
        if (!_states.TryGetValue(name, out var state))
        {
            UnknownState?.Invoke(name);
            return false;
        }

        if (float.IsNaN(seconds) || seconds <= 0f)
            return ApplyState(name);

        // The state being left is the in-flight target when interrupting (CurrentState is
        // null mid-transition), so the interrupted transition is reported as the origin.
        FromState = ToState ?? CurrentState;
        ToState = name;
        CurrentState = null;
        IsTransitioning = true;
        ElapsedSeconds = 0f;
        DurationSeconds = seconds;
        _transitionTargets = state;

        // Capture each target bus's starting volume from the first mixer that owns it: the
        // mixers seed from the same table and every write reaches all of them, so they agree
        // unless a caller hand-diverged them — in which case the ease re-converges them on
        // the first frame by construction.
        _starts.Clear();
        foreach (var bus in state.Buses.Keys)
            _starts[bus] = _mixers.Length > 0 ? _mixers[0].VolumeOf(bus) : 0f;
        return true;
    }

    /// <summary>Advances the active transition; call once per frame with the frame delta.</summary>
    public void Update(float deltaSeconds)
    {
        if (!IsTransitioning || _transitionTargets is null) return;

        if (deltaSeconds > 0f)
            ElapsedSeconds += deltaSeconds;
        var finished = ElapsedSeconds >= DurationSeconds;
        var tau = DurationSeconds / MathF.Log(1f / SettleEpsilon);

        foreach (var (bus, target) in _transitionTargets.Buses)
        {
            var value = Sanitize(target);
            if (!finished)
            {
                // Absolute closed form (start value → target over elapsed), not an
                // incremental step: repeated updates compose to the same curve with no drift.
                var start = _starts.TryGetValue(bus, out var captured) ? captured : _mixers.Length > 0 ? _mixers[0].VolumeOf(bus) : value;
                var eased = value + (start - value) * MathF.Exp(-ElapsedSeconds / tau);
                if (MathF.Abs(eased - value) < SettleEpsilon)
                    eased = value;
                value = Sanitize(eased);
            }

            foreach (var mixer in _mixers)
                mixer.SetBus(bus, value);
        }

        if (!finished) return;

        CurrentState = ToState;
        ClearTransition();
    }

    private void WriteTargets(MixStateConfig state)
    {
        foreach (var (bus, target) in state.Buses)
            foreach (var mixer in _mixers)
                mixer.SetBus(bus, Sanitize(target));
    }

    private void ClearTransition()
    {
        IsTransitioning = false;
        ElapsedSeconds = 0f;
        DurationSeconds = 0f;
        FromState = null;
        ToState = null;
        _transitionTargets = null;
        _starts.Clear();
    }

    private static float Sanitize(float volume) =>
        Math.Clamp(float.IsNaN(volume) ? 0f : volume, 0f, 1f);
}
