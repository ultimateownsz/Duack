# Duack vNext — Slice E — Named mix states (added Nov 19, 2026, from the Voltage gap analysis)

Index: `../README.md` · Shared evidence/ADRs/gates/math: `../SHARED.md`

`STATUS.md` (state and open questions) and `slice.md` (decisions) are added when this slice starts.

---

Evidence: the gap analysis ranks this the single highest-value missing mix feature for Duack. It is the standard game pattern: peace, combat, and low-health are different mix identities over the same buses. Voltage's version captures live runtime state; per ADR-10, Duack's states are authored config instead.

Scope:
- `AudioConfigBuilder.WithMixState(name, s => s.Bus("music", 0.4f).Bus("ambience", 1.0f)...)`: named states as authored per-bus target vectors. No capture of runtime gains, no snapshot serialization — the authored file is the snapshot.
- `ApplyState(name)` instant; `TransitionTo(name, seconds)` timed, with the one-pole smoothing of §12 over the target vector; `IsTransitioning`, elapsed/duration, from/to telemetry matching the existing crossfade telemetry pattern.
- States compose with the slice C resolution path: state targets are bus volume inputs to the same single effective-gain resolver; player settings still layer on top; duck contribution stays live on top of the state.
- Mix states also target duck source intensities later (design note only — first version targets bus volumes).

Non-goals: zone/trigger components (engine state; duack-skills and samples instead), capture of runtime volumes, per-region rich config (deferred per ADR-11 until a consumer wiring story exists).

Acceptance:
- Unit tests: target resolution, monotone interpolation, telemetry correctness, composition order (state → duck → player settings) pinned against the §12 formulas.
- Sample: walking between a calm field and a combat area transitions the mix state while music crossfades, on both backends; human listening, worst-moment standard.

Definition of done: tests green, sample verified on both backends, CHANGELOG, skills doc, one sentence in the spatial and duck skills warning against scaled dt (gap analysis finding: fades and ducks must use unscaled time).
