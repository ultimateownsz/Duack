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
- `cue_variation` is configurable per section, on BOTH `MusicConfig` and `SfxConfig`, so
  per-play pitch/volume jitter (`CueVariation`) applies to music cues and sound-effect cues
  alike. Optional and omitted from JSON when unset (no churn on existing configs);
  `Sanitize` normalizes negative ranges; `AudioConfigBuilder` gains
  `WithMusicCueVariation`/`WithSfxCueVariation`. `CueVariation` is now JSON round-trippable
  (`[JsonConstructor]`). The WP1.6 primitives are documented as dual-purpose: `MarbleBag<T>`
  shuffles a music playlist AND a sound-effect variant pool, and `VolumeCurve` maps any
  volume control (music or SFX).

#### 2026-10-16 (playlist)

- `PlaylistConfig` - named playlists (a `PlaylistDefinition` per name): an ordered list of
  `PlaylistTrack` (a name plus a music-context tag), a `PlaylistRepeatMode`
  (`Off`/`Playlist`/`Track`) and a `PlaylistShuffleMode` (`Off`/`Bag`).
- `MusicPlaylistController` - the backend-agnostic playlist sequencer. It owns the sequencing
  decisions (which track is current, when a completion advances, how repeat and shuffle
  behave) and exposes `Play(playlistName)`, `Next()`, `Stop()`, `CurrentTrack`, and a
  `TrackCompleted` event. The adapter owns playback and completion detection: it calls
  `NotifyTrackCompleted()` when the current track ends, then starts whatever `CurrentTrack`
  names. Built on neither `MediaQueue` nor `MediaPlayer` (that queue cannot be changed at
  runtime and does not exist on the web backend), so desktop and web share one sequencing
  path. `Play` on an unknown/empty playlist is a no-op (never throws, so a stray name cannot
  crash a game); a `TrackCompleted` handler reading `CurrentTrack` sees the successor (or
  null on stop); shuffle is seeded via the injected `Random`.
- `MarbleBag<T>` gains an opt-in `avoidImmediateRepeat` flag: when set, a draw never repeats
  the previous item across a cycle boundary, so a shuffled playlist loops without a
  back-to-back repeat at the seam. Off by default (plain shuffle-bag behavior unchanged);
  best-effort with duplicate items.
- Crossfade + ducking integration contract pinned in `PlaylistCrossfadeIntegrationTests`:
  a playlist advance is realized with the existing crossfade curve
  (`MusicTransitionSequencer`/`CrossfadeMath`) and a concurrent duck source stays audible and
  in range mid-transition via `MusicVolumeMath.Effective`.
