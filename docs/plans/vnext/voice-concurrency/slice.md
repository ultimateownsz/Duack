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
- **D-A4 — Slot-only stealing, then audible via the voice-control seam.** Initially a stolen
  voice lost only its bookkeeping slot and kept sounding. The #38 follow-up (2026-11-25) added
  the additive optional `ISfxVoiceControlBackend` seam (`PlayTracked`/`StopVoice`): `SfxManager`
  stops the chosen victim before the new voice starts; desktop maps handles to
  `IAudioInstance`s, web to JS source ids (`playSourceTracked`/`stopSource`, per-source registry
  cleaned on `onended`/`stopSource`/`StopAll`). `ISfxBackend.Play` is untouched; backends without
  the seam (test fakes, custom hosts) fall back to slot-only stealing — the observable
  config/result API is identical across backends (ADR-8 pattern), only the acoustic result
  differs. Real-browser audible verification remains a human check.
- **D-A5 — Dedicated desktop example.** The stress demo lives in `samples/Duack.Sample.Concurrency`,
  not in the existing storyline samples. Verification on web uses the existing `Duack.Sample.Web`
  head; no new web example project.

## Work list (non-exhaustive) — cross-platform follow-up

Recorded 2026-11-20 in the [`PLAN.md`](PLAN.md) addendum; landed 2026-11-25 (#38).

- [x] Web sample: author the limits and call `SfxManager.Update(unscaledDt)` every frame — the web
      head previously never aged voices, so a burst would saturate the cap permanently.
- [x] Add an optional, additive voice-control seam (handle → stop/fade) so a stolen voice is
      actually silenced: desktop maps to `IAudioInstance`; KNI gained `playSourceTracked`/
      `stopSource` (per-source registry beyond `StopAll()`). `ISfxBackend.Play` stays unchanged;
      falls back to slot-only stealing when absent.
- [ ] Optional, later: report real source end (`onended`) through the bridge to release slots at
      the true lifetime instead of the authored one; `[Experimental]` per `SHARED.md` §13.
- [x] Wire the limits into the existing `Duack.Sample.Web` head (authored in the head; browser
      listening with a user-gesture start still pending — worst-moment standard). No additional
      web sample project.
