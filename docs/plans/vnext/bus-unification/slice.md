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

(populated as the refactoring lands them)
