# Duack vNext — Slice F — Bus unification

Index: `../README.md` · Shared evidence/ADRs/gates/math: `../SHARED.md`

`STATUS.md` (state and open questions) and `slice.md` (decisions) are added when this slice starts.

Tracking issue: #36. Additional slice on top of slice C (#31) — not a replacement: user-defined buses, their composition order, and all slice C public surface stay.

---

Evidence: recorded defects from the slice C qa verdict in `../user-buses/slice.md` — the "single resolver readback" is split across two surfaces (`BusMixer.Views` vs `MusicManager.DuckRatioFor`), and a duck source targeting the literal `"sfx"` bus affects only bus-routed plays. The parent-chain version is rejected scope under `../user-buses/review-structure.md` §4 (D1: flat buses only; a hierarchy is a mixer graph the README scope promise excludes). This slice is the flat table subset: review-structure.md ruled on hierarchy, solo, snapshots, effects, naming, and mutation policy — it never evaluated a flat unification.

Scope:
- Config: one flat `BusConfig { Volume }` table in `AudioConfig` replaces the three dedicated fields (`AudioConfig.MasterVolume`, `MusicConfig.MasterVolume`, `SfxConfig.MasterVolume`) and the side dictionary (`SfxConfig.Buses`); `AudioConfigBuilder` pre-registers `master`/`music`/`sfx`; `WithBus(name, defaultVolume)` keeps its signature; `WithMasterVolume`/`WithMusicMasterVolume`/`WithSfxMasterVolume` stay as thin table writes (existing code compiles unchanged). C# authoring only (ADR-4). `Sanitize` clamps volumes to [0,1] as today; no cycle validation (no parents exist).
- Runtime: one resolver for every bus including the fixed three, replacing the parallel fixed-field math in `SfxManager`/`MusicManager`. `BusMixer` public surface unchanged: `SetBus`, `SetMute`, `SetPaused`, `MasterMuted`, `Views`, `Changed`, `BusFellBack`, `ZeroEffectiveGain` — including the W1 same-value guard and the W2 create-on-`SetBus` asymmetry pinned in `../user-buses/review-structure.md` §4.
- Composition fold (the one behavior change, Gate D below): the fixed SFX path joins the bus-duck composition, so a duck source targeting `"sfx"` scales the fixed path too. CHANGELOG "preview breaking" note per SHARED.md §9; pinned both ways in tests.
- Readback unification: per-bus effective gain and duck contribution on one surface (`Views` gains the duck-contribution field); `MusicManager.DuckRatioFor` becomes a delegating wrapper so callers compile. Closes the slice C PLAN scope item "per-bus readback of the stages".
- Backends: WebAudio head uses one gain node per resolved bus including the fixed three (no ad-hoc volume math); MonoGame head is arithmetic-only and unaffected beyond the resolver (`Volume`/`Pitch`/`Pan` only — SHARED.md §0, no adapter contract change).
- Routing unchanged: the Gate 2 outcome stands (per-cue bus argument, optional directory convention, contexts stay music-only), a revisitable default per `../user-buses/STATUS.md`.
- Docs: `duack-skills/buses/SKILL.md` rewritten around the flat table (mechanism chain, pre-registered entries, the `"sfx"` fold, unified readback, pitfalls reword, frontmatter extended), README feature table row, CHANGELOG under `## Unreleased`. Docs ride in the same PR as the behavior change (SHARED.md §8).

Non-goals: no parent chains, no bus effects, no solo, no snapshots, no CamelCase names, no public mutable bus fields, no resolve-once-at-play (D1–D5 binding, `../user-buses/slice.md`); no per-bus player-settings volumes (the music/SFX slider pair stays user-facing); no constructor signature changes; no per-cue routing model changes. If a real game demonstrates a need for nested attenuation, open a fresh proposal against `../user-buses/review-structure.md` §2 with that evidence — per the D1 re-propose rule.

Gates (slice-local; the initiative-level gates in SHARED.md §3 are untouched):
- **Gate M (music representation):** `music` becomes a table entry for storage uniformity; contexts/crossfades stay music-only in behavior. Nothing user-facing moves.
- **Gate D (duck-fold default):** folding the fixed SFX path into bus-duck composition changes audible behavior for existing configs whose duck sources named `"sfx"` while routing nothing to buses. Default: fold (one semantic, one path), recorded loudly in the changelog per SHARED.md §9 and pinned both ways in tests. Resolves by listening test on both backends (SHARED.md §8: worst moment, not the average).

Acceptance:
- Unit tests: wrapper equivalence (thin wrappers write the same values the dedicated fields carried — this is the regression contract, written first); pre-registration of `master`/`music`/`sfx` as `BusConfig` entries indistinguishable from user buses; every slice C pin migrated with equivalent coverage (composition order, `BusFellBack`, `ZeroEffectiveGain`, W1, W2, ref-counted holds, `DuckSourceConfig.Target`); the `"sfx"` duck fold pinned both old-semantics-excluded and new-semantics-included.
- Readback: `Views` carries per-bus effective gain and duck contribution for every bus including the fixed three; `DuckRatioFor` delegates.
- WebAudio: one gain node per resolved bus, no ad-hoc volume math for fixed buses; the same manifest and config drive both heads.
- Sample: an authored user bus and the fixed `sfx` bus through the same mixer UI path, gated on the maintainer's call that bus-targeted ducks stay out of the sample until a real user proves the scenario (`../user-buses/slice.md`) — the `"sfx"` fold is pinned in tests and listening either way.
- Docs: skills doc, README row, CHANGELOG `## Unreleased`.
- Human listening on both backends: desk and web mixes unchanged for existing configs; the Gate D fold verified on both.

Definition of done: tests green (whole suite, both CodeOnly and JsonRoundTrip), wrapper-equivalence pins green, sample regression unchanged, both backends pass human listening on the Gate D fold, changelog "preview breaking" entry in, checkbox for this slice ticked in the same commit as the work, every implementing commit carrying `Refs: #36`.
