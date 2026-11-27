using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// Runtime state and routing for the one flat bus table (see <see cref="AudioConfig.Buses"/>):
/// the pre-registered <see cref="BusNames.Master"/>/<see cref="BusNames.Music"/>/
/// <see cref="BusNames.Sfx"/> entries plus the user buses, in one resolver.
///
/// <para>
/// Routing: a cue reaches a bus by its per-play <c>bus</c> argument. An un-routed cue stays on
/// the fixed SFX bus; an unknown bus name falls back to the SFX bus and raises <see
/// cref="BusFellBack"/> so the routing mistake surfaces instead of staying silent (the
/// no-silent-failure principle applied to routing). Effective gain per bus is volume × state
/// factor × (muted or paused ? 0 : 1); the global master mute zero-folds into every resolution,
/// and <see cref="Changed"/> fires on any state change so a frontend mixer can render this
/// state without owning it.
/// </para>
/// </summary>
public sealed class BusMixer
{
    /// <summary>The fixed SFX bus cues land on when un-routed or after an unknown-bus fallback.</summary>
    public const string SfxBusName = "sfx";

    private readonly Dictionary<string, BusState> _buses = new(StringComparer.Ordinal);

    /// <summary>Raised when a play named a bus that does not exist (the resolved gain is the SFX bus's).</summary>
    public event Action<string>? BusFellBack;

    /// <summary>Raised whenever any bus state changes (volume, mute, pause, master mute).</summary>
    public event Action? Changed;

    /// <summary>When true, every bus resolves to zero gain.</summary>
    public bool MasterMuted
    {
        get => _masterMuted;
        set
        {
            if (_masterMuted == value) return;
            _masterMuted = value;
            Changed?.Invoke();
        }
    }
    private bool _masterMuted;

    private readonly Func<string, float>? _duckRatioFor;

    /// <summary>Per-bus mix-state factors, written by the <see cref="MixStateManager"/> state
    /// layer and read at effective-gain time — never folded into the authored bus volume, so
    /// user settings (the slider writes) and states stay independent layers that compose
    /// multiplicatively. Null/absent means factor 1 (no state naming this bus).</summary>
    private readonly Dictionary<string, float> _stateFactors = new(StringComparer.Ordinal);

    /// <summary>
    /// Seeds the mixer from the flat bus table's authored volumes. The fixed three
    /// (<see cref="BusNames.Master"/>/<see cref="BusNames.Music"/>/<see cref="BusNames.Sfx"/>)
    /// always exist: missing entries (e.g. a table-less config) start at full volume, table
    /// entries override them, and user buses arrive alongside.
    /// </summary>
    /// <param name="buses">The authored bus table (see <see cref="AudioConfig.Buses"/>).</param>
    /// <param name="duckRatioFor">
    /// Live per-target duck ratio read at snapshot time (D4: duck never embeds in bus state);
    /// <see cref="Views"/> reports it as the per-bus duck contribution. Null means no duck
    /// system (every bus reports a contribution of 1).
    /// </param>
    public BusMixer(IEnumerable<KeyValuePair<string, BusConfig>>? buses = null, Func<string, float>? duckRatioFor = null)
    {
        _duckRatioFor = duckRatioFor;
        _buses[SfxBusName] = new BusState(1f);
        _buses[BusNames.Master] = new BusState(1f);
        _buses[BusNames.Music] = new BusState(1f);
        if (buses is null) return;
        foreach (var (name, config) in buses)
            _buses[name] = new BusState(config.Volume);
    }

    /// <summary>
    /// The authored volume of the named bus (not the effective gain: mute/pause do not fold in).
    /// Unknown names read 0; this is the hot-update read side of the managers' delegating
    /// volume properties.
    /// </summary>
    public float VolumeOf(string name) => _buses.TryGetValue(name, out var state) ? state.Volume : 0f;

    /// <summary>
    /// Hot-updates a bus's authored volume (same clamp as <see cref="AudioConfig.Sanitize"/>).
    /// This is the authoring surface: an unknown name intentionally creates the bus (authored
    /// hot updates follow the <c>DuckMixer.ReloadSources</c> pattern) and raises <see
    /// cref="Changed"/>. The routing surface is <see cref="Resolve"/>, which falls back loudly
    /// instead - the asymmetry between authoring (creates) and routing (falls back) is
    /// deliberate.
    /// </summary>
    public void SetBus(string name, float volume)
    {
        volume = Math.Clamp(float.IsNaN(volume) ? 0f : volume, 0f, 1f);
        if (_buses.TryGetValue(name, out var state))
        {
            // Per-frame hot-update loops set the same value every frame; only real state
            // changes re-notify (creation is a state change and always notifies).
            // Bit-exact comparison, not a tolerance question: both values pass the same
            // clamp, so any difference however small is an authored change that must notify -
            // an epsilon here would silently drop legitimate updates.
            if (BitConverter.SingleToInt32Bits(state.Volume)
                == BitConverter.SingleToInt32Bits(volume)) return;
            state.Volume = volume;
            Changed?.Invoke();
        }
        else
        {
            _buses[name] = new BusState(volume);
            Changed?.Invoke();
        }
    }

