# Duack vNext — routing entry

This initiative's canonical plan and durable state live in `docs/plans/vnext/`:

- `README.md` — the index: one row per slice with status and links.
- `SHARED.md` — evidence base, ADRs, gates, verification standards, guardrails, sequencing, math,
  and the Voltage gap intake. The workspace convention is authoritative for this initiative.
- `<slice>/PLAN.md` — the spec for a started slice (plus its dated addenda).
- `<slice>/STATUS.md` — state and open questions for that slice.
- `<slice>/slice.md` — decisions made while building that slice.

Phases:

- phase-01: D1 to D19, G1 to G19

Start at the index. Every slice's folder holds its `PLAN.md` from the moment the plan is split;
`STATUS.md` and `slice.md` are added when the slice starts. The plan is tracked only when a new
slice is created that references it — until then these files are uncommitted working state.
