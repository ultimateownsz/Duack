# Slice C — user buses — STATUS

Started: Nov 22, 2026.

## Decisions

- Gate 2 resolved (Nov 22, 2026): the maintainer adopted the SHARED default routing —
  a per-cue bus argument with an optional directory convention, contexts stay
  music-only. Recorded as a **revisitable default**, not a validated choice: if the
  Gate 0 debrief shows the target user routed differently, the routing surface is
  revised under preview rules.
- Slice B will follow this one; slice D stays dormant; slice A is implemented on
  `feat/voice-concurrency` awaiting the human listening check (unchanged).

## Open questions

- Whether the Ambience sample lands in `Duack.Sample.Desktop` or a new sample
  project (default: extend the existing storyline sample unless it fights the
  regression standard).
- Player-settings store stays per plan: no per-bus player volumes (music/SFX pair
  only) unless the debrief says otherwise.
