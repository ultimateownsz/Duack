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

- (none — closed Nov 25, 2026. Record of the closes:
  - Gate M held: `music` is a storage/resolution table entry only; contexts/crossfades stayed
    music-only through the whole slice (pinned, regression green).
  - Gate D resolved by the maintainer's listening (fold kept; five rounds covered the desk and
    web mixes unchanged, no cue bleed into any screen, no cue truncation, and the warm web
    start - confirmed "this fixes it"). Five rulings recorded as D27-D31 in slice.md; the
    audibles are the sample's; the fold's own semantics are pinned both ways in core tests.
  - The per-bus player-settings default held: music/SFX slider pair only.)

## Known limitations

- Per-bus active-voice counts remain unexposed (`ISfxBackend` completion telemetry gap,
  user-buses/STATUS.md) — unchanged by this slice.
- The web backend's per-user-bus gain nodes stay unrouted until a bus name crosses the core
  seam (`ISfxBackend` carries only the composed volume); user-bus nodes are created on demand
  by `setBusVolume` but nothing routes into them (D18/D21). Not a regression - recorded when
  shipped (work item 4).
- Bus-targeted ducks remain carried unvalidated (the maintainer's standing call from slice C
  stands; nothing in this slice's sample demos them).
