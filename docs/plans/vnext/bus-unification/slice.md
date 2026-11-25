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
- **D11 — Runtime seed stays behavior-identical until work item 3**: `SfxManager` projected
  only user buses (filtering the now-present `master`/`music`/`sfx`) into the unchanged
  `BusMixer` seed; `MusicManager.Compose` read the `music` table entry with a
  standalone-`MusicConfig` fallback of 1. Mechanical, zero audible change; superseded by
  D12-D17.
### Resolver decisions (work item 3, `a2ff83e` + `0e63d08`)

- **D12 — `BusMixer` seeds the whole table.** Ctor re-typed to
  `IEnumerable<KeyValuePair<string, BusConfig>>?`; master/music/sfx always exist (missing
  entries start at full volume; table entries override; user buses alongside), so a
  table-less `SfxConfig` can no longer turn `Resolve("master")` into a silent sfx fallback.
- **D13 — Two mixer states, one table of volumes** (deviation from a literal "one resolver"
  reading): `SfxManager` and `MusicManager` each own a `BusMixer` seeded from the same config
  table, because D7 freezes every constructor and there is no channel between managers.
  Changelogged consequences: per-bus mute/pause is per-manager, volumes snapshot at
  construction, and a `SetBus` on one manager's mixer does not move the other's.
- **D14 — Manager volume properties are thin delegating writers.**
  `SfxManager.MasterVolume`/`SfxBusVolume` and `MusicManager.MasterVolume` get/set the mixer's
  master/sfx entries (`BusMixer.VolumeOf` is the new tiny read-side surface). Ctor float
  params still win over config at construction — pre-existing behavior, kept.
- **D15 — `BusView` gains `DuckRatio`** (appended; compile-breaking, changelogged).
  `BusMixer` takes an optional `Func<string,float>? duckRatioFor` (injected by `SfxManager`),
  read live at snapshot — D4 holds: duck never embeds in bus state. A mixer without the
  function reports 1 per bus. `MusicManager.DuckRatioFor` stays as a delegating wrapper.
- **D16 — The `ZeroEffectiveGain` fixed-path pin was already green** under slice C (the mixer
  zero-folded `Resolve(null)`), so the "new" pin is an honest regression pin, not a red.
- **D17 — Fold split across two commits.** (a) behavior-silent unification, (b) the audible
  fold (duck exemption for `bus is null` removed: `* _music.DuckRatioFor(resolution.Bus)`
  always) — each breaking surface has its own changelog entry and revert unit.

Deviations from PLAN wording, all changelogged: "one resolver" landed as two mixer instances
over one table (D13, forced by D7); `BusMixer` ctor signature and `BusView` shape changed
(PLAN said "public surface unchanged") — forced by whole-table seeding and the readback field.