    /// <summary>Silences a bus at zero gain; <see cref="SetMute(name, false)"/> restores it.</summary>
    public void SetMute(string name, bool muted)
    {
        if (_buses.TryGetValue(name, out var state))
        {
            if (state.Muted == muted) return;
            state.Muted = muted;
            Changed?.Invoke();
        }
    }

    /// <summary>Pauses a bus at zero gain (distinct flag from mute so a UI can show them apart).</summary>
    public void SetPaused(string name, bool paused)
    {
        if (_buses.TryGetValue(name, out var state))
        {
            if (state.Paused == paused) return;
            state.Paused = paused;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Resolves where a cue plays and at what bus gain. Null routes to the SFX bus; an unknown
    /// name falls back to the SFX bus (raising <see cref="BusFellBack"/>).
    /// </summary>
    public BusResolution Resolve(string? requestedBus)
    {
        var fellBack = false;
        if (requestedBus is not null && !_buses.ContainsKey(requestedBus))
        {
            BusFellBack?.Invoke(requestedBus);
            requestedBus = SfxBusName;
            fellBack = true;
        }

        var bus = requestedBus ?? SfxBusName;
        var gain = MasterMuted ? 0f : EffectiveGainOf(bus, _buses[bus]);
        return new BusResolution(bus, gain, fellBack);
    }

    /// <summary>Read-only snapshot of every bus for diagnostics.</summary>
    public IReadOnlyList<BusView> Views
    {
        get
        {
            var list = new List<BusView>(_buses.Count);
            foreach (var (name, state) in _buses)
                list.Add(new BusView(
                    name, state.Volume, state.Muted, state.Paused,
                    MasterMuted ? 0f : EffectiveGainOf(name, state),
                    _duckRatioFor is null ? 1f : _duckRatioFor(name),
                    StateFactorOf(name)));
            return list;
        }
    }

    /// <summary>
    /// Writes the named bus's mix-state factor (0..1, the multiplier the state layer applies
    /// on top of the authored bus volume). Hot-updated per frame by
    /// <see cref="MixStateManager"/>'s ease; the authored volume is never touched, so a user
    /// settings write to the bus survives mid-transition. Same clamp and bit-exact
    /// no-op-on-unchanged rule as <see cref="SetBus"/>.
    /// </summary>
    public void SetStateFactor(string name, float factor)
    {
        factor = Math.Clamp(float.IsNaN(factor) ? 0f : factor, 0f, 1f);
        if (_stateFactors.TryGetValue(name, out var existing)
            && BitConverter.SingleToInt32Bits(existing) == BitConverter.SingleToInt32Bits(factor)) return;
        _stateFactors[name] = factor;
        Changed?.Invoke();
    }

    /// <summary>The named bus's mix-state factor (1 when the state layer never named it).</summary>
    public float StateFactorOf(string name) =>
        _stateFactors.TryGetValue(name, out var factor) ? factor : 1f;

    private float EffectiveGainOf(string bus, BusState state) =>
        state.Muted || state.Paused ? 0f : state.Volume * StateFactorOf(bus);

    private sealed class BusState(float volume)
    {
        public float Volume = volume;
        public bool Muted;
        public bool Paused;
    }
}

/// <summary>The outcome of resolving a play's bus: which bus it landed on, at what gain.</summary>
public readonly record struct BusResolution(string Bus, float Gain, bool FellBack);

/// <summary>
/// Diagnostics view of one bus: as authored (volume, mute, pause), as currently effective
/// (gain), the live duck contribution the duck system currently applies to it (1 = no
/// ducking; D4: the ratio is read at snapshot time, never stored in bus state), and the
/// mix-state factor the state layer currently applies (1 = no state naming this bus).
/// </summary>
public readonly record struct BusView(string Name, float Volume, bool Muted, bool Paused, float EffectiveGain, float DuckRatio, float StateFactor = 1f);
