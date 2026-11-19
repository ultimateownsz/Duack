# Duack — Plan Index

Table of contents for multi-phase work. One-off changes do not need a plan
document — just do the work.

Layout:

    plan/
      plan.md                        <- this file (index, never content)
      <issue#>-<slug>/
        phase-01-<slug>.md
        ...

Rules:

- plan.md is the index, never the content. One row per phase, linking to
  the phase doc.
- One document per phase, self-contained: problem, resolution, steps as
  checkboxes. No reader should need the GitHub issue open to work the phase.
- Phase docs are living: check boxes off as work lands; discovered work
  becomes new checkboxes, never silent expansion.
- Write the next phase doc when the previous phase is stable, not up front.
- Status: Not started / In progress / Blocked / Landed. Update the row here
  when it changes.
- When an initiative completes, keep the folder — it is the record of why
  the design is what it is.

## Initiatives

(none yet)
