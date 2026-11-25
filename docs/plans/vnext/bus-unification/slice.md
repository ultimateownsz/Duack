# Slice F — bus unification — decisions made while building

Populated as implementation decisions land. ADR deviations belong in
[STATUS.md](STATUS.md) with rationale, never here.

### Structure decisions (Nov 24, 2026, before any code — inherited, binding)

- **D1–D5 of slice C stand** (user-buses/slice.md): flat buses only, lowercase Ordinal names,
  `sfx` fixed, `Changed`/methods as the mutation surface, live per-play resolution, duck
  outside bus state. This slice adds no parent, solo, snapshot, effect, or resolve-once surface.
- **D6 — Config-level wrapper compatibility.** `WithMasterVolume`,
  `WithMusicMasterVolume`, `WithSfxMasterVolume`, and `WithBus` keep their exact signatures and
  write the one flat `BusConfig` table; the wrapper-equivalence tests are the regression
  contract and are written red first.
- **D7 — Runtime constructor signatures stay** (issue #36 non-goal): the bus table reaches the
  managers through the existing `SfxConfig` transport; no `SfxManager`/`MusicManager`
  constructor parameter is added, removed, or re-typed.

### Working decisions

- **D8 — One table, same-instance transport** (work item 2, `c45e39c`). Physical storage is
  `AudioConfig.Buses: Dictionary<string, BusConfig>` (Ordinal, lowercase); `SfxConfig.Buses`
  and `MusicConfig.Buses` are nullable *references* to that same instance wired by
  `AudioConfig`'s setters/constructor, so replacing a section via object initializer cannot
  fork storage (pinned: `Assert.Same(built.Buses, built.Sfx.Buses)`). Chosen over keeping an
  independent `SfxConfig` dictionary: one truth, while D7's "existing transport" still holds.
- **D9 — Pre-registration lives in `AudioConfig.CreateDefaultBuses()`, not the builder**
  (deviation from the issue's "builder pre-registers" wording): hand-composed graphs
  (`PlayerSettings.ApplyOverrides`, object initializers) must carry `master`/`music`/`sfx`
  at full volume too; builder-only seeding would leave every hand-built runtime config without
  the fixed three. Names exposed as `BusNames.Master/Music/Sfx`.
- **D10 — Dedicated volume fields removed, not delegated** (`AudioConfig.MasterVolume`,
  `MusicConfig.MasterVolume`, `SfxConfig.MasterVolume` gone; preview-breaking): a delegating
  keep-alive property was considered and rejected — it would hide the one-truth change and
  leave dead surface for work item 3 to remove anyway. Called out in the changelog
  (`## Unreleased → Changed → #### 2026-11-25`) with the compile symptoms (CS0117/CS1061/CS0029/CS1503).
- **D11 — Runtime seed stays behavior-identical until work item 3**: `SfxManager` projects
  only user buses (filtering the now-present `master`/`music`/`sfx`) into the unchanged
  `BusMixer` seed; `MusicManager.Compose` reads the `music` table entry with a
  standalone-`MusicConfig` fallback of 1. All mechanical, zero audible change; the unified
  resolver replaces the projection in work item 3.
