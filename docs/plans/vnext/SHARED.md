# Duack vNext

Status: draft v2 · November 17, 2026 · Owner: Duack maintainer
Revision v2 (Nov 17, 2026): the prototype weekend (Nov 14–15) did not take place. Timing of the prototype and debrief is now unknown. The plan no longer holds slice A and B hostage to the debrief; see §1, §3 and the slip rule.
Executor: an AI coding agent with no prior memory of this project. This document plus the repository must be sufficient to orient, act, and report. If they are not, fix this document first — that is the walk test.

---

## 0. Orientation (read this first)

Duack is a code-first audio library for MonoGame, KNI, and FlatRedBall2 games, shipped as preview NuGet packages (`Duack`, `Duack.MonoGame`, `Duack.KNI`). Core facts you need before touching code:

- Core is pure BCL over two interfaces: `IMusicBackend` and `ISfxBackend`. MonoGame and KNI adapters share configuration, playlists, and ducking.
- Volume chain today: Master → fixed music/SFX buses → per-context scales → player settings. Exactly two buses, both hardwired at construction time (`masterVolume`, `sfxBusVolume`). No user-defined buses exist.
- `DuckMixer` provides sidechain ducking: duck sources with ratio, attack, hold, release; pulse by cue; `ReleaseDucks()`, `IsDuckReady()`, `DuckRatio`. Works on ratios, not hardware voices.
- Crossfades: equal-power curve, sequential, max 2 simultaneous music tracks. Desktop overlaps voices; web (KNI) sequences fade-out → swap → fade-in behind the same telemetry API.
- SFX: directory convention `Content/Audio/SFX/` and `Tracks/` (case-sensitive, lowercase extensions). Web needs a checked-in `audio-manifest.json` produced by `duackcli scan`.
- Web SFX one-shots already go through a JS bridge (`WebAudioNative` → `WebAudioSfxBackend`). Web music goes through `MediaPlayerMusicBackend` / HTML audio element, outside any Web Audio graph.
- MonoGame `SoundEffectInstance` exposes only `Volume`, `Pitch`, `Pan`. No filters, no submix voices, no sample tap. This is a hard platform constraint, not a TODO.
- Tooling: `duackcli` (build-time only: `convert`, `scan`, `normalize`). Samples exist for Desktop, Web, and Crossfade. Skills live in `duack-skills/`. Tests pin the shared core (`tests/Duack.Sample.Tests`).

Read order for the workspace: this file → `README.md` index → CHANGELOG → the slice's `PLAN.md` → its `STATUS.md` → its tests.

## 1. Evidence base (why this plan exists)

