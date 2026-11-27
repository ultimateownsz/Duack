using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// One named mix state: a vector of authored per-bus volume targets (a "combat" state, a
/// "low-health" state) resolved over the same flat bus table every play resolves through.
/// Buses the state does not name keep their current volume — a state is a target vector,
/// never a snapshot of the whole table. Authored in code via
/// <see cref="AudioConfigBuilder.WithMixState(string, Action{MixStateBuilder})"/>; the
/// authored file is the snapshot (no runtime capture, per ADR-10).
/// </summary>
public class MixStateConfig
{
    /// <summary>
    /// Per-bus volume targets in [0, 1], keyed by bus name (lowercase,
    /// <see cref="StringComparer.Ordinal"/>); clamped by <see cref="AudioConfig.Sanitize"/>.
    /// </summary>
    public Dictionary<string, float> Buses { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Fluent authoring of one state's target vector:
/// <c>WithMixState("combat", s => s.Bus("music", 0.4f).Bus("ambience", 1.0f))</c>.
/// </summary>
public sealed class MixStateBuilder
{
    private readonly MixStateConfig _state = new();

    /// <summary>Sets (or replaces) the target volume of one bus for the state being authored.</summary>
    public MixStateBuilder Bus(string name, float volume)
    {
        _state.Buses[name] = volume;
        return this;
    }

    internal MixStateConfig Build() => _state;
}
