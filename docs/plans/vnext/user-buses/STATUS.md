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

- **Human listening pending** (the slice cannot tick its box without it): the demo wires the
  ambience bus into the shared storyline — the notice cue routes to `ambience`, and the M key
  (desktop) hot-mutes the bus while music and SFX-path cues continue. The bus-targeted
  dialogue-duck scenario was removed from the demo on the maintainer's PR-review call (above)
  and stays unvalidated until a real project proves it. Verify on desktop (and web, built
  explicitly) by the worst moment.
- Whether the Ambience sample truly needs a dedicated sample project (default applied: it is
  wired into the existing storyline of both heads, which the regression standard already
  tracks).
- Player-settings store stays per plan: no per-bus player volumes (music/SFX pair only) unless
  the debrief says otherwise.

## Known limitations

- Per-bus active-voice counts need backend completion telemetry the `ISfxBackend` seam does not
  expose (`Play` returns only whether the one-shot started); `BusMixer.Views` deliberately
  omits them. Revisit if per-playback SFX handles (recorded as a deferred experiment in
  SHARED.md §13) ever land.
