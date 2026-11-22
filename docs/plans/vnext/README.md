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
| A — Voice concurrency | [voice-concurrency/](voice-concurrency/) | awaiting human listening (unmerged) | Implementation lives on `feat/voice-concurrency` (pushed at `29aa0c6b`), not merged into `master` yet. Core, tests, CHANGELOG, skill, and a dedicated `Duack.Sample.Concurrency` example exist on that branch; full suite green there. Admission limiting is already cross-platform. Cross-platform follow-up documented in the slice PLAN addendum (web `Update(dt)` wiring, audible-stealing seam, verification in the existing web sample). Human listening check pending. |
| B — 2D positional SFX | [2d-spatial/](2d-spatial/) | draft | No gate; debrief-tunable curves. |
| C — User-defined buses | [user-buses/](user-buses/) | blocked | Gate 2 routing decision; slip-rule default after 2026-12-01. |
| D — Build-time effects | [baked-effects/](baked-effects/) | blocked | Dormant until positive evidence of a runtime-color need; Gate 1 spike may run standalone. |
| E — Named mix states | [mix-states/](mix-states/) | draft | After C (needs bus tree and resolution path); authored config only. |

Initiative-wide open questions are the gates in [`SHARED.md`](SHARED.md). Slice-specific state and
open questions live in each started slice's `STATUS.md`. ADR deviations are recorded in the
affected slice's `STATUS.md` with rationale, never silently applied.
