---
name: planner
description: Operate the Duack audio library repository's ICM planning
  workspace: orient from plan/plan.md, triage incoming work against the
  threshold gate, start and run multi-phase initiatives, record
  decisions and gotchas with stable IDs, keep the pinned metric honest,
  run experiments on exp/ branches, maintain the deferred register, and
  pass the walk test. Load for any Duack task involving planning,
  initiatives, phases, phase docs, checkboxes, blocked work, feature
  triage, deferred ideas, or experiments. If plan/plan.md is missing,
  the workspace is not installed: say so and stop.
license: MIT
---

# Planner

Operates the planning workspace of the Duack repository, built on the
primitives of Interpretable Context Methodology (ICM: sequence, shelf,
catalog; folder structure as agent architecture, plain files as state).
Durable state is always a file write. Never keep planning state in
agent memory or conversation. If this skill and the repository's
planning files disagree, the repository's files win.

## Orient (start of every session)

1. Read the planning section of `AGENTS.md` at the repository root.
2. Read `plan/plan.md`. From the index alone, state the active
   initiatives, the next phase of each, and the blocked rows with
   their blockers.
3. Read `overview.md` of the initiative you will touch, then only its
   current phase doc.
4. Load nothing else. `design/deferred.md` only when evaluating a
   proposal; `CHANGELOG.md` only for release questions.

## Triage incoming work (the threshold gate)

In order:

- Finishes in one sitting or one PR? Do it now, or one line in
  `design/TODOS.md`. No plan folder.
- A feature idea with no real issue behind it? Check
  `design/deferred.md` first: if the idea is present, check its
  reopening condition before re-proposing; if absent, add one entry
  with a concrete reopening condition and close any filed issue with a
  pointer to the entry.
- A feasibility question ("would this even work here?")? Open an
  experiment doc per `design/experiments/README.md`, with the question
  stated as yes/no, measurable criteria, and an effort-only time box.
  Code lives on an `exp/<slug>` branch and is never merged as-is. End
  Adopted, Parked, or Rejected, with evidence in the doc.
- Needs sequencing across independently shippable chunks? Start a plan
  initiative, below.

Never inline phase content into `plan.md`. Never widen existing
checkboxes quietly: unplanned work becomes a new box or a new phase.

## Start an initiative

1. Create `plan/<NN>-<slug>/` with the next free id; copy
   `plan/_templates/overview.md` into it. Fill in the problem from the
   user's perspective, the tracking issue number (or none), and the
   pinned metric with how a test or script enforces it. Reserve an ID
   block for the first phase.
2. Copy `plan/_templates/phase.md` to `phase-01-<slug>.md`, written
   just before the phase starts, never long before: early plans are
   fiction by the time they are read.
3. Add one index row per phase to `plan/plan.md`, status Not started.
4. One branch per phase, named after what it implements; a type prefix
   is decoration, never a rule. Commits follow Conventional Commits as
   stated in CONTRIBUTING.md.

## Work a phase

- Read only the index row, the overview, and the current phase doc.
- Move the index row to In progress before starting.
- Tick a checkbox in the same commit as the work it describes. If a
  state changed but no file changed, it did not happen.
- Discovered work becomes a new box or a new phase. A new phase gets
  its ID block reserved in the overview and its own index row.
- Every decision and gotcha gets the next free ID from the phase's
  reserved block, recorded as one line in the phase doc in the same
  commit. Cite IDs (D30), never "the decision in the other phase
  about X". Never renumber or reuse an ID. Cross-cutting caveats are
  stated once in the overview as C1, C2, and so on, and cited by the
  phases they affect.
- Record the phase's effect on the pinned metric when it lands,
  including regressions on the way. A metric that got worse before it
  got better is what makes it credible.
- Blocked: write the blocker into the index row.

## Land an initiative

1. In the commit that lands the final phase, set every row of the
   initiative to Landed. Landed folders are never deleted.
2. Fill the gap table in the overview: anything the initiative
   deliberately does not do, each with its stated reason. A stated
   reason ends the question; an unexplained gap invites it forever.
3. Append one epilogue paragraph to `plan/plan.md` below the
   initiative's rows: what the finished feature does from the user's
   perspective. It is the only content the index ever holds.
4. If the initiative came from an experiment or a deferred entry, the
   register entry is replaced by a line pointing at the plan folder.

## Issues are an inbox

GitHub issues are not state. Status and progress never live in issue
comments; if it matters for orientation, it is a file write. Wishlist
issues do not stay open: register the idea and close the issue with a
pointer, per the triage rules.

## Walk test (end of any planning session)

State, from the files alone: what initiatives are active, which phase is
next, what is blocked and why, and which checkboxes in the next phase
are still open. If any answer needed conversation history, correct the
files, not the explanation. Run the scripted portion as well:
`python tools/plan_check.py` (or `py tools/plan_check.py` where that is
the name) must print `pass`.
