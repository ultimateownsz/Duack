# Slice E — named mix states — decisions made while building

Populated as implementation decisions land. ADR deviations belong in
[STATUS.md](STATUS.md) with rationale, never here.

- **D1 — Standalone `MixStateManager`, not a manager surface.** `ApplyState`/`TransitionTo`
  live on a new component constructed with the authored state table and every runtime
  `BusMixer` (`MusicManager.Buses`, added additive in this slice, mirrors `SfxManager.Buses`),
  rather than on `MusicManager`/`SfxManager` or a `DuackAudio` facade. Rationale: the state is
  a mix-wide concern crossing both managers, the constructor-free shape keeps the existing
  constructor surface frozen (the slice-F non-goal pattern), and the manager that advances
  it is the same per-frame `Update(dt)` loop the game already owns.
- **D2 — A state writes both mixers, every write reaches all of them.** Slice-F D13 keeps two
  `BusMixer` states seeded from one table; a state applied to one mixer would move music but
  not SFX. The transition's starting values are captured PER MIXER — a real consumer (the
  dogfooding game) diverges the mixers on purpose by pushing the player's volumes onto one
  mixer each, so each mixer's ease must start from its own current volume rather than
  re-converging through the first mixer's.
- **D3 — Duration-mapped one-pole τ.** A timed transition runs the §12 formula
  `g(t) = target + (start − target)·e^(−t/τ)` with `τ = duration / ln(1/ε)`,
  ε = 1e-4 (the duck source's settle epsilon), so the ease settles exactly at its stated
  duration and the elapsed/duration telemetry is honest. The value is evaluated in absolute
  closed form from the captured start (never incrementally), so repeated `Update` steps
  compose to the same curve with no drift. Revisitable as taste until the Gate 0 debrief.
- **D4 — A state is a target vector, not a snapshot.** Buses a state does not name keep their
  current volume. Pinned by test; it is what keeps states composable with per-bus hot updates
  and with player settings overlays (which clone `MixStates` rather than reference them).
- **D5 — Unknown states report loudly, never fall back.** `ApplyState`/`TransitionTo` return
  false and raise `UnknownState` with the name, touching nothing — the `BusFellBack`/
  no-silent-failure principle applied to states; falling back to a "nearest" state silently
  would be a routing bug the mix never surfaces.
- **D6 — Transitions write per-frame `SetBus` through the existing authoring surface.** No new
  storage in mixers, no transition state in buses: the state's eased value rides the same
  `BusMixer.SetBus` path a hot update uses, so `Changed` fires and the mixers' readback stays
  the one truth. Composition order (state target = bus volume, duck read live on top, player
  sliders layer over) is pinned by test against the resolver.
