# 2D positional SFX — status

Started 2026-11-25 on branch `feat/positional-sfx`, cut from post-bus-unification
`master` as a fresh restart: the prior implementation on `feat/spatial-sfx` was set
aside unmerged on the qa agent's blocking verdict (its merge resolution had dropped
the `SfxManager` wiring, its changelog described adapter code that never existed —
see the 2026-11-25 entry in [`../REVIEWS.md`](../REVIEWS.md) and issue #34).

Design deltas the restart makes against this folder's original PLAN, each a
structural fix the review forced:

- Positional gain composes AFTER bus resolution (`master x bus x duck`) as its own
  documented multiplier, riding slice F's unified chain instead of fighting it. A
  positional play whose total gain composes to zero (mute floor, muted bus, ducked
  bus) takes the existing `ZeroEffectiveGain` path, so the mute floor releases the
  voice slot instead of consuming one.
- Re-mix keys by voice handle, never cue name: two overlapping instances of the same
  cue are independent voices. The seam follows the additive-capability pattern of
  `ISfxVoiceControlBackend` (see `ISfxPositionalBackend`): `PlayTrackedAt(cue, pan,
  pitch, volume)` plus a per-handle `SetVoiceMix(volume, pan)`.
- A backend without the seam plays positional cues mono-centered (composed volume,
  pan dropped, documented) — the established additive-fallback behaviour.
- Pan surface cut from three knobs to two (`PanSaturation`, `PanDistance`; the tanh
  shape itself carries the saturation sharpness). Distance models: two, per the
  slip-rule default (linear and inverse); the old branch's third model (exponential)
  is cut. Both models clamp to 1 at the reference distance and 0 at the max distance.
- Non-finite coordinates (listener, source) are treated as centered at zero in core
  and pinned by tests, instead of propagating NaN into the pan curve.

Known acoustic difference (ADR-8 pattern, revisitable): the web backend pans through
`StereoPannerNode` (constant-power, the SHARED section-12 normative law); the desktop
`SoundEffectInstance.Pan` property is the platform's linear taper. The core hands both
backends the same pan value (pinned cross-seam in tests); only the L/R reproduction
differs. A curve-matching recombination on desktop is deliberately not built until
listening evidence demands it.

Awaiting after the code landed on master (#41, merged 2026-11-25; external checks and taste, tracked on #44):

- Positional sample on both heads + human listening (acceptance criterion; judged by
  the worst moment).
- Tunable defaults stay revisitable per the slip rule until Gate 0: pan saturation
  depth, the pan-distance scale, the rolloff preset choice.
