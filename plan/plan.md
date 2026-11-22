# Duack Plan Index

This folder exists for work that will not fit in one pull request: a
feature arrives in several shippable steps, or a bug sweep runs until a
class of problem is gone. Everything else (a fix, a doc edit, a sample)
skips the plan entirely. If you can finish it in a sitting, plan.md does
not want to hear about it.

Layout:

    plan/
      plan.md                        <- routing only; phase content
      _templates/                       lives one level down, one file
        overview.md                     per phase; new initiatives and
        phase.md                        phases start as template copies
      <NN>-<initiative-slug>/
        overview.md
        phase-01-<slug>.md
        phase-02-<slug>.md

How it is worked:

- plan.md routes. Each phase gets one row: what, where its doc is, and
  what state it is in. Anything longer than a row is telling a story
  that belongs in the phase doc.
- An initiative starts as a copy of `_templates/overview.md` with the
  next free `<NN>-` id: the problem from the user's perspective, the
  pinned metric, the decision/gotcha ID ranges, and the cross-cutting
  caveats. Every phase is a copy of `_templates/phase.md`, written just
  before the phase starts, never long before: early plans are fiction by
  the time they are read.
- The checkboxes are the truth. When work lands, its box is ticked in
  the same commit. Work nobody planned gets a new box or a new phase,
  never quiet widening of the ones that exist. Phase work lists are
  stated as non-exhaustive.
- Every checkbox names its issue, created before the work starts:
  `- [ ] <step> (#123)`. Implementation commits end in `Refs: #123`, so
  `git log --grep '#123'` lists exactly the commits behind that box — the
  set to review, document or revert when a change proves wrong. The issue
  holds the problem, acceptance criteria and repro; evidence goes in its
  comments. Status stays here.
- Decisions and gotchas keep stable IDs: `D<n>` and `G<n>`, ranges
  reserved per phase in overview.md. Cite D30; never cite "the decision
  in the other phase about X". An ID is never renumbered or reused.
- Each phase records its effect on the pinned metric in its phase doc,
  including phases that made it worse before it got better. The
  regressions are what make the metric credible.
- States are Not started, In progress, Blocked, Landed. A Blocked row
  states its blocker, because that is the row people scan first. Status
  changes are file writes, landed with the work they describe.
- Landed initiatives are not deleted. The folder stays as the answer to
  "why is this the way it is": every row showing Landed, the overview's
  gap table filled, and one epilogue paragraph appended to this index
  under the row block: what the finished feature does from the user's
  perspective. The epilogue is the only content the index ever holds.

## Initiatives

| Initiative | Phase | Doc | Status |
|---|---|---|---|
| (none yet) | | | |
