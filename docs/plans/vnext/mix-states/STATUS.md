# Slice E — named mix states — STATUS

Started: Nov 26, 2026. Tracking issue: #46.

## State

Core landed on the working branch (authored config + runtime transition, unit-tested against
the §12 one-pole formula): `AudioConfigBuilder.WithMixState` authors named per-bus target
vectors, `AudioConfig.MixStates` carries them (sanitized), `MusicManager.Buses` exposes the
music-side mixer, and `MixStateManager` applies a state instantly or eases it over a duration,
writing both runtime mixers. See slice.md for the decision record.

## Awaiting

- The two-backend sample (calm field ↔ combat area, state transition while a crossfade runs)
  — acceptance per PLAN.md; human listening on both heads, judged by the worst moment.
- The gate-0/debrief view of the shipped transition curve: the duration↔time-constant mapping
  (D3) is revisitable until then.
- Duck-source intensity targeting ("states also target duck source intensities later") stays
  a design note in PLAN.md, unchanged by this work.

## Open questions

- The per-frame `SetBus` writes during a transition race a hot volume slider writing the same
  bus (last writer wins that frame). Pinned instead of solved: the player-settings surface
  writes master/music/sfx sliders; a state that also targets those buses will fight a slider
  the player is actively dragging. Noting it here; revisit if a sample or a user hits it.

## Known limitations

- The runtime remains two bus mixers seeded from one table (slice-F D13); a state writes both,
  and a transition eases each mixer from its own current volume, so an externally hand-diverged
  mixer is eased from its own audible baseline, not re-converged through the first mixer (D2).
- `MixStateManager.Update` expects the same unscaled frame delta the managers get, like every
  other ease in the library; there is no way to enforce that from the core.
