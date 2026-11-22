# Duack vNext — Slice A — Voice concurrency limiting

Index: `../README.md` · Shared evidence/ADRs/gates/math: `../SHARED.md`

Status and open questions for this slice live in `STATUS.md`; decisions made while building live in `slice.md`.

---

Evidence: his hand-rolled limiter; the cheapest item; pure core.

Scope:
- Global cap and per-cue caps in config (`AudioConfigBuilder.WithVoiceLimit(...)`, per-cue override).
- Stealing policy: configurable per cue — oldest, quietest, or priority-based (reject new below priority). Default: oldest.
- Applies to SFX voices only; music already capped at 2 by crossfade design.
- A refused or stolen play returns a result the game can read (no silent drops — the README's no-silent-failure principle applies to play calls too).

Non-goals: no backend changes, no platform voice limits, no async loading changes.

Acceptance:
- Unit tests: caps enforced, policy selects the predicted victim at exact boundaries, per-cue overrides win over global, results reported correctly.
- Sample: the Desktop sample gains a stress mode firing 50 overlapping cues with a global cap of 8, with an on-screen active-voice counter.

Definition of done: tests green in CI, sample verified by a human listener, CHANGELOG entry, skills doc (`duack-skills/concurrency/SKILL.md`) written in the existing skill style.

### Slice A addendum (2026-11-20) — cross-platform concurrency

The limiter itself is already cross-platform: `VoiceLimiter` and the detailed play result live in
the pure core, and both heads construct `SfxManager` over their own `ISfxBackend`. Authoring
`SfxConfig.VoiceLimits` (or passing `voiceLimits:`) therefore caps plays and reports refusals on
MonoGame **and** on KNI/web without any core change. What is *not* yet complete is the web wiring
and the audible meaning of "stealing":

1. **Web sample wiring (small, do first).** `samples/Duack.Sample.Web/Game1.cs` constructs
   `SfxManager` but never calls `SfxManager.Update(dt)` — only `MusicManager.Update(dt)`. Authored
   voice lifetimes therefore never expire on web, so a sustained burst saturates the cap forever.
   Fix: configure the limits and call `_sfx.Update(unscaledDt)` every frame, exactly as desktop.
2. **Audible stealing (needs an additive backend seam).** Today a stolen voice loses only its
   bookkeeping slot; the sounding voice keeps playing. On desktop `SoundEffectSfxBackend` already
   holds each `IAudioInstance`, so a handle→instance map lets the manager stop/dispose the victim.
   On KNI, `IWebAudioNative.PlaySource` is `void` and the bridge only has `StopAll()`; an additive
   `PlaySourceWithHandle`/`StopSource(id)` (or a per-source `GainNode` fade) is required. Keep
   `ISfxBackend.Play(string, float, float)` untouched — add an optional voice-control seam, have
   `SfxManager` call it when a victim is chosen, and fall back to slot-only stealing when a backend
   does not implement it. Per ADR-8 the observable config/result API stays identical; only the
   acoustic result differs, and that difference is documented.
3. **Optional exact lifetime.** Web can later report a source's `onended` back through the bridge
   so a slot is released at the real end instead of at the authored lifetime. This is the
   per-playback SFX handle experiment in §13; keep it behind an `[Experimental]` flag with a kill
   criterion, and do not use it as a prerequisite for 1 or 2.
4. **Verify in the existing web sample.** Do not create another web example. Once 1 and 2 are in
   place, wire the limits into the existing `Duack.Sample.Web` head and verify there in a browser
   with the user-gesture start, judging by the worst moment. The dedicated
   `Duack.Sample.Concurrency` project stays desktop-only for now.

Scope note: this addendum records a follow-up, not work already landed. Slice A as committed caps
admissions on both heads; audible cross-backend stealing and the web wiring in the existing sample are a separate,
ungated chunk to be scheduled after the current slice is accepted.
