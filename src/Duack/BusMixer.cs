using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// Runtime state and routing for the user-defined buses authored in <see cref="SfxConfig.Buses"/>.
///
/// <para>
/// Routing: a cue reaches a bus by its per-play <c>bus</c> argument. An un-routed cue stays on
/// the fixed SFX bus; an unknown bus name falls back to the SFX bus and raises <see
/// cref="BusFellBack"/> so the routing mistake surfaces instead of staying silent (the
/// no-silent-failure principle applied to routing). Effective gain per bus is volume × (muted
/// or paused ? 0 : 1); the global master mute zero-folds into every resolution, and <see
/// cref="Changed"/> fires on any state change so a frontend mixer can render this state
/// without owning it.
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

    /// <summary>Seeds the mixer from the authored bus volumes; the fixed SFX bus always exists.</summary>
    public BusMixer(IEnumerable<KeyValuePair<string, float>>? buses = null)
    {
        _buses[SfxBusName] = new BusState(1f);
        if (buses is null) return;
        foreach (var (name, volume) in buses)
            if (name != SfxBusName)
                _buses[name] = new BusState(volume);
    }

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
        var gain = MasterMuted ? 0f : _buses[bus].EffectiveGain;
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
                    MasterMuted ? 0f : state.EffectiveGain));
            return list;
        }
    }

    private sealed class BusState(float volume)
    {
        public float Volume = volume;
        public bool Muted;
        public bool Paused;
        public float EffectiveGain => Muted || Paused ? 0f : Volume;
    }
}

/// <summary>The outcome of resolving a play's bus: which bus it landed on, at what gain.</summary>
public readonly record struct BusResolution(string Bus, float Gain, bool FellBack);

/// <summary>Diagnostics view of one bus (as authored and as currently effective).</summary>
public readonly record struct BusView(string Name, float Volume, bool Muted, bool Paused, float EffectiveGain);
