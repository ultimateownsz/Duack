# Duack vNext — plan index

Shared evidence, ADRs, gates, verification standards, sequencing, and math live in
[`SHARED.md`](SHARED.md). Start there, then open the slice you are assigned.

Review verdicts that affected this workspace are recorded in [`REVIEWS.md`](REVIEWS.md).

Every slice has a folder holding its `PLAN.md` from the moment the plan is split; add
`STATUS.md` and `slice.md` only when that slice starts. Track this plan only when a new
slice is created that references it — until then these files are uncommitted working state.

## Slices

| Slice | Folder | Status | Notes |
| --- | --- | --- | --- |
| A — Voice concurrency | [voice-concurrency/](voice-concurrency/) | landed (Nov 25, 2026); acceptance remainder on #72 | Implementation landed on master via #40 (merged 2026-11-25): core limiter plus the cross-platform follow-up from #38 — audible stealing via the additive `ISfxVoiceControlBackend` seam (desktop instance handles, web JS source ids; slot-only fallback) and web-head voice limits aged by `SfxManager.Update(unscaledDt)`. QA verdict good (`REVIEWS.md`). Remaining: human listening on `Duack.Sample.Concurrency` (desktop, incl. audible stealing) and a browser check of the wired web head, tracked on [#72](https://github.com/ultimateownsz/Duack/issues/72); the `onended` exact-lifetime item stays `[Experimental]`. |
| B — 2D positional SFX | [2d-spatial/](2d-spatial/) | landed (Nov 25, 2026) | Implementation landed on master via #41 (merged 2026-11-25; the earlier `feat/spatial-sfx` branch stays set aside on the blocking qa verdict, REVIEWS.md 2026-11-25). Positional gain rides the unified bus chain; deltas in [2d-spatial/STATUS.md](2d-spatial/STATUS.md). Code-level acceptance remainder tracked on #44: the positional sample on both heads and human listening, judged by the worst moment.
| C — User-defined buses | [user-buses/](user-buses/) | landed (Nov 25, 2026, via #36) | Gate 2 resolved Nov 22, 2026: the maintainer adopted the default routing (per-cue bus argument, optional directory convention, contexts stay music-only) as a revisitable default. The routing surface and the unified bus table it resolves over landed via #36; see [user-buses/STATUS.md](user-buses/STATUS.md). The routing stays a revisitable default until the Gate 0 debrief. |
| D — Build-time effects | [baked-effects/](baked-effects/) | blocked | Dormant until positive evidence of a runtime-color need; Gate 1 spike may run standalone. |
| E — Named mix states | [mix-states/](mix-states/) | in progress; acceptance remainder on #73 | Authored config over the unified bus table (#46, closed; core landed via #47): `AudioConfigBuilder.WithMixState` authors named per-bus factors, `MixStateManager` applies them instantly (`ApplyState`) or eases them (`TransitionTo`, §12 one-pole, crossfade-style telemetry) as a multiplicative layer over the authored bus volumes (`effective = master × bus × state factor × duck × fade` — #68, in review as PR #69). Core unit-tested against the §12 formula; awaiting the two-backend sample and human listening, tracked on [#73](https://github.com/ultimateownsz/Duack/issues/73) — see [mix-states/STATUS.md](mix-states/STATUS.md). |
| F — Bus unification | [bus-unification/](bus-unification/) | landed (Nov 25, 2026) | Additional slice on top of C (#36): one flat `BusConfig` table for master/music/sfx and user buses, one resolver, unified readback, the `"sfx"` duck fold (preview breaking, changelogged), web per-bus gain nodes + startup warm-up. Human listening done over five rounds (no bleed, no truncation, warm start) - verdicts and decisions D6-D31 in `bus-unification/slice.md`, reviews in `REVIEWS.md`. |

Initiative-wide open questions are the gates in [`SHARED.md`](SHARED.md). Slice-specific state and
open questions live in each started slice's `STATUS.md`. ADR deviations are recorded in the
affected slice's `STATUS.md` with rationale, never silently applied.
