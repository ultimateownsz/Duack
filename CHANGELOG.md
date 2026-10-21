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

#### 2026-10-22

- **`duack convert` now passes pre-encoded `.ogg` files through.** A content directory may hold a
  cue that ships no WAV master (only an already-encoded OGG). There is nothing to encode, and
  dropping the file would lose the cue entirely, so `convert` copies such a file into the output
  verbatim; an output that already holds a newer copy of the same file is left alone. The summary
  line gained a `copied=<n>` counter next to `converted=`. This is what lets a build pipeline
  point the CLI at a whole `SFX/` or `Tracks/` directory and get a complete OGG set back, without
  the caller re-implementing the copy step. Pins: `Convert_copies_a_pre_encoded_ogg_through`,
  `Convert_keeps_a_newer_ogg_output_instead_of_copying_over_it`.

#### 2026-10-22 (code-only)

- **Code-only: the JSON wire format moves to the provisional `Duack.Config` package.**
  The mix is authored in C# via `AudioConfigBuilder` / `PlaylistConfigBuilder` (the core API).
  `AudioConfigJson`, `PlaylistJson`, the snake_case source-generated serialization contexts, and
  `AudioConfigBuilderJsonExtensions.WithPlaylists` move to the new `src/Duack.Config` project. The
  core (`src/Duack`) no longer exposes `AudioConfig.Parse/TryParse/Serialize/Load/TryLoad/Save`
  or `AudioConfig.OpenStream`; it keeps `AudioConfig`, `MusicConfig`, `SfxConfig`, `DuckSourceConfig`,
  `TrackConfig`/`LoudnormConfig`/`CompressConfig`, `ExtensionData` and `[JsonExtensionData]` for
  unknown-section preservation, and `Sanitize()` only. JSON round-trip tests live in
  `tests/Duack.Config.Tests`; the core suite (`tests/Duack.Tests`) runs with no `Duack.Config`
  reference and stays pure BCL (proven against an empty NuGet package cache). `Duack.Config` is
  `IsPackable=false` in this preview (consumed by project reference by the tooling and consumers
  that need file I/O). The CLI and samples now reference `Duack.Config` explicitly. AGENTS.md and
  README reframe the library as code-first with JSON as a secondary/provisional door.
- **The sample embeds playlists via the JSON extension method.**
  `SampleAudio.BuildConfig` now calls `WithPlaylists(playlists)` through `Duack.Config`'s
  extension; the desktop sample prints the serialized playlists via `PlaylistJson`.

### Changed

#### 2026-10-19 (duck diagnostics + hot-reload reload)

- **`MusicDirector.DuckSources` / `MusicDirector.ReloadDuckSources()`**: the director now exposes the
  live duck-source snapshot (`IReadOnlyList<DuckSourceView>`, delegating to `DuckMixer.Sources`) and
  a one-call reload of the sources currently in `MusicConfig.DuckSources`, delegating to
  `DuckMixer.ReloadSources`. Consumer migration (MonsterProjectWeb) needs both: its automation
  surface reads `.Duck.EffectiveRatio` / `.Duck.Sources` off a central mix object it no longer owns,
  and its hot reload mutates the duck-source dictionary in place and must re-apply it without
  tearing down the director. Pins: 2 new tests (`ReloadDuckSources_preserves_active_state_and_applies_new_params`,
  `DuckSources_exposes_live_state_for_diagnostics`).

### Fixed

#### 2026-10-19 (zero-hold duck pulse latched on forever)

