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

#### 2026-10-16 (music director)

- `IMusicBackend` - the thin adapter contract the platform packages implement: overlap
  capability flag, `PlayTrack` (name/initial volume/cue pitch), active/outgoing **volume**
  setters, `Stop`/`StopOutgoing`, `IsTrackPlaying`, a `TrackEnded` completion event, and
  a per-frame `Update` hook. The setters take the FINAL composed volume, so adapters never
  multiply anything themselves.
- `MusicDirector` - backend-agnostic music playback core: drives a
  `MusicPlaylistController`, realizes both crossfade models (equal-power overlap for
  desktop backends via `CrossfadeMath`; fade-out/switch/fade-in sequencing for
  single-instance web backends via `MusicTransitionSequencer`), restarts a
  `Repeat=Track` (or single-track loop) after a natural end, applies the music
  `CueVariation` pitch on every start, and raises `TrackCompleted` after the playlist
  advances.
- `MusicDirector` now OWNS the full mix: every backend volume is
  `MusicVolumeMath.Effective(music master x the track's context scale x the live duck
  ratio x the crossfade ramp)`, composed in `MusicDirector` with an internal
  `DuckMixer` built from `MusicConfig.DuckSources`. The outgoing track keeps its OWN
  context scale while fading out, and `DuckRatio` is exposed for diagnostics. New
  passthroughs `Pulse(source, hold)` / `SetDuck(source, active)` let the game drive
  dialogue/SFX ducking through the director instead of a separate mixer. Pins:
  `MusicDirectorTests` (19 cases, fake backend - play/advance/repeat/crossfade both
   models/zero crossfade/cue pitch/stop/unknown playlist/idle updates plus master x
   context x duck composition and duck release).

#### 2026-10-16 (backend adapters)

- New `src/Duack.MonoGame` package (`Duack.MonoGame`): the desktop
  `IMusicBackend`. `SoundEffectMusicBackend` plays cached, NON-looped tracks through the
  headless-testable seams `IAudioClipLibrary` / `IAudioClip` / `IAudioInstance` (so the
  backend has no MonoGame dependency in its logic and its tests need no audio device).
  `SupportsOverlap` is true, so the core crossfades the outgoing track against the
  incoming one. Completion is detected by polling the instance and is reported exactly
  once via `TrackEnded`; looping is left to the core playlist (a natural end is a
  completion, not an instance `IsLooped`). The real library,
  `SoundEffectClipLibrary`, loads `Content/Audio/{name}.{ext}` through `TitleContainer`
  (OGG-first via the ported `OggPcmWav` NVorbis decoder, WAV fallback), caches successes,
  does NOT cache failures, retries once after a 50 ms pause for mid-copy files, and never
  throws. `Invalidate(name)` drops a cached clip for content hot-reload.
- New `src/Duack.KNI` package (`Duack.KNI`): the web `IMusicBackend` plus the browser
  settings storage. `MediaPlayerMusicBackend` rides the platform player (an HTML
  `<audio>` element on BlazorGL) through the injectable `IMediaPlayerHost` seam
  (`KniMediaPlayerHost` wraps the static `MediaPlayer` in production). `SupportsOverlap`
  is false, so the core uses its fade-out/switch/fade-in sequencing; the adapter plays
  NON-repeating and reports a natural end via `TrackEnded`. Track end is decided by the
  pure, headless-tested `MediaEndState`: an explicit stop latches the state out of
  "running" synchronously, and a fresh start opens a short settle window during which
  stop signals are swallowed, so the delayed "stopped" echo of a track switch is not
  mistaken for the new track ending. `LocalStorageKeyValueStorage` (ported from the
  game's `JsRuntimeKeyValueStorage`) is the only JS-interop surface: synchronous
  `[JSImport("globalThis.localStorage.*")]` bindings, `Get` swallows and logs a failure
  so a storage-disabled browser boots on authored defaults, `Set` logs and rethrows so a
  real save failure is never reported as success.
- New test suite `tests/Duack.Adapters.Tests` (27 cases, references both adapters): fake
  `IAudioClipLibrary`/clip/instance drive the MonoGame backend (start params, overlap
  demote/drop, outgoing stop, full stop, one-shot end), and a fake `IMediaPlayerHost`
  drives the KNI backend (ogg URI, non-repeating, volume, natural end once, no end on
  explicit stop, stale switch signal within the settle window, missed-event polling), with
  `MediaEndState` pinned directly (settle, latch-once, stopped-by-us, zero-settle).

### Known Limitations

#### 2026-10-16

- Web track-end detection (`Duack.KNI.MediaEndState`) is heuristic. The platform's
  `MediaStateChanged` signal carries no track identity and no reason, so a stale "stopped"
  event that arrives within the settle window of a track switch is correctly swallowed, but
  one that arrives later than the window can still be misread as a natural end. Tracks are
  minutes long and the window is a few frames, so this is not reachable by ordinary
  playback; it has not been verified on a real browser from this environment.
