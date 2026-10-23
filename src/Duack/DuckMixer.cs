using System;
using System.Collections.Generic;
using System.Linq;

namespace Duack;

/// <summary>
/// Smooths multiple named duck sources toward their target ratios. Each source eases toward its
/// target with an exponential time constant (attack when ducking down, release when coming back
/// up), snapping to the exact target once within <c>SettleEpsilon</c>; active sources combine
/// multiplicatively.
/// </summary>
public sealed class DuckMixer
{
    private readonly Dictionary<string, DuckSourceState> _sources = new();

    /// <summary>The combined ratio applied to the music each frame (1 = no ducking).</summary>
    public float EffectiveRatio { get; private set; } = 1f;

    /// <summary>Combined gain reduction / attenuation currently applied to music (0 = no reduction, 1 = full silence).</summary>
    public float GainReduction => 1f - EffectiveRatio;

    /// <summary>Alias for <see cref="GainReduction"/>.</summary>
    public float Attenuation => GainReduction;

    /// <summary>Read-only snapshot of every registered source for diagnostics (live per-source ratios).</summary>
    public IReadOnlyList<DuckSourceView> Sources =>
        _sources.Select(kv => new DuckSourceView(
            kv.Key, kv.Value.IsActive, kv.Value.CurrentRatio,
            kv.Value.IsActive ? kv.Value.Config.Ratio : 1f)).ToList();

    /// <summary>Registers (or replaces) the configuration for a named source.</summary>
    public void SetSource(string name, DuckSourceConfig config) =>
        _sources[name] = new DuckSourceState(config);

    /// <summary>
    /// Re-applies a fresh config set (e.g. after a hot reload). Existing sources swap to the new
    /// parameters in place preserving their live active/pulse/current-ratio state so ducking
    /// stays continuous new sources are added, and removed ones dropped.
    /// </summary>
    public void ReloadSources(IEnumerable<KeyValuePair<string, DuckSourceConfig>> sources)
    {
        var byName = new Dictionary<string, DuckSourceConfig>();
        foreach (var (name, config) in sources)
            byName[name] = config;

        foreach (var (name, config) in byName)
        {
            if (_sources.TryGetValue(name, out var existing))
                existing.Apply(config);
            else
                _sources[name] = new DuckSourceState(config);
        }

        foreach (var name in _sources.Keys.Where(name => !byName.ContainsKey(name)).ToList())
            _sources.Remove(name);
    }

    /// <summary>Holds a source ducked on or off (e.g. dialogue open/close).</summary>
    public void Set(string name, bool active)
    {
        if (_sources.TryGetValue(name, out var source))
            source.IsActive = active;
    }

    /// <summary>Ducks for <paramref name="holdSeconds"/> (or the source's configured hold), then releases.</summary>
    public void Pulse(string name, float holdSeconds = -1f)
    {
        if (!_sources.TryGetValue(name, out var source)) return;

        var hold = holdSeconds >= 0f ? holdSeconds : source.Config.HoldSeconds;
        // A zero hold means "duck and release immediately", which is indistinguishable from doing
        // nothing at all once the frame's easing runs. Latching IsActive here instead would stick the
        // duck forever: Update only releases a source while PulseRemaining > 0, and a zero hold
        // leaves PulseRemaining at 0 from the first frame. So a zero-hold pulse is a no-op and an
        // explicit Set(name, true) hold is left untouched.
        if (hold <= 0f) return;

        source.IsActive = true;
        source.PulseRemaining = hold;
    }

    /// <summary>Immediately releases every duck source (no easing tail) - used on scene switches.</summary>
    public void ReleaseAll()
    {
        foreach (var source in _sources.Values)
        {
            source.IsActive = false;
            source.PulseRemaining = 0f;
            source.CurrentRatio = 1f;
        }
        EffectiveRatio = 1f;
    }

    /// <summary>True when the combined duck ratio has recovered to at least <paramref name="threshold"/>.</summary>
    public bool IsReady(float threshold = 0.99f) => EffectiveRatio >= threshold;

    /// <summary>Advances all sources; call once per frame with the frame delta.</summary>
    public void Update(float deltaSeconds)
    {
        var ratio = 1f;
        foreach (var source in _sources.Values)
        {
            source.Update(deltaSeconds);
            ratio *= source.CurrentRatio;
        }
        EffectiveRatio = ratio;
    }

    private sealed class DuckSourceState(DuckSourceConfig config)
    {
        // Easing converges asymptotically, so an exact-equality settle check would never fire and
        // the source would ease forever. Snap once within this (inaudible ~0.0009 dB) epsilon.
        private const float SettleEpsilon = 0.0001f;

        public DuckSourceConfig Config { get; private set; } = config;
        public bool IsActive;
        public float PulseRemaining;
        public float CurrentRatio = 1f;

        /// <summary>Replaces the config in place, keeping active/pulse/current-ratio state.</summary>
        public void Apply(DuckSourceConfig config) => Config = config;

        public void Update(float deltaSeconds)
        {
            if (PulseRemaining > 0f)
            {
                PulseRemaining -= deltaSeconds;
                if (PulseRemaining <= 0f)
                    IsActive = false;
            }

            var target = IsActive ? Config.Ratio : 1f;
            if (MathF.Abs(CurrentRatio - target) < SettleEpsilon)
            {
                CurrentRatio = target;
                return;
            }

            var tau = target < CurrentRatio ? Config.AttackSeconds : Config.ReleaseSeconds;
            CurrentRatio = tau > 0f
                ? CurrentRatio + (target - CurrentRatio) * (1f - MathF.Exp(-deltaSeconds / tau))
                : target;
        }
    }
}

/// <summary>Diagnostics view of one duck source (target = the ratio it is easing toward).</summary>
public readonly record struct DuckSourceView(string Name, bool IsActive, float CurrentRatio, float TargetRatio);