- **A pulse with a resolved hold of 0 stuck the duck on permanently.** Symptom: every sound effect
  plays through `SfxDirector.Play`, which calls `MusicDirector.Pulse("sfx")` with no explicit hold -
  so the hold comes from the `sfx` duck source's `hold_seconds`. With the shipped `hold_seconds: 0`,
  `DuckMixer.Pulse` set `IsActive = true` and left `PulseRemaining` at 0, and `Update` only releases
  a source while `PulseRemaining > 0`. The duck therefore never released: music stayed at the ducked
  ratio for the rest of the session (only a scene switch's `ReleaseDucks()` or `ReleaseAll()` cleared
  it). Reproduction: `mixer.SetSource("sfx", ratio 0.3, hold 0); mixer.Pulse("sfx"); mixer.Update(0.016f)`
  used to leave `EffectiveRatio` pinned at 0.3 with `IsActive` true. Fix: a zero-hold pulse is a
  no-op - it neither ducks nor cancels an explicit `Set(name, true)` hold, since "duck and release
  immediately" is audible-free anyway once the frame's easing runs. Pins:
  `Zero_hold_pulse_is_a_no_op`, `Zero_hold_pulse_leaves_an_explicit_hold_alone` (the pre-existing
  `Pulse_holds_then_releases` / `Pulse_with_explicit_hold_overrides_config` cases are unchanged).
- **`PlayerSettings.ApplyOverrides` dropped the authored cue variation.** Symptom: an authored
  `Music.CueVariation` / `Sfx.CueVariation` (the per-play pitch/volume jitter that makes repeated
  hits and footsteps not sound machine-gunned) vanished as soon as a player had saved any volume
  setting, because `ApplyOverrides` rebuilds `MusicConfig`/`SfxConfig` from the saved blob and
  carried over only master/crossfade/contexts/duck sources/tracks/extension data. Cues played dead
  flat for those players only. Reproduction: author both cue variations, `store.Save(new
  PlayerAudioSettings { MusicMasterVolume = 0.25f })`, then `ApplyOverrides(authored, store)` -
  `effective.Music.CueVariation` used to be null. Pins:
  `ApplyOverrides_preserves_authored_cue_variation`, `ApplyOverrides_leaves_cue_variation_null_when_none_is_authored`.

#### 2026-10-19 (master bus)

- **Master bus (three-bus terminology)**: the composition chain is now
  `master bus x bus volume x context x duck x player settings`, with the master entering
  once at the top. `MusicDirector` takes a `masterVolume` parameter (default 1) and
  multiplies it into the music composition; `SfxDirector` takes `masterVolume` and
  `sfxBusVolume` and composes them into the per-play SFX volume. Both directors expose
  the buses as live get/set properties, so a settings slider applies on the next
  frame/play without rebuilding the director (same liveness the music bus already had
  through its config reference). `PlayerAudioSettings`
  gains a persisted `MasterVolume` (default 1 = full volume, backward compatible: a blob
  saved before this change has no `master_volume` key and deserializes to full volume).
  `PlayerSettings.ApplyOverrides` layers the saved master over the authored one, and
  `InMemoryPlayerSettingsStore` copies it. Duck sources target the music bus, under the
  master. Pins: 13 new tests (music composition, SFX composition, runtime bus changes,
  player layering, persistence round-trip, backward compat). No behavior change when
  master = 1.
- **Bus terminology and positioning docs**: README gains a "Buses" section (three default
  buses, the composition chain, ducks under the master), the FMOD/Wwise positioning note
  (Duack replaces ad-hoc indie audio code, not middleware), and the "No UI, ever"
  extensibility-boundary wording (the CLI is the first-party plugin surface).
  `audio-engine`, `player-settings`, and `ducking` skills updated to match. Also fixed
  the README/skill quickstarts that showed a non-existent `AudioConfigBuilder.Create()`
  lambda API — the real builder is `new AudioConfigBuilder()` with flat `With*` methods
  (verified against `src/Duack/AudioConfigBuilder.cs`).

#### 2026-10-19 (README + WP6 migration step)

