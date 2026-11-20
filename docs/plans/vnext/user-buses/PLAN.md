# Duack vNext — Slice C — User-defined buses

Index: `../README.md` · Shared evidence/ADRs/gates/math: `../SHARED.md`

`STATUS.md` (state and open questions) and `slice.md` (decisions) are added when this slice starts.

---

Evidence: medium — he asked for it. Blocked by Gate 2 (routing model), which resolves by debrief or by the slip-rule default after December 1, 2026 — see §3.

Scope:
- Config: `AudioConfigBuilder.WithBus("ambience", volume)`; unlimited named buses compose into the existing deterministic chain (bus factor is one more multiplication; nothing about the adapter contract changes — this is the proven machinery).
- Routing per Gate 2's outcome.
- Ducking extends to buses: a bus is a valid duck target; `DuckMixer` already works on ratios, so this is routing plus composition, no engine work.
- Ref-counted duck holds (from Voltage gap analysis): `BeginDuck(source, ...)`/`EndDuck(source)` paired holds so overlapping speakers cannot release the duck early; a sustained "duck while a category plays" gate is a follow-up once per-bus targets exist.
- Runtime per-bus volume, mute, and pause flags (from Voltage gap analysis): a paused gameplay bus while music and UI continue; a global master mute switch folding into the same resolution path. Per-cue and per-bus settable, hot-updatable.
- One effective-gain resolution path (from Voltage gap analysis): a single per-frame resolver walks the whole chain — master switch → bus tree → mute/pause → duck contribution → player settings — producing per-source effective gain, with per-bus readback of the stages (BusEffectiveGain, duck contribution, active voice count) and change events, so a frontend mixer renders Duack's mix state without owning it.
- Unknown bus names fall back to the SFX bus and report loudly (extends the no-silent-failure principle to routing); a play that resolves to zero effective gain reports the cause instead of staying silent.
- Player settings: decide whether per-bus player volumes are exposed in the settings store (default: no; music/SFX sliders stay the user-facing pair unless Gate 0 shows otherwise).

Non-goals: no bus effects in this slice (slice D), no reordering of the fixed music/SFX buses, no breaking of the existing constructor signatures — new buses compose below master alongside the fixed pair.

Acceptance:
- Unit tests: bus factor composes with master/fixed buses/context scales/player settings in the documented order; ducking a bus scales every cue on it; an un-routed cue lands on the SFX bus (or a documented default).
- Sample: ambience bus faded while UI sounds continue, ducked under a dialog cue.

Definition of done: tests green, sample verified, README feature table updated, skills doc.
