# Duack vNext — Voice concurrency — status

| Slice | Status | Notes |
| --- | --- | --- |
| A — Voice concurrency | awaiting human listening (unmerged) | Implementation lives on `feat/voice-concurrency` (pushed at `29aa0c6b`), not merged into `master` yet. Core, tests, CHANGELOG, skill, and a dedicated `Duack.Sample.Concurrency` example exist on that branch; storyline samples untouched; full suite green there. Admission limiting is already cross-platform. Cross-platform follow-up documented in [`PLAN.md`](PLAN.md) addendum (web `Update(dt)` wiring, audible-stealing seam, verification in the existing web sample). Human listening check pending. |

Verified on `feat/voice-concurrency`: `dotnet build Duack.slnx` clean; `dotnet test Duack.slnx`
green (392 tests). Commits on that branch: `4ae8db3` (core), `5a90bef` (example),
`fa94769` (docs). None of this work is on `master` until that branch is merged.

## Open questions for the maintainer

1. Human listening check for `Duack.Sample.Concurrency` (T key, 50 cues, cap 8, counter on screen).
2. Cross-platform concurrency follow-up (recorded, not scheduled): should the audible-stealing seam
   be a vNext slice of its own, or land as a small additive change after Slice A is accepted? The
   limiter needs no change; the work is web `SfxManager.Update(dt)` wiring, an optional stop/fade
   handle seam, and verification in the existing web sample — no new web example.

## ADR deviations

None. The voice-lifetime decision (authored duration aged by `Update(unscaledDt)`) is recorded in
[`slice.md`](slice.md); no ADR addresses voice lifetime, so there is nothing to deviate from.
