# Slice F — bus unification — STATUS

Started: Nov 24, 2026. Tracking issue: #36.

## Decisions

- Design latitude: issue #36 sketches the API shape ("buses[\"master\"] = new BusConfig{...}");
  the maintainer (Nov 24, 2026) authorized the refactoring role to land a better shape where
  the code demands it, recording any deviation from the issue's sketch as a numbered decision
  in slice.md. The defect targets (split readback, fixed-path duck exclusion) are not
  negotiable; the mechanism is.
- Constructor signatures of `SfxManager`/`MusicManager` stay (issue #36 non-goals): the bus
  table reaches the runtime through the existing `SfxConfig` transport.

## Open questions

- Gate M: nothing user-facing moves; `music` is a table entry for storage uniformity only.
- Gate D: the `"sfx"` duck-fold default (fold) is implemented and pinned both ways in tests;
  it resolves only by human listening on both backends — the slice box cannot tick before it.
- Bus-targeted ducks stay out of the sample until a real project proves the scenario
  (maintainer call, Nov 22, 2026, user-buses/slice.md) — unchanged by this slice.

## Known limitations

- Per-bus active-voice counts remain unexposed (`ISfxBackend` completion telemetry gap,
  user-buses/STATUS.md) — unchanged by this slice.
