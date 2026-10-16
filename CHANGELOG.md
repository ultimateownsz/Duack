# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While the version is `0.x`, the API may change between minor versions.

> **Working agreement:** every change to the codebase is recorded here under
> `## Unreleased`, within its appropriate category and a dated (`#### YYYY-MM-DD`)
> subsection before it lands. Fixed entries note the symptom and enough reproduction
> detail to re-verify (the regression tests guarding it) or recover the previous behavior.

## Unreleased

### Added

#### 2026-10-16

- Initial extraction of the audio mix core from MonsterProjectWeb's
  `MonsterProjectWeb.Audio` project: `AudioConfig` (model, JSON load/save, sanitize,
  extension-data preservation, snake_case player settings), `DuckMixer`,
  `MusicVolumeMath`, `CrossfadeMath`, `MusicTransitionSequencer`, `PlayerAudioSettings`,
  `PlayerSettings`, and the `IPlayerSettingsStore` trio (`FilePlayerSettingsStore`,
  `WebPlayerSettingsStore`, `InMemoryPlayerSettingsStore`).
- `MusicRetryPolicy` moved in from the game's `Common/Audio` (pure retry/backoff logic).
- Unit tests ported from the game's `MonsterProjectWeb.Tests/Audio` into two suites:
  `CodeOnly` (no JSON serialization surface) and `JsonRoundTrip` (pin the JSON wire
  format and unknown-section preservation).

#### 2026-10-16 (new API)

- `MarbleBag<T>` - a shuffle bag (random without replacement): every item is drawn once
  per cycle, then the cycle refills. Takes an optional `Random` so a seeded generator makes
  a playlist-shuffle sequence reproducible. Pins: permutation per cycle, auto-refill past a
  full cycle, `Draw`/`TryDraw` on an empty bag, deterministic same-seed sequences.
- `VolumeCurve` - perceptual slider-to-amplitude mapping. `ToAmplitude` is a quadratic
  approximation (0.5 slider - 0.25 amplitude) so the control feels linear to the ear;
  `ToSlider` is its exact inverse. Both clamp and treat NaN as silence.
- `CueVariation` / `CuePlay` - per-play pitch/volume jitter for sound effects (repeated
  footsteps, hits) so they don't sound machine-gunned. Pitch is clamped to the engine's
  [-1, 1] units, the volume scale is floored at 0, and the `Random` is injected for
  reproducible tests.
- `AudioConfigBuilder` - fluent builder that assembles an `AudioConfig` in code and runs
  `Sanitize` on `Build`, for tests/samples/first-run defaults.
- Schema-version stamp on both persisted documents (`AudioConfig` and
  `PlayerAudioSettings`, serialized as `schema_version`). A missing stamp reads as the
  current version, `Sanitize` normalizes a zero/legacy stamp up to 1, and a FUTURE stamp is
  preserved rather than silently downgraded.
