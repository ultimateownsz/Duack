# Duack vNext — Voice concurrency — status

| Slice | Status | Notes |
| --- | --- | --- |
| A — Voice concurrency | awaiting human listening, both heads (unmerged) | Implementation lives on `feat/voice-concurrency`, rebased onto the bus-unified `master` (2026-11-25, #37) with the conflicts resolved: bus routing composes the final volume, then the zero-gain short-circuit (no slot consumed), then limiter admission. Core, tests, CHANGELOG, skill, and `Duack.Sample.Concurrency` are joined by the cross-platform follow-up from the PLAN addendum: audible stealing via the additive `ISfxVoiceControlBackend` seam (desktop = instance handles, web = JS source ids via `playSourceTracked`/`stopSource`; slot-only fallback without the seam), and the web head (`Duack.Sample.Web`) now authors the limits and calls `SfxManager.Update(unscaledDt)` every frame. |

Verified on the rebased `feat/voice-concurrency`: `dotnet build Duack.slnx` clean;
`dotnet test Duack.slnx` green (450 tests: 348 CodeOnly + 38 adapters + 21 sample + 43 tools);
`python tools/plan_check.py` → `pass`; `samples/Duack.Sample.Web` builds standalone with the same
changes. QA verdict for 2026-11-25: **good** (see `docs/plans/vnext/REVIEWS.md`).

Commits (rebased): `06a67fa` (core), `70c34ca` (example), `5894dec` (docs) plus this session's
rebase resolution, stale-doc removal, override-pin test, and `912c766` (audible stealing + web
wiring). None of this is on `master` until the branch merges.

## Open questions for the maintainer

1. Human listening check for `Duack.Sample.Concurrency` (T key, 50 cues, cap 8, counter on
   screen) — now also judging that a burst's older voices actually stop (audible stealing) on
   desktop.
2. Browser check of the existing `Duack.Sample.Web` head with the limits wired (user-gesture
   start, worst-moment standard, judging only the worst moment per the plan convention). The
   headless pins cover the bridge calls, not the JS audio graph.
3. `PlayDetailed` collapses a zero-gain play into `Played` (inherited master semantics; the
   `ZeroEffectiveGain` event is the report channel). A distinct outcome member would be more
   honest in the detailed API — the maintainer may want that as a small follow-up.

## ADR deviations

None. The voice-lifetime decision (authored duration aged by `Update(unscaledDt)`) is recorded in
[`slice.md`](slice.md); no ADR addresses voice lifetime, so there is nothing to deviate from.
The voice-control seam follows the ADR-8 pattern: an additive, optional capability; the
observable config/result API is identical across backends, only the acoustic result differs.
