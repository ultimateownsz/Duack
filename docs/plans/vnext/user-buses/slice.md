# Slice C — user buses — decisions made while building

Populated as implementation decisions land. ADR deviations belong in
[STATUS.md](STATUS.md) with rationale, never here.

### Structure decisions (Nov 22, 2026, from review-structure.md §3)

- **D1 — Flat buses only.** No parent-chain gain tree, no per-bus effects, no solo, no
  snapshot transitions. A bus tree is a mixer graph; the README scope promise excludes it,
  and an effect hook would be untestable dead surface in a pure-BCL assembly. Re-propose
  against [review-structure.md](review-structure.md) §2 only with new evidence.
- **D2 — Bus naming stays lowercase, Ordinal-compared, config-seeded.** `sfx` is the fixed
  fallback name and must not be renamed or re-cased (see review-structure.md V1).
- **D3 — Mutation surface is methods + `Changed`, never public mutable fields.** Same-value
  setters are no-ops that do not re-notify; `Resolve` is the only routing surface and the
  authoring surface (`SetBus`) is the only creator.
- **D4 — Ducking stays in the existing duck system.** Bus gain never embeds duck
  multipliers.
- **D5 — Gain resolves live** on every play, never resolve-once-at-play.

### Working decisions

- **Bus-duck scaling applies at play time.** One-shots do not update per frame and have no
  completion telemetry, so a play routed to a bus is scaled by `DuckRatioFor(bus)` when it
  starts (`SfxManager.Play`). Chaos-free with holds: cues played while the hold is active are
  ducked. Bus *volume* changes (mute/pause) likewise apply from the next play.
- **Zero-effective-gain plays succeed verbally, fail audibly.** `Play` returns true, raises
  `ZeroEffectiveGain(cue)`, and makes no backend call and no duck — a muted-bus play is not a
  caller error, but it must not silently duck the music for nothing.
- **Sample wiring rides the shared storyline.** `StoryFrame` carries `Bus` plus
  `BeginDuckSource`/`EndDuckSource` intents; both heads apply them identically from
  `SampleAudio`'s config (`ambience` bus at 0.55, `dialogue` duck targeting the bus), so the
  desktop and web demos cannot drift (pinned by `Duack.Sample.Tests`).
- **`DuckSourceView` grew a `Target` field** with a default (`"music"`), keeping existing
  positional constructions compiling.
- No per-bus active voice counts: `ISfxBackend` has no completion callback and honest
  absence beats invented numbers (recorded as a known limitation in STATUS.md).
- **Bus-targeted ducks are carried unvalidated — out of the sample too.** The maintainer's
  call (Nov 22, 2026, PR review comments): until a real user and project prove the
  dialogue-style scenario, no worked example ships in the skills and the sample does not
  wire it. The sample's original dialog-hold demonstration was removed (PR review, same
  day); what stays in the demo is bus routing (the notice cue on the `ambience` bus) and
  the mute/hot-volume path. The mechanism keeps its core unit tests only. Revisit when
  Gate 0 or a consumer reports the need.
- **qa verdict: good** (Nov 22, 2026), with recorded non-blocking findings (round 1 below;
  round 2 verified W1/W2 and the docs increments - no new findings except a duplicated qa
  heading here, now merged):  - The issue's "single resolver readback" is split across two surfaces by design:
    `BusMixer.Views` carries per-bus gain state, `MusicManager.DuckRatioFor(bus)` carries the
    duck contribution; a duck source targeting the literal `"sfx"` bus affects only bus-routed
    plays (the fixed SFX path is outside bus-duck composition).
  - `DuckMixer.Set(name, false)` now also clears ref-counted holds — deliberate
    (scene-switch semantics of `ReleaseAll`, per-source), unit-pinned.
  - The web head lacks the desktop's M-key mute toggle (the mute demo is desktop-only;
    verify web by explicit build when listening).
