# Slice C — user buses — decisions made while building

Populated as implementation decisions land. ADR deviations belong in
[STATUS.md](STATUS.md) with rationale, never here.

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
- **Bus-targeted ducks are carried unvalidated.** The maintainer's call (Nov 22, 2026): the
  mechanism is implemented and unit-tested, but until a real user and project prove the
  dialogue-style use case, the buses skill documents it without a worked example and the
  README/CHANGELOG avoid dialogue framing. The sample's dialog-hold wiring stays — it is the
  scenario the pending human listening check must judge. Revisit if Gate 0 or a consumer
  reports the need.
- **qa verdict: good** (Nov 22, 2026), with recorded non-blocking findings:
  - The issue's "single resolver readback" is split across two surfaces by design:
    `BusMixer.Views` carries per-bus gain state, `MusicManager.DuckRatioFor(bus)` carries the
    duck contribution; a duck source targeting the literal `"sfx"` bus affects only bus-routed
    plays (the fixed SFX path is outside bus-duck composition).
  - `DuckMixer.Set(name, false)` now also clears ref-counted holds — deliberate
    (scene-switch semantics of `ReleaseAll`, per-source), unit-pinned.
  - The web head lacks the desktop's M-key mute toggle (the mute demo is desktop-only;
    verify web by explicit build when listening).
