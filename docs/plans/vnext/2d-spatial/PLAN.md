# Duack vNext — Slice B — 2D positional SFX

Index: `../README.md` · Shared evidence/ADRs/gates/math: `../SHARED.md`

`STATUS.md` (state and open questions) and `slice.md` (decisions) are added when this slice starts.

---

Evidence: his hand-rolled implementation; cheap on both backends; high audible value.

Scope:
- Core math (pure BCL, fully unit-tested): pan from normalized listener-space x with a saturation curve (pan should reach its maximum before the viewport edge; hard ±1 at the edge reads artificial — he said "slight panning"); gain from distance with a rolloff curve, a clamp distance, and a mute floor (distant cues release their voice — interacts with slice A).
- API: `sfx.Play(cue, position, ...)` overload plus a `PlayAt(cue, worldPos, listener)` convenience; per-frame update for moving sources (`sfx.UpdatePosition(handle, pos)` or a returned handle).
- MonoGame adapter: set `Pan`/`Volume` per instance per frame. No new APIs needed — the platform exposes them.
- KNI adapter: add `StereoPannerNode` + per-instance `GainNode` to the `WebAudioNative` bridge for one-shots. Music path untouched.
- Input seam (per ADR-11): `SetListener(x, y)` as a primitive input plus `ListenerPosition` readback; spatial state updates ride the existing per-frame `Update` like `dt`. Zones, triggers, and region wiring stay engine-side (duack-skills); a richer region config surface (per-region fades, mix states, restore-on-exit) is deferred until a consumer shows a concrete wiring story.
- Curve parameters live in config (`WithSpatial(new SpatialConfig { ... })`) with sane defaults; ship more than one rolloff preset (linear, logarithmic) until his debrief says which is taste.

Acceptance:
- Unit tests pin the curves: pan(x) end behavior, monotonic attenuation, mute floor, clamp distance, listener-relative math.
- A positional sample: one moving source crossing the screen and exiting the viewport on both backends. A human listens on both. Judge by the worst moment, not the average.
- Web build of the sample verified in a browser with a user-gesture start (browsers suspend audio until gesture).

Definition of done: tests green, both samples pass human listening, crossfade/ducking behavior unchanged (regression: run the existing sample storyline), CHANGELOG + skills doc.

---

Addendum 2026-11-25 — fresh restart from master (issue #34).

Prior implementation on branch `feat/spatial-sfx` was set aside unmerged on the qa agent's
blocking verdict (see REVIEWS.md, 2026-11-25: the merge resolution had silently dropped the
SfxManager wiring, and the changelog described adapter wiring that never existed). The slice
restarts from post-bus-unification `master` on `feat/positional-sfx` with the deltas recorded
in [STATUS.md](STATUS.md): positional gain composes after bus resolution (the mute floor then
releases the voice via the zero-effective-gain path), re-mix keys by voice handle through the
additive `ISfxPositionalBackend` seam, the pan surface shrinks to two knobs (`PanSaturation`,
`PanDistance`), two distance models ship per the slip-rule default (linear, inverse), and
non-finite coordinates are pinned. Everything else in this PLAN — curves, the moving-source
handle, the sample acceptance and listening gates — stands.
