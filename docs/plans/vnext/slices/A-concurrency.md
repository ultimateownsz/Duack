# Slice A — Voice concurrency limiting

Canonical spec: `docs/plans/vnext/PLAN.md` §4. No gate (v2).

## Scope

- Global cap and per-cue caps in config (`AudioConfigBuilder.WithVoiceLimit(...)`, per-cue override).
- Stealing policy configurable per cue — oldest, quietest, priority (reject new below priority). Default: oldest.
- SFX voices only; music already capped at 2 by crossfade design.
- A refused or stolen play returns a result the game can read; no silent drops.

Non-goals: no backend changes, no platform voice limits, no async loading changes.

## Acceptance

- Unit tests: caps enforced, policy selects the predicted victim at exact boundaries, per-cue overrides win over global, results reported correctly.
- Sample: dedicated `samples/Duack.Sample.Concurrency` example — 50 overlapping cues, global cap 8,
  on-screen active-voice counter. The existing storyline samples stay untouched.
- Definition of done: tests green in CI, sample verified by a human listener, CHANGELOG entry,
  `duack-skills/concurrency/SKILL.md` in the existing skill style.

## Decisions made while building

- No backend changes (plan non-goal). Backends expose no voice-end event, so voice lifetime is
  **authored** (`DefaultVoiceSeconds`, default 0.5 s; per-cue `DurationSeconds` override) and voices
  are aged by `VoiceLimiter.Update(dt)`. Recorded here as a documented limitation, not an ADR deviation.
- `SfxManager.Play` keeps its `bool` return for source compatibility; the detailed outcome
  (including the stolen voice's sequence) is additive via a result-returning API.

## Cross-platform follow-up (2026-11-20, recorded in PLAN.md §4 addendum)

The limiter is core and already works on both heads; the outstanding work is web-specific wiring
and making "stealing" audible rather than bookkeeping-only:

- [ ] Web sample: author the limits and call `SfxManager.Update(unscaledDt)` every frame — the web
      head currently never ages voices, so a burst would saturate the cap permanently.
- [ ] Add an optional, additive voice-control seam (handle → stop/fade) so a stolen voice is
      actually silenced: desktop maps to `IAudioInstance`; KNI needs a JS bridge method beyond
      `StopAll()`. `ISfxBackend.Play` stays unchanged; fall back to slot-only stealing when absent.
- [ ] Optional, later: report real source end (`onended`) through the bridge to release slots at
      the true lifetime instead of the authored one; `[Experimental]` per §13.
- [ ] Wire the limits into the existing `Duack.Sample.Web` head and browser-listen there with a
      user-gesture start (worst-moment standard). No additional web sample project.

## Work list (non-exhaustive)

- [x] Pure core `VoiceLimiter` + config (red test written 2026-11-19; green confirmed).
- [x] `AudioConfigBuilder.WithVoiceLimit(...)` and per-cue overrides wired into `SfxConfig`.
- [x] `SfxManager` consults the limiter; result observable; `Update(dt)` ages voices.
- [x] Unit tests green in CodeOnly and JsonRoundTrip; full suite regression (392 tests across the solution).
- [x] Dedicated `Duack.Sample.Concurrency` example (T key, 50 cues, cap 8) with active-voice counter.
- [x] CHANGELOG entry + `duack-skills/concurrency/SKILL.md`.
- [ ] Human listening check (awaiting maintainer).
