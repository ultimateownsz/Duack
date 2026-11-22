# Duack vNext — Slice D — Build-time effects experiment (`Duack.Render`)

Index: `../README.md` · Shared evidence/ADRs/gates/math: `../SHARED.md`

`STATUS.md` (state and open questions) and `slice.md` (decisions) are added when this slice starts.

---

Evidence: weakest — gated on Gate 0 showing he needs ambient color he cannot bake himself today, and on Gate 1 for send/return. Draft the code behind these gates; do not ship before they clear.

Scope:
- New package `Duack.Render`: `EffectsProfileBuilder` (POCO builders, `Convolver`, `BiquadFilter` offline), renderer reading `Content/Audio`, writing variants next to sources, updating `audio-manifest.json` with cue → variant mappings, reporting per-profile byte cost and warnings.
- Cross-platform output: variants are rendered per platform — WAV for desktop SFX, OGG for web tracks (same split `duackcli convert` already handles). Profiles can mark variants per-platform, so a web build can skip heavy variants to control download size; the render report shows per-platform byte cost so the game dev decides what ships where.
- Runtime: `AudioConfigBuilder.WithBusEffect(bus, effectId, ...)`; cue resolution picks the variant file by name from the manifest. Compile-time safety: effect IDs come from a profile assembly shared between the build project and the game (typo = build error, not silent dry cue).
- Replacement mode first. Send/return mode only if Gate 1 passes (wet-only render + dual instance + wet volume as send).
- Offline convolution with real impulse responses is allowed and encouraged — build time can afford what runtime engines approximate.
- CLI orchestration (optional): a thin `duackcli render` wrapper that invokes the consumer's build project (or a compiled profile assembly), with incremental rebuilds keyed on input hashes (profiles, sources, impulse responses), so only changed cues re-render; it can chain the existing `convert`/`scan` steps in one pass. The CLI orchestrates; the library owns the DSP and the authoring surface. Added Nov 17, 2026, per maintainer request.
- Optional scaffolding: `duackcli init` (or a `dotnet new` template) generates the boilerplate build project and a starter profile in the consumer's solution — the CLI writes the starting C#, the consumer edits it from there. Scaffolding only; the CLI never becomes the authoring surface.
- MSBuild wiring optional: a BeforeBuild target running the game's build project. Keep it optional; no mandatory content pipeline.
- A sample A/Bing dry vs wet on an ambience cue, both backends, human-verified.

Non-goals: no runtime DSP, no capability flags, no editor, no JSON profiles, no changes to the adapters.

Acceptance:
- Unit tests: renderer output contains the expected tail (impulse-response tests); manifest mapping round-trips; byte-cost report accurate; determinism — same inputs produce identical files.
- The compile-time safety claim demonstrated in a test project (bad ID fails the build).

Definition of done: gates cleared, tests green, sample heard by a human, package named `Duack.Render` (naming per ADR-5), skills doc, README tooling section updated.