A target user (indie C# game dev, 2D games, desktop-first) described the system he hand-rolled in his own game, before Duack's public release (Oct 20, 2026):

1. Buses/channels with simple mixing and volume control.
2. Simple FX racks on those buses (reverb, filters, ducking/sidechaining).
3. Max concurrency of sounds.
4. 2D spatial sound: slight left/right panning by screen position, volume attenuation by distance, weaker when out of viewport.

Mom Test assessment: he built it, so past behavior proves the need. That evidence stands on its own; it does not depend on the debrief. He was scheduled to prototype with the released library over the weekend of Nov 14–15, 2026; that session did not happen. Consequence of v2: slices A and B may be fully built now — their need is proven by his hand-rolled implementations, and the debrief only tunes them (curve taste, concurrency numbers). The debrief still gates the decisions that need his data: the routing model (Gate 2), the runtime-filter question (slice D trigger), and any curve presets he indicates are taste rather than necessity.

## 2. Decisions already made (do not relitigate without new evidence)

ADR-1 — Evidence-ordered slices. Build in order of evidence-to-cost ratio: concurrency → 2D spatial → user buses → build-time effects experiment. Rationale: each item is cheaper and better-evidenced than the next.

ADR-2 — No runtime DSP in core. Duack owns no sample data on either backend (desktop: SoundEffectInstance volumes; web: HTML audio element for music). Runtime per-voice effects would require a decode-and-mix engine via `DynamicSoundEffectInstance`, which turns Duack into the mixer graph the README explicitly refuses to be. Deferred indefinitely unless a user proves a need baked variants cannot cover.

ADR-3 — Effects are baked at build time, authored in C#. A `Duack.Render`-style package holds profile builders (POCOs + builder, pattern of `DuckSourceConfig`) plus offline DSP (biquads, convolution). A tiny build console project in the game's solution renders variants; the manifest maps cues to variants; runtime selects files by name. No adapter involvement; both backends play files identically; fully portable.

ADR-4 — No JSON authoring format. Code-first authoring is a listed Duack feature; profiles are C# so they are compile-checked, unit-testable, and self-describing for the walk test. `duackcli` keeps mechanical ops (`convert`/`scan`/`normalize`); it may later replay a compiled profile from the manifest, but only if someone asks.

ADR-5 — Naming by phase. Runtime playback capabilities live in adapters (`Duack.MonoGame`, `Duack.KNI`, capability flags if ever needed). Build-time packages get build vocabulary (`Duack.Render`). Avoid "extension" (collides with C# extension-method convention) and never call the render package an adapter.

ADR-5 addendum v2 (Nov 19, 2026, Laws of UX naming analysis). The naming decision was tested against the Laws of UX, using the audience's actual mental models (C# game devs on MonoGame/KNI, not recording engineers). Results:

- Jakob's Law + Mental Model: the name must summon an existing, correct model. The audience's only pre-existing model for "computed ahead of time, stored in the asset, zero runtime cost" is bake (Unity baked lighting). Render summons the graphics pipeline instead — wrong first model, and the paradox of the active user says they will not read the README to correct it. Print, Commit, Freeze, and Bounce summon models this audience does not have (console printing, git, DAW jargon) or wrong ones.
- Law of Prägnanz + Cognitive Load: the name that decodes in the fewest steps wins. Bake decodes in one step; every pro-audio alternative needs a paragraph; novel coinages (Confit, Kiln, Dub) need brand lore no active user will read.
- Von Restorff Effect: Bake remains distinctive inside the package list while staying decodable — distinctiveness without obscurity.
- Occam's Razor: the fewest-assumptions name for "writes effects into files before the game runs" is the kitchen word, not the studio word.

Verdict: a better name than Bake was not found because one cannot exist within this audience's model vocabulary; any "better" name would require a model the audience does not share. Bake is promoted to the working name for the package and the CLI verb (`duackcli bake`). Render is demoted to rejected (wrong first mental model). Rejected list extended: Effects/Fx, Content (pipeline vocabulary), Print, Commit, Freeze, Bounce, Press, and novel coinages. The pre-release package-list test stands as final confirmation, unchanged: a MonoGame dev scanning the packages must instantly see what runs on the dev machine and never ships to the player.

ADR-6 — Bus effects are honest about limits. Buses-as-volume are portable. Bus effects are baked variants (replacement mode: wet file replaces dry). A send/return emulation (dry + fully-wet instances together, wet volume as send knob) is a spike, gated on a sync-offset test — two instances started the same frame are not sample-aligned; acceptable for reverb tails, not for tight filters or short delays.

ADR-7 — 2D positional audio is playback parameters, not DSP. Pan and per-instance volume only. Core computes pan/gain from 2D positions (pure BCL); backends apply per-instance values (desktop: `Pan`/`Volume`; web: `StereoPannerNode`/`GainNode` via the existing `WebAudioNative` bridge). Music is never positional.

ADR-8 — Capability flags follow the crossfade precedent. Where backends differ acoustically, the observable API stays identical and the difference is documented (same pattern as overlap vs sequenced transitions). Applies if any runtime effect capability is ever added.

ADR-9 — Live mixing is web-only and stays out of vNext. The Web Audio API (GainNode, BiquadFilterNode, ConvolverNode, StereoPannerNode) makes runtime mixing genuinely possible in the browser, including a true sample-aligned send/return that solves the dual-instance sync problem. Desktop has no equivalent (`SoundEffectInstance` exposes Volume/Pitch/Pan only), so this can never be a parity feature. If it is ever built, it is an opt-in web capability behind a capability flag (pattern of ADR-8): one `WithBusEffect` config line serves baked variants on desktop and a live graph on web. Hard prerequisite: web music playback must move from the HTML audio element into the Web Audio graph — the largest single change in this plan's scope and deliberately not scheduled. Trigger: Gate 0 evidence of a runtime-filter need (pause-menu muffle, underwater). Until that trigger fires, this ADR exists to preserve the reasoning, not to schedule work.

ADR-10 — Mix states are authored config, never captured runtime state (added Nov 19, 2026, from the Voltage gap analysis). Named mix states are vectors of authored per-bus targets in `AudioConfigBuilder` (a "combat" state, a "low-health" state), applied instantly or over a timed transition (`TransitionTo(name, seconds)` with `IsTransitioning` telemetry matching the existing crossfade telemetry). Engine snapshots capture live gains; Duack does not copy that, because authoring in code and refusing an editor/mixer-graph are the same boundary. Transitions ride the existing volume chain with the one-pole smoothing of §12; player settings still layer over the state targets. This is ranked the single highest-value missing mix feature in the gap analysis.

ADR-11 — Input seam rule (added Nov 19, 2026, from the Voltage gap analysis §6b). Core accepts primitives — names, floats, positions — and never engine types (Entity, Collider, Component). Observable state goes in through primitive setters (`SetListener(x, y)`) and comes out through readback (`ListenerPosition`, meters), mirroring how transition telemetry already flows. Anything taking an engine type goes in an adapter package or does not exist. Engine-side wiring (zones, triggers, colliders) ships as duack-skills, the existing channel for engine patterns without owning engine code. This is the seam discipline of `IMusicBackend` (Duack owns the consequence, the backend owns playback) applied to inputs.

ADR-12 — DSP provenance and the standing rejection of a managed mixer (added Nov 19, 2026, from the Voltage gap analysis findings). Voltage is the counterexample to "effects require a DSP framework": its reverb is a public-domain reimplementation of Freeverb (~150 lines, tuning constants included), its low-pass a hand-written one-pole, its only third-party audio packages codecs (NVorbis MIT, NLayer MIT). Consequences: (1) `Duack.Render` needs no DSP package — convolution and biquads from §12 are a few hundred lines of pure C#; (2) the web backend never ports algorithms, it uses native `BiquadFilterNode`/`ConvolverNode` (per ADR-9); (3) a desktop managed mixing backend remains rejected all the same — cheap DSP was never the reason, ownership of the playback device (real-time thread, latency, GC, underruns) was, and that cost is unchanged by the algorithm being 150 lines. If it is ever revisited, the reference shape is recorded: a pure managed mixer testable through a MixInto-style callback, a thin SDL2 device layer (~115 lines, four P/Invoke functions, SDL2 already ships with MonoGame DesktopGL; pin the SDL2 callback API, SDL3 renamed the entry points). Every deferred experiment ships behind an [Experimental] flag with a kill criterion defined up front.

## 3. Gates (do not build past a closed gate)

Gate 0 — Prototype debrief. Not yet held (Nov 14–15 session did not happen; new date unknown). Capture facts, not opinions when it happens: what he built, where he got stuck, what he hand-rolled because Duack lacked it, his concurrency numbers and stealing policy, his pan/attenuation curves (taste vs necessity), whether he needs runtime filter automation (pause menu / underwater cases), his targets (desktop only vs web), and whether he would delete his own code if Duack covered the list. The code he shares outranks every questionnaire answer.

v2 blocking scope: Gate 0 blocks the Gate 2 routing decision, the slice D trigger decision, and final curve-preset selection in slice B. It no longer blocks slices A and B — build them on the documented defaults and mark debrief-tunable parameters as such in the slice files.

Slip rule: if no debrief has happened by December 1, 2026, apply the default decisions — Gate 2 default (per-cue argument with optional directory convention, contexts stay music-only), slice B defaults (both rolloff presets shipped, saturation curve as documented) — and record them in the affected slice's `STATUS.md` as revisitable defaults, not as validated choices. Proceed with slice C. Slice D still requires positive evidence of a runtime-color need and stays dormant until Gate 0 provides it or another user does.

Gate 1 — Sync-offset spike (blocks send/return mode in slice D). Start two SoundEffectInstances of the same buffer in one frame on MonoGame and on KNI; measure the offset; classify audible for reverb-class effects only, or kill send/return and keep replacement mode.

Gate 2 — Routing model (blocks slice C implementation). Decide how a cue gets a bus: per-cue in code, per-directory convention (`SFX/UI/`), or per-context in config. Prefer whatever he hand-rolled; default proposal: per-cue argument with optional directory convention, contexts stay music-only.

## 8. Verification standards (the agent cannot hear)

- Every slice ships unit tests for the pure logic (curves, policies, composition, mapping). This is the agent's primary correctness signal.
- Every audible behavior ships a runnable sample. A human performs the listening check on both backends before a slice is called done. The standard is the worst moment of the experience, not the average (existing Duack docs standard).
- Cross-backend rule: identical observable API, documented acoustic differences. If a change makes one backend behave observably differently in API terms, stop and update this plan first.
- Regression: the existing Desktop and Web story samples must behave unchanged after every slice.

## 9. Guardrails (non-negotiable)

- The core stays pure BCL. Platform APIs enter only through adapters.
- No mixer engine, no sample-data ownership, no runtime per-voice DSP (ADR-2). If a task seems to require them, escalate in the status report instead of building.
- No new declarative config files (ADR-4). C# authoring only.
- Preview API discipline: additive changes first; breaking changes require a CHANGELOG "preview breaking" note and matching skills updates.
- Keep `duackcli` build-time only; nothing it produces ships as a runtime dependency.
- Update the README's "What it's not" sentence when slices C/D land: "no mixer graph" stays true (buses are arithmetic, effects are baked), but "none are planned" must be revised to reference this plan.

## 10. Workspace conventions (agent operating rules)

- `docs/plans/vnext/README.md` is the index. This file holds the evidence, ADRs, gates, standards, sequencing, and math shared by every slice.
- Each slice has its own folder with `<slice>/PLAN.md` (spec), present from the moment the plan is split. Add `<slice>/STATUS.md` (state and open questions) and `<slice>/slice.md` (decisions made while building) only when that slice starts; until then the folder holds the spec only.
- Record any decision that deviates from an ADR in that slice's STATUS.md with rationale; do not silently override an ADR.
- Track the plan only when a new slice is created that references it. Until then these files stay uncommitted working state.
- Walk test before each handoff: a fresh agent session reading only the repo files (README, this plan, slice file, STATUS) can state what is done, what is in progress, and what is blocked. If not, the files are wrong, not the reader.
- End every session with a report: what changed, what is verified by tests, what awaits human listening, what is blocked and why.

## 11. Sequencing summary (v2)

1. Slice A concurrency — build now, no gate. His hand-rolled limiter is sufficient evidence.
2. Slice B 2D spatial — build after A, no gate. Defaults documented; curve presets stay tunable, and parameters marked debrief-tunable in the slice file.
3. Slice C buses — after A and B, now including the gap-analysis extensions: single effective-gain resolution, per-bus mute/pause, ref-counted duck holds, unknown-bus fallback, per-bus metering readback. Gate 2 routing decision: by Gate 0 if the debrief has happened, else by the slip-rule default after December 1, 2026 (or earlier if the maintainer decides the default is good enough).
4. Slice E mix states — after C (needs the bus tree and the resolution path). Not gated: authored config only, pure core, evidence is a ranked gap plus the standard game pattern.
5. Slice D render experiment — dormant until positive evidence of a runtime-color need (Gate 0, or another user's). Gate 1 sync-offset spike may be run at any time as a standalone measurement; it informs but does not trigger slice D. De-risked on provenance per ADR-12: no DSP package needed, ever.
6. Gate 0 debrief — whenever it happens; re-tune whatever its answers touch and record the deltas in the affected slice's `STATUS.md`. If it surfaces a runtime-filter need, that also triggers the ADR-9 evaluation (web live mixing), which remains otherwise unscheduled.

If Gate 0's debrief contradicts this plan (e.g., he needs runtime filters, or never needed buses), revise this document via a dated addendum rather than editing history.

## 12. Mathematical foundations (normative for slice tests)

All curves and processors referenced in the slices are defined here. Slice unit tests must pin these formulas exactly — an implementation whose crossfade, pan, rolloff, biquad, or convolution output deviates from the definitions below is wrong, not "different".

Notation: discrete-time signal x[n] at sample rate fs (T = 1/fs); amplitude gain g relates to level difference in dB as ΔL = 20·log₁₀(g), g = 10^(ΔL/20); signal power ∝ g².

Equal-power crossfade (existing core; also slice D dry/wet blend). For uncorrelated sources, constant output power across the overlap requires the gain pair to satisfy f₁(t)² + f₂(t)² = 1 for all t. Canonical solution over t ∈ [0,1]:

  f_out(t) = cos(πt/2),  f_in(t) = sin(πt/2)

Property to test: f_out² + f_in² = 1 at any t; midpoint gain 0.707 each. Equal-gain (linear) fades instead satisfy f₁ + f₂ = 1 and dip ≈ 3 dB in the middle — the exact failure mode Duack's crossfade exists to avoid.

Constant-power stereo pan (slice B). For pan p ∈ [−1, 1], let θ = (p + 1)·π/4:

  L(p) = cos θ,  R(p) = sin θ,  L² + R² = 1

This is the equal-power law the Web Audio API uses for StereoPannerNode, so desktop `SoundEffectInstance.Pan` (linear in [−1,1]) and the web panner agree by construction if the core hands both the same p. For the 2D screen-space mapping, the plan's "saturate before the edge" candidate curve is p = psat·tanh(x_norm/σ) with psat < 1 (debrief-tunable, Gate 0).

Distance attenuation (slice B). Distance d, reference distance dref, clamp distance dmax, rolloff factor R. The three distance models of the Web Audio PannerNode, all candidates for the config presets:

  inverse:     g(d) = dref / (dref + R·(max(d, dref) − dref))
  linear:      g(d) = 1 − R·(d − dref) / (dmax − dref)
  exponential: g(d) = (max(d, dref) / dref)^(−R)

The inverse model approximates physical free-field spreading (pressure amplitude ∝ 1/d); the plan's mute floor is g(d) = 0 for d ≥ dmax, which also releases the voice (slice A interaction). Tests: monotone non-increasing, g(dref) = 1, g(dmax) = 0 by definition of the clamp.

Duck envelope smoothing (existing core, extension target for slice C bus ducks). The attack/release curve is the first-order exponential approach (one-pole):

  g(t) = g_target + (g(t₀) − g_target)·e^(−(t − t₀)/τ)

where τ is the time constant; the remaining gap closes by 1 − e⁻¹ ≈ 63.2% per τ. This matches Duack's existing attack/hold/release duck semantics and the Web Audio AudioParam setTargetAtTime definition, so a future web-backend duck behaves identically.

Biquad filters (slice D offline). Transfer function and difference equation:

  H(z) = (b₀ + b₁z⁻¹ + b₂z⁻²) / (1 + a₁z⁻¹ + a₂z⁻²)
  y[n] = b₀x[n] + b₁x[n−1] + b₂x[n−2] − a₁y[n−1] − a₂y[n−2]

Lowpass prototype per the RBJ cookbook, with ω₀ = 2πf₀/fs and α = sin(ω₀)/(2Q):

  b₀ = (1 − cos ω₀)/2,  b₁ = 1 − cos ω₀,  b₂ = (1 − cos ω₀)/2
  a₀ = 1 + α,  a₁ = −2 cos ω₀,  a₂ = 1 − α        (normalize all by a₀)

Highpass, bandpass, peaking, and shelf variants follow the same cookbook. Tests: DC gain of the LPF = 1 (impulse input sums to unity), Nyquist gain = 0, stability |poles| < 1.

Convolution reverb (slice D offline). Wet output of impulse response h of length M:

  y[n] = Σₖ₌₀^(M−1) h[k]·x[n − k]        (direct: O(N·M))

Offline fast convolution via FFT: Y(k) = X(k)·H(k) with transform size N ≥ Nₓ + M − 1 (zero-padded, acyclic); long inputs are partitioned into blocks and summed with the overlap-add method, cost O(N log N). Build time has no latency budget, so full-accuracy FFT convolution with real impulse responses is the default; block-based FDN/Schroeder approximations are not needed. Tests: impulse input yields h at the output exactly; linearity; determinism.

C# implementation notes (slice D tooling). Decode/encode for the render step should not be hand-rolled: NAudio is the established open-source .NET audio library with a 32-bit float mixing engine and WAV readers/writers, a reasonable foundation for `Duack.Render`'s file I/O (verify license compatibility at implementation time). The FFT for fast convolution is either a small in-tree radix-2 implementation (testable against known DFT pairs) or a numerics package — decided at slice start, recorded in the slice file.

## 13. Voltage gap analysis intake (Nov 19, 2026)

Source: a feature comparison against the Voltage.Engine/Audio source at main (verified Nov 19, 2026) with Duack status from README, duack-skills, and v0.1.0-preview.5. Voltage is an engine that also owns playback, spatialization, and editor tooling; Duack is a mix library that deliberately does not. Sections ADR-10/11/12, slice C extensions, slice B input seam, and slice E record the accepted findings. This section is the reconciliation record.

Confirmed by the analysis (no plan change): the evidence ordering of slices A→C; that per-cue instance caps are the cheapest gap in the whole comparison; that 2D spatial is gain scheduling with free desktop-web parity (both backends already accept the numbers); that Duack's transient duck, duck telemetry, crossfade, playlists, fade control, seek, cue variation, auto-duck-by-cue, loud load failures, loudness normalization, and web conversion are equal or stronger than Voltage's counterparts.

Added to the plan:
- Slice E, named mix states: the highest-value missing mix feature, authored-config form per ADR-10.
- Slice C extensions: single effective-gain resolution path with per-bus readback and change events, per-bus mute/pause flags, global master mute, ref-counted duck holds, unknown-bus fallback with loud reporting, zero-effective-gain play reporting, per-bus active voice counts.
- Slice B extension: listener position as primitive input plus readback, per the ADR-11 input seam.
- Skills-work note: warn against scaled dt in fades and ducks (correctness currently depends on the caller passing unscaled dt).

De-risked: DSP provenance per ADR-12. Effects need no DSP framework (public-domain Freeverb, textbook filters, MIT codecs); `Duack.Render` stays dependency-light; the web backend uses native nodes instead of ports; a desktop managed mixer stays rejected for the recorded ownership reasons, with the SDL2 reference shape (~115-line device layer, MixInto-style testable mixer, SDL2 callback API pinned) preserved for a future decision.

Deaffirmed out of scope, with reasons:
- Bus solo: debug/editor tooling, not a library concern.
- Zone, trigger, and listener components: engine state; wiring ships as duack-skills and samples (ADR-11).
- Software mixing backend, runtime codecs, mix-loop profiling: impossible to measure without owning the mixing loop, which is the README boundary.
- Editor inspector sync, live editor sliders: no editor, by the README promise.
- 3D HRTF: web gets it natively via PannerNode; desktop needs convolution plus an HRTF dataset whose research licenses (IRCAM Listen, SADIE) are not public domain — the MIT-licensed KEMAR set is the candidate, license verification mandatory, capability-gated if it ever lands.
- Voice virtualization (virtual voices waiting for a free slot): deferred until per-cue caps and handles exist; without handles it cannot be meaningful.

Deferred experiments (each behind an [Experimental] flag with a kill criterion before any stable API, per ADR-12): per-playback SFX handles (FadeTo, Pause, Resume, live position — pause-menu demand may surface it on its own), priority-based voice stealing with loudness tie-break, sustained duck gating on a playing category, per-play fade-in on one-shots.

The one guarded temptation: the analysis shows a managed mixer is cheap (150-line reverb, 115-line device layer, zero-new-native-dependency on DesktopGL). ADR-12 records why this is a trap for Duack anyway: the cost of owning playback was never the algorithm, it is the real-time guarantee, and accepting it would end the thin-adapter identity the README states on purpose. The ranked shortlist's own bottom line is adopted verbatim as a plan invariant: buses must strengthen the one-mix, one-knob promise, not start the middleware.
