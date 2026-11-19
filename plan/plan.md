# Duack Plan Index

This folder exists for work that will not fit in one pull request: a
feature arrives in several shippable steps, or a bug sweep runs until a
class of problem is gone. Everything else (a fix, a doc edit, a sample)
skips the plan entirely. If you can finish it in a sitting, plan.md does
not want to hear about it.

Layout:

    plan/
      plan.md                    <- routing only; phase content lives one
      <slug>/                       level down, one file per phase
        phase-01-<slug>.md

How it is worked:

- plan.md routes. Each phase gets one line here: what, where its doc is,
  what state it is in. Anything longer than a line is telling a story that
  belongs in the phase doc.
- A phase doc is written just before the phase starts, never long before:
  early plans are fiction by the time they are read. It carries the
  problem being solved, the shape of the fix, and the work as checkboxes,
  so a session that has never seen this repo can pick the doc up and
  continue without asking anyone.
- The checkboxes are the truth. When work lands, its box is ticked in the
  same commit. When work appears that nobody planned, it gets a new box or
  a new phase, not absorbed quietly into the existing ones.
- States are Not started, In progress, Blocked, Landed. A Blocked phase
  states its blocker in the index line, because that is the row people
  scan first.
- Landed initiatives are not deleted. The folder stays as the answer to
  "why is this the way it is", with every row showing Landed. In-time
  summaries may be added to the index as a closing paragraph, the one
  exception to routing-only.

## Initiatives

(none yet)
