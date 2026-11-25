# Phase 01: vNext slices

- Problem: the vNext feature set (voice concurrency, 2D spatial SFX, user buses,
  named mix states, baked effects) arrived as one monolithic plan that a fresh
  agent could not execute slice by slice without losing state between sessions.
- Shape: the plan lives in `docs/plans/vnext/` in the per-slice workspace shape
  (index `README.md`, shared `SHARED.md`, one `<slug>/PLAN.md` per slice, plus
  `STATUS.md`/`slice.md` once a slice starts). This phase tracks the slices
  through that workspace; this file is the stage contract, the slice folders
  hold the detail.
- Caveats: none
- Done: every slice row in `docs/plans/vnext/README.md` reads Landed (or its
  scope was consciously closed in the slice's `STATUS.md`), and this index row
  says Landed in the same commit as the last slice's closure.

## Pinned metric

Before: not measured (preview library, no baseline instrumented). Target for
this phase: each slice ships with green unit tests for its pure logic and a
runnable sample for its audible behavior, per the verification standards in
`docs/plans/vnext/SHARED.md`. Record the actual effect here when the phase
lands.

## Work

The checkbox list is the phase's truth. It is non-exhaustive. Slice-level
state and open questions live in each slice's `STATUS.md`; the boxes here
track slice completion only.

- [ ] Slice A — voice concurrency (context: docs/plans/vnext/voice-concurrency/)
- [ ] Slice B — 2D positional SFX
- [x] Slice C — user-defined buses (#31)
- [ ] Slice E — named mix states
- [ ] Slice D — build-time effects experiment (dormant until Gate 0 evidence)
- [x] Slice F — bus unification (#36)

## Decisions and gotchas

Stable IDs from the range reserved for this phase in ../overview.md,
assigned in first-come order. The ID lands in both files in the same
commit as the work the decision describes. Note: vNext's architecture
decisions are numbered ADR-1..ADR-12 in `docs/plans/vnext/SHARED.md` and
keep those IDs; the D/G range here is reserved for phase-level decisions
that do not fit the ADR series.

- (none yet)