- **`README.md`** — project landing page: positioning statement, "why Duack exists"
  (MonoGame issues #5472 / #6153 / #6828), code-first quickstart, music-context OBS
  framing, feature list, scope boundary, and install line. Documents the preview
  release channel.
- **`MonsterProjectWeb.BlazorGL` now directly references `Duack.KNI`** (WP6 step 2):
  the browser head gets its own explicit project reference to the KNI adapter, in
  addition to the transitive one through `MonsterProjectWeb.Common.Kni`, so the head
  declares its adapter dependency directly.

#### 2026-10-19 (agent skills documentation)

- **Agent Skills for Duack features**: added topic skills under duack-skills/ (the single
  source of truth) following the agentskills.io format, documenting how to use Duack's
  features: udio-engine, crossfade, ducking, playlists, sfx-variation,
  player-settings, udio-library, config, dapters, cli. The duack-getting-started
  skill was merged into udio-engine to avoid overlap. Skill names no longer carry a
  duack- prefix; the containing directory (duack-skills/) groups them, matching the
  FlatRedBall2 rb-skills/ convention: duack-skills/ is the single committed source of
  truth and consumers copy it into .claude/skills/ (or their tool's location) manually,
  exactly as the FlatRedBall2 README describes. Each skill is a single SKILL.md with YAML
  frontmatter (
ame, description) and focused instructions/examples. No production code
  changed; documentation only.

#### 2026-10-18 (two-directory audio convention)

- **Two-directory convention with no registration**: an audio root holds `SFX/` (small
  one-shot sounds, wav preferred) and `Tracks/` (loops/songs, ogg preferred); dropping a
  file in is all it takes. `AudioLibrary` (core, pure BCL) classifies relative paths
  (`FromFiles`) or scans the layout live (`ScanDirectory` — missing subdirectory is an
  empty side, missing root throws), exposes sorted/deduped `Sfx`/`Tracks` cue names,
  `IsSfx`/`IsTrack`, ordered `CandidatePaths` (owning directory first, legacy flat
  `{name}.ogg|.wav` fallback last — existing flat content keeps loading unchanged),
  `ToPlaylist`, and `BuildPlaylists` (one looping single-track playlist per track plus the
  bag-shuffled `music` playlist; name collision throws, empty library yields no playlists,
  per-track contexts via `ContextForTrack`). Classification is deliberately
  case-SENSITIVE (`SFX`/`Tracks`, lowercase extensions only) — case-sensitive web hosts
  404 on any mismatch. Pins: `AudioLibraryTests` (21).
- **`AudioLibraryManifest` + `AudioLibraryManifestJson`** — the checked-in
  `audio-manifest.json` web hosts read at runtime (browsers cannot enumerate
  directories): snake_case (`schema_version`, `sfx`, `tracks`), source-generated (AOT
  safe), missing stamp reads as current, a future stamp is preserved not downgraded,
  unknown fields survive a load→save cycle, explicit null lists normalize to empty,
  malformed JSON throws `JsonException`. `AudioLibrary.ToManifest`/`FromManifest`
  round-trip. Pins: `AudioLibraryManifestTests` (9).
- **`ISfxBackend` + `SfxDirector`** (core) — cue-name one-shot playback with automatic
  music ducking: a successful play pulses the music director's duck source (config hold),
  while unknown cues and backend refusals report false and never duck. `PlayNext` draws
  from a `MarbleBag<string>` (no back-to-back repeats). Pitch passes through in engine
  units, so "same sound, differently pitched" is one call. Pins: `SfxDirectorTests` (8).
- **`SoundEffectSfxBackend`** (Duack.MonoGame) — the MonoGame `ISfxBackend` over
  `IAudioClipLibrary`: non-looping instance per cue, pitch/volume clamped to engine
  ranges, finished instances reaped on the next play, `Dispose` stops everything.
  `SoundEffectClipLibrary` gains an optional `AudioLibrary` so its probes follow
  `CandidatePaths` order, with the exact legacy flat order when omitted (pure
  `CandidateUris` helper pins both). KNI is untouched (web stays music-only). Pins:
  `SoundEffectSfxBackendTests` + `SoundEffectClipLibraryTests` (9).
- **`duack scan <audioRoot> [--out <file>]`** — writes `<audioRoot>/audio-manifest.json`
  (or `--out`) from the two-directory layout; exits 1 on a missing root; warns (without
  failing) about files the loader can never open (wrong type or upper-cased extension).
  Pins: 5 CLI end-to-end cases in `CliTests`.
- **Samples rebuilt around the convention.** Desktop is now a scripted Pokémon-style
  storyline (walk → notice → dialog → battle → loop) driven by the pure, headless-pinned
  `StoryController` (new `tests/Duack.Sample.Tests`, 11 cases: scene transitions, emitted
  cue names pinned as literals against the shipped files, seeded `MarbleBag<float>` hit
  pitches, Space/R handling): `AudioLibrary.ScanDirectory` + `BuildPlaylists` wire scene
  music by track name, all cues play through `SfxDirector` (auto-duck), hits vary pitch
  from one `hit.wav`, the top strip visualizes `DuckRatio`. The web sample is
  manifest-driven music-only: it fetches the checked-in `wwwroot/Content/Audio/audio-manifest.json`
  (generated by `duack scan` against its own `Tracks/`), rebuilds the same `AudioLibrary`,
  and shows one button per track plus the `music` shuffle playlist and a duck-pulse
  button. Desktop content restructured to `SFX/` (4 wav) + `Tracks/` (3 ogg); the WAV
  masters moved to a gitignored `_masters/` (regenerate the OGGs with
  `duack convert _masters Content/Audio/Tracks --all`). Docs: both sample READMEs,
  `AUDIO_ATTRIBUTIONS.md` (real MonsterProjectWeb-sourced audio ships in the samples, with
  the per-file source table kept in the game repo), tools README (`scan`).
  Verified: `dotnet test Duack.slnx` green (355 tests across 4 suites), desktop + web
  samples build clean.
- Core additions are backward compatible: existing consumers (MonsterProjectWeb) keep the
  flat `Content/Audio` layout working through the preserved candidate fallback and are
  unaffected until they opt into `AudioLibrary`/`SfxDirector`.

#### 2026-10-18 (shared sample core)

- **`samples/Duack.Sample.Core`** — the engine-free sample core both heads now share, so
  desktop and web play the *same* storyline and the *same* mix by construction (decision:
  desktop stays MonoGame, web stays KNI; the demo logic is shared, not the engine):
  `StoryController`/`StoryScene`/`StoryFrame` moved here from the desktop sample (namespace
  `Duack.Sample.Core`, behavior unchanged — the 11 pinned cases in `Duack.Sample.Tests` follow
  it), plus **`SampleAudio`** — the wiring that was copy-pasted in `Game1` and `Index.razor`:
  `BuildPlaylists` (battle→`Battle`, everything else→`Overworld`) and `BuildConfig` (masters
  0.8/0.8, contexts, `sfx` duck source 0.3/0.06/0.4/0.35, 1.2 s crossfade). Pins:
  `SampleAudioTests` (5: playlist shape, context mapping, sfx-only library, config values,
  playlist embedding).
- **The web sample now runs the scripted storyline** (previously music-only buttons): "Start
  story" doubles as the browser user-activation gesture and begins the overworld track; the
  shared `StoryController` advances on the existing render-loop timer, "Advance"/"Restart"
  mirror Space/R, and scene music switches crossfade through `MediaPlayerMusicBackend` exactly
  as desktop crossfades through `SoundEffectMusicBackend`. Story cues surface as `sfx`
  duck-pulse + a cue readout (the `Duack.KNI` no-sfx-backend limitation below) using the same
  duck source and configured hold as desktop's `SfxDirector` auto-duck, so the mix reacts
  identically; scene/dialog/round/HP state renders from the shared controller. The manual
  per-track buttons and duck pulse stay for direct exploration.
- `Duack.Sample.Tests` now references `Duack.Sample.Core` instead of the desktop sample
  project (no MonoGame package flows into the headless suite anymore), and the desktop
  sample references the core. Verified: full suite green (360 tests across 4 suites), desktop
  and web samples build clean (`dotnet build` with `-c Release`).
- No production (`src/`) code changed in this step; the razor page is UI glue over the
  pinned core, so it carries no direct test (the shared behavior lives in
  `StoryController`/`SampleAudio`).

#### 2026-10-18 (sample polish + probe cleanup)

- **Cleanup**: removed the scratch `samples/probe2` project and `samples/Duack.Sample.Web/Probe.razor`
  left over from the Razor `@onclick` generation investigation (they were never part of the
  solution and are not referenced anywhere).
- **Core: `ISfxBackend.Stop()`** (additive) — stops every one-shot the backend is playing
  (a hard cut). Implemented in `SoundEffectSfxBackend` (stops + disposes all instances) and
  exposed through `SfxDirector.Stop()`. Used by the desktop sample to cut the long dialog cue
  when the battle-start sting plays, so it cannot bleed into the battle scene.
- **Fixed: `OggPcmWav.Decode` overflow on larger Vorbis streams** — `NVorbis.TotalSamples` is an
  estimate for some files (overworld.ogg reported fewer frames than it actually contains), so
  the fixed-size PCM buffer overflowed with "Index was outside the bounds of the array" and
  the track silently failed to load — which is why the battle→overworld crossfade never played
  (the overworld clip was null and `PlayTrack` returned early, leaving the battle track
  running). The decoder now grows the buffer on demand and rewrites the RIFF header with the
  actual decoded length, so any Vorbis stream decodes regardless of how accurate
  `TotalSamples` is.
- **Core: `SfxDirector.Play(..., duck = true)`** (additive, default true) — lets a caller
  play a cue without pulsing the music duck source. The desktop sample uses `duck: false` for
  the fainting hit (which also switches the scene to overworld), so the cue can no longer
  suppress the crossfade back to the overworld track; the web sample mirrors this by skipping
  the duck pulse on cues that carry a `Music` switch.
- **Sample: removed the marble-bag pitch variation from battle hits** — the ±0.3 engine-unit
  jitter (one `hit.wav`, differently pitched) did not read well in the showcase, so hits now
  play at natural pitch. `MarbleBag<T>` and the bag-shuffled `music` playlist are untouched
  (still core features). `StoryController` no longer takes a `Random` (the parameter was only
  there for the bag); `LastHitPitch` is now always 0. The two pins that asserted varying
  pitches were updated to assert natural pitch.
- **Sample: dialog cue cut on battle start** (see `ISfxBackend.Stop()` above).
- **Sample UX**: the top duck bar is now taller with a clearer color ramp (green → orange →
  red) and a pulsing highlight while ducking, so the ducking is readable at a glance; in battle
  the entity that gets hit flashes white and scales up, with a strike marker on the attacker, so
  traded blows are visible; hit cues now draw from a `MarbleBag<string>` over the discovered
  `hit*` cues, so adding `hit-2.wav`, `hit-3.wav`, … gives no-back-to-back variation with no
  code change (a single `hit` cue keeps playing "hit"); the dialog box now shows a speaker
  portrait, a big digit for the current line, placeholder text bars with a blinking caret, and
  the line pips, so the dialog reads as live speech; the duck meter is now a two-lane sidechain
  view (a MUSIC lane showing the duck ratio with tick marks, an SFX lane that pulses while a cue
  speaks, plus a sidechain connector), so the cause-and-effect of ducking reads as two audio
  tracks.
- **Web sample now mirrors the desktop storyline**: the KNI/Blazor head runs the same shared
  `StoryController` with Start/Advance/Restart buttons, scene state (walk progress, dialog line,
  HP bars, battle round, hit flash), a two-lane duck meter, and a hit-cue `MarbleBag<string>`
  readout — all driven through `MediaPlayerMusicBackend` (KNI). Story cues still surface as duck
  pulses (no web sfx backend), but the mix reaction and scene visuals now match the desktop
  sample. Added matching CSS (meter, HP bars, pips, hit-flash).
- **`WebAudioSfxBackend`** (Duack.KNI): new `ISfxBackend` adapter that plays one-shot cues through
  the browser WebAudio API (`AudioContext` + `AudioBufferSourceNode`), independent of MediaPlayer
  so SFX overlap the music track while `DuckMixer` dips the music volume. Cue buffers are
  preloaded at construction; `Play` stays synchronous and returns false for cues not yet loaded.
  Exposed through `IWebAudioNative` so the adapter is unit-testable headlessly (7 pinned cases
  in `Duack.Adapters.Tests`).
- **Fixed boot screen start**: the game loop now starts immediately on `DOMContentLoaded` (not
  waiting for Blazor's `initRenderJS`), and `startGame()` defers until the .NET dispatcher is
  ready if the user clicks before Blazor finishes booting. The boot-screen "Start" button now
  reliably launches the game and begins audio playback.
- Verified: full suite green (367 tests across 4 suites); desktop + web samples build clean.

### Known Limitations

#### 2026-10-18 (shared sample core)

- The web story demo has no real sfx playback (the `Duack.KNI` limitation above): story cues
  surface as duck pulses + a cue readout. The pulse uses the same `sfx` duck source and
  configured hold as the desktop `SfxDirector` auto-duck, so the mix reaction matches; only the
  one-shot audio is missing.

#### 2026-10-19 (duck release on scene switch)

- **Fixed delayed ducking on scene switch**: when transitioning between scenes (overworld→battle),
  any lingering duck from the previous scene's SFX (e.g., a dialog cue still playing) was bleeding
  into the incoming track's crossfade, making the ducking feel delayed. Added `DuckMixer.ReleaseAll()`
  and `MusicDirector.ReleaseDucks()` to immediately reset all duck sources (no easing tail) on scene
  switches. Both desktop and web samples now call `ReleaseDucks()` when `frame.SceneChanged` is true
  OR `frame.Music` is not null (covers the BattleStart cue, which has no Music switch but is still
  a scene change that should release any lingering dialog duck).
  Also added `DuckMixer.IsReady()` / `MusicDirector.IsDuckReady()` for callers that prefer to gate
  the visual scene change on natural duck recovery instead of an immediate reset. Two complementary
  patterns for consumers:
  1. **Hard reset** — call `ReleaseDucks()` on scene switch; duck recovers instantly, visual changes
     immediately. Best when you want the incoming track at full volume from frame one.
  2. **Natural gate** — skip `ReleaseDucks()` and gate the visual transition on `IsDuckReady()`;
     the duck eases back over its release tau, and the visual changes once it has recovered. Best
     when you want to preserve the duck's smooth release curve.
  Pins: `DuckMixerReleaseTests` (2), `DuckReadyTests` (3: ready when unducked, recovers after
  release, immediate after ReleaseAll).

#### 2026-10-19 (crossfade reset on Play)

- **MusicDirector.Play() now triggers transition immediately**: `Play()` calls `Update(0f)` after setting the
  playlist, so the crossfade starts on the same frame instead of waiting for the next `Update()` call.
  This ensures the audio track plays right after the last action in the previous scene. Pins:
  `MusicDirectorPlayResetTests` (1: Play triggers immediate transition).

#### 2026-10-19 (crossfade documentation)

- **Added `docs/Crossfades.md`**: comprehensive guide covering sequential crossfades between
  any number of tracks, desktop (overlap) vs web (sequenced) backends, playlists for automatic
  sequencing, ducking during transitions, the two complementary patterns (hard reset via
  `ReleaseDucks()` vs natural gate via `IsDuckReady()`), scene transitions in practice, and
  the full API quick reference.
- **Added `.claude/skills/duack/` and `.claude/skills/duack-crossfades/`**: agent skills
  (SKILL.md with YAML frontmatter) mirroring the markdown documentation for agent consumption.
  Skills are easier to update than static docs since the library is in preview (0.x) and the
  documented surface may change between releases. Each skill has `name` and `description`
  metadata for agent discovery.

#### 2026-10-19 (ConfigScaffolder nullable fix)

- **ConfigScaffolder.cs**: Fixed nullable warning CS8604 on line 35 (`tracks[key]` where `key` was inferred
  as `string?`). Added null-forgiving operator (`key!`) since the keys are already filtered by
  `Where(key => !string.IsNullOrEmpty(key))`. No behavior change.

#### 2026-10-19 (WebAudio SFX backend)

- **WebAudio SFX backend** added to `Duack.KNI` — one-shot cues now play through WebAudio
  alongside music on the web sample. The previous "no web sfx backend" limitation is resolved
  for the web sample; SFX still require a user gesture (boot-screen click) before audio plays
  due to browser autoplay policy.

#### 2026-10-19 (SFX stopAll iteration fix)

- **Fixed `stopAll()` in webaudio-sfx.js**: The `for...of` loop iterated over the live
  `playingSources` array, but `source.stop()` triggers `onended` which splices the array during
  iteration, causing some sources (e.g., the long dialog cue) to be skipped and keep playing into
  the next scene. Now iterates over a copy (`[...playingSources]`) and clears the array at the
  end, ensuring all one-shots are stopped before the next audio track begins.
- **Faster duck recovery in sample config**: Reduced `HoldSeconds` (0.4→0.15) and `ReleaseSeconds`
  (0.35→0.1) in `SampleAudio` so the music ducks briefly and recovers quickly, preventing SFX
  overlap with the next audio track. Updated `SampleAudioTests` accordingly.

#### 2026-10-18

#### 2026-10-17

- Samples and repo hygiene (WP5): `samples/Duack.Sample.Desktop` (MonoGame DesktopGL demo:
  looping song, context switch crossfade, duck pulse keyed, perceptual-curve clear indicator)
  and `samples/Duack.Sample.Web` (BlazorWASM/KNI demo driving `MediaPlayerMusicBackend`
  directly, no FlatRedBall2 dependency), each with a README and one-line `dotnet run` command.
  `LICENSE` (MIT), `CONTRIBUTING.md`, and `.github/workflows/ci.yml` (build all projects, run
  all tests headless). Verified: `dotnet build Duack.slnx` and `dotnet test Duack.slnx` both
  green (292 tests), desktop sample builds clean, web sample builds clean.

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

#### 2026-10-17 (tooling package)

- New desktop-only `tools/Duack.Tools` project publishing the `duack` CLI
  (`AssemblyName Duack.Tools`; `duack` is only the packaging command name). It is the single
  home for `NAudio` (2.2.1) and `OggVorbisEncoder` (1.2.2) and is never referenced by `src/`
  or `samples/`. Three verbs:
  - `duack init <contentDir> [--out <file>] [--force]` - scaffold a starting
    `audio-config.json` from the discovered `*.wav` masters (default sections + `sfx`/
    `dialogue` duck sources + one placeholder `tracks` entry per WAV); refuses to overwrite
    without `--force`.
  - `duack validate <config.json> [--playlists <file>]` - report schema/range problems with
    path-qualified messages; exits non-zero only on an **error** (warnings exit zero). Never
    mutates the file, so a hand-edited config is checkable before the runtime `Sanitize`
    clamps it.
  - `duack convert <contentDir> <outDir> [--config <file>] [--playlists <file>]
    [--quality <0..1>] [--force] [--all]` - encode the SONGS to OGG Vorbis for the web build
    (SFX stay WAV: they are short and small, so converting them buys nothing), skipping
    outputs already newer than their WAV. The **playlist is the authoritative song list**, so
    a playlist source (`--playlists`, or an embedded `playlists` section in `--config`) is
    **required**; `--all` is the explicit opt-in to convert every WAV (SFX included) and is
    mutually exclusive with a playlist source.
- Pure, individually-tested policy pieces: `ConfigValidator` (issues with severity,
  range/path checks, playlist-context cross-check), `SongSet` (case/extension-insensitive
  membership), `ConversionPlanner.Decide` (`Convert`/`SkipUpToDate`/`SkipNotSong`),
  `ConfigScaffolder.Build`, and `WavToOggConverter` (ported from the game's
  `tools/AudioConverter`; `ClampQuality`, FNV-1a serial derived from the output path for
  byte-deterministic output). CLI is a thin `Cli.Run(args, output, error)` dispatcher over
  them, so the end-to-end behavior (exit codes + filesystem effects) is test-coverable
  without a console.
- `PlaylistFile` loads a `PlaylistConfig` from a standalone playlist JSON or from an embedded
  `playlists` section in an audio config's `ExtensionData`. This documents and relies on the
  **ExtensionData round-trip contract**: build-only sections such as `playlists` are absent
  from the runtime model yet survive `AudioConfig` load->save because unknown properties are
  captured and re-emitted. An external editor against this tooling layer MUST preserve unknown
  sections (see `tools/Duack.Tools/README.md`).
- New test suite `tests/Duack.Tools.Tests` (57 cases): validator rules, song-set/planner
  decisions, scaffold shape, WAV->OGG round trip in a temp directory, playlist loader
  (standalone + embedded, string/numeric enums, comments/trailing commas), and CLI
  end-to-end for `init`/`validate`/`convert`.
- Assembly identity note: the tool assembly was initially named `duack`, which collides
  case-insensitively with the core `Duack.dll` on Windows; MSBuild then silently dropped the
  tool assembly from the test project's references (symptom: `CS0103` against a type that
  clearly existed). Renamed to `AssemblyName Duack.Tools`.

#### 2026-10-17 (playlist authoring in code)

- `PlaylistJson` - the playlist JSON wire format moved into core and made source-generated
  (not reflection) so the AOT web head can read playlists at runtime. `Parse`/`TryParse`
  (a standalone `{ "playlists": { ... } }` document), `Serialize`, `ToElement`/`FromElement`
  (the bare map stored under an audio config's `playlists` key), and `FromConfig`/`Embed`
  (read/write that section through `AudioConfig.ExtensionData`). Snake_case keys with string
  enum values (`Playlist`, `Bag`, ...); reads are case-insensitive and also accept numeric
  enums, comments, and trailing commas.
- `PlaylistConfigBuilder` (plus `PlaylistDefinitionBuilder`) - fluent code authoring of a
  `PlaylistConfig`, mirroring `AudioConfigBuilder`, so a game can define playlists in C#
  instead of JSON. `AudioConfigBuilder.WithPlaylists(PlaylistConfig)` embeds them, so
  `Build()` then `AudioConfig.Serialize()` produces a config the tooling reads back. This is
  the "define in code, convert from JSON" loop the game pipeline uses.
- `duack`'s `PlaylistFile` now delegates to `PlaylistJson`, so the tool and the runtime share
  ONE wire-format definition (no drift); behavior is unchanged (its tests pass untouched).
- The new CLI end-to-end pin `Convert_uses_code_built_playlists_serialized_to_a_config`
  covers code-authoring -> serialize -> `duack convert` (grows `tests/Duack.Tools.Tests` to
  58). Core gains `PlaylistJsonTests` (10: round-trip, snake_case, lowercase/numeric enums,
  comments, embedded map through an `AudioConfig`, malformed JSON) and
  `PlaylistConfigBuilderTests` (6: ordering, defaults, modes, empty, replace, fluency).

### Known Limitations

#### 2026-10-16

- Web track-end detection (`Duack.KNI.MediaEndState`) is heuristic. The platform's
  `MediaStateChanged` signal carries no track identity and no reason, so a stale "stopped"
  event that arrives within the settle window of a track switch is correctly swallowed, but
  one that arrives later than the window can still be misread as a natural end. Tracks are
  minutes long and the window is a few frames, so this is not reachable by ordinary
  playback; it has not been verified on a real browser from this environment.

#### 2026-10-17

- Playlists are authored in the game's `audio-config.json` under `playlists` (decided: one file
  drives both playback and conversion), but MonsterProjectWeb has not written that section yet.
  Until it does, its web build must call `duack convert ... --all`, so the song-vs-SFX split
  (SFX staying WAV) does not engage for the game yet. Authoring the playlist is part of the WP6
  migration.
