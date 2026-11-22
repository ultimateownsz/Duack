# Duack vNext — Voice concurrency — decisions and work log

Decisions made while building this slice. The spec is [`PLAN.md`](PLAN.md); current state and open
questions are in [`STATUS.md`](STATUS.md).

## Decisions

- **D-A1 — Authored voice lifetime.** The backend exposes no voice-end event, so a voice's duration
  is authored (`DefaultVoiceSeconds`, per-cue `DurationSeconds`) and aged by `SfxManager.Update(dt)`.
  The caller must pass unscaled dt (same rule as fades and ducks).
- **D-A2 — Additive result API.** `Play(...)` still returns `bool`. `PlayDetailed(...)`,
  `LastPlayResult`, and `ActiveVoiceCount` report refusals and readback without breaking callers.
- **D-A3 — Backend refusal releases the slot.** If `ISfxBackend.Play` returns false (missing clip or
  unloaded web buffer), the reserved slot is released so a failed play cannot saturate the cap.
  This also covers the web async-preload path.
- **D-A4 — Slot-only stealing for now.** A stolen voice loses its bookkeeping slot but keeps
  sounding; making the steal audible needs an optional handle/stop-or-fade seam on the backends.
  Recorded as a cross-platform follow-up in [`PLAN.md`](PLAN.md), not part of this slice.
- **D-A5 — Dedicated desktop example.** The stress demo lives in `samples/Duack.Sample.Concurrency`,
  not in the existing storyline samples. Verification on web uses the existing `Duack.Sample.Web`
  head; no new web example project.

## Work list (non-exhaustive) — cross-platform follow-up

Recorded 2026-11-20 in the [`PLAN.md`](PLAN.md) addendum; not scheduled with this slice.

- [ ] Web sample: author the limits and call `SfxManager.Update(unscaledDt)` every frame — the web
      head currently never ages voices, so a burst would saturate the cap permanently.
- [ ] Add an optional, additive voice-control seam (handle → stop/fade) so a stolen voice is
      actually silenced: desktop maps to `IAudioInstance`; KNI needs a JS bridge method beyond
      `StopAll()`. `ISfxBackend.Play` stays unchanged; fall back to slot-only stealing when absent.
- [ ] Optional, later: report real source end (`onended`) through the bridge to release slots at
      the true lifetime instead of the authored one; `[Experimental]` per `SHARED.md` §13.
- [ ] Wire the limits into the existing `Duack.Sample.Web` head and browser-listen there with a
      user-gesture start (worst-moment standard). No additional web sample project.
