# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Detailed per-change notes live in [`.opencode/audit-log.md`](.opencode/audit-log.md).

## [Unreleased]

### Added

#### 2026-10-28

- **GitHub Pages workflow for the web sample.** New `.github/workflows/pages.yml` builds and
  publishes `samples/Duack.Sample.Web` (WASM workload) to GitHub Pages on push to `master` or
  manual dispatch, so the crossfade + ducking demo is viewable in a browser without a local
  build. Autoplay requires a click ("Start story") — documented up front.

### Changed

#### 2026-10-28

- **README adds a browser/WASM gotcha for copied Duack skills.** If a project targets the
  browser, keep the audio flow in Duack's C# API and adapters instead of re-implementing it in
  vanilla HTML/JS or raw browser audio code. Desktop builds are already C# and do not involve
  HTML/JS.
- **README clarifies the CLI is desktop-only build-time tooling**, not a runtime dependency:
  it runs on the dev machine and never ships to the browser; the web head reads the checked-in
  `audio-manifest.json`. README also notes that files the loader cannot open are reported (scan
  warnings + runtime `[Audio] Could not load ...` lines) rather than failing silently, adds a
  compact five-line config sample, an autoplay/user-gesture note, a "who adopts first"
  (MonoGame, then FlatRedBall2) answer, and an old-way-vs-new-way crossfade table.
- **Web sample README brought up to date**: it now documents `WebAudioSfxBackend` for
  overlapping one-shot cues instead of the obsolete "no sfx backend" note.
- **README gains a Samples table** with a Play column linking to the GitHub Pages live demo.
- **Web sample gains a live crossfade toggle.** Press `C` to flip `CrossfadeSeconds` between
  the authored value and 0 (instant hard cut); a corner indicator shows green (ramp) / red
  (hard cut). README points at it.
- **New `samples/Duack.Sample.Crossfade`** minimal showcase: click tracks or press 1/2/3/Space,
  click preset buttons for crossfade duration (4s/8s/12s/16s), toggle crossfade on/off.
  Config mirrors `WithCrossfadeSeconds` usage.
- **Crossfade demo UX clarified.** The player now presents a guided choose-track/play flow,
  labeled transport actions, larger touch targets, clearer transition and playback feedback,
  visible keyboard guidance, and an expandable explanation of the desktop and web transition
  paths. The UI remains a sample-only Blazor surface over Duack's C# API.
- **Added `crossfade-demo.html`** standalone WebAudio page (slider + play buttons) under the
  web sample's wwwroot for a lightweight feature showcase.

### Fixed

#### 2026-10-28

- **Added an HTML/Blazor control overlay to the Crossfade sample.** The overlay now mirrors the
  former two-column player layout with track list, previous/play/next controls, status, meter,
  crossfade toggle, and 4s/8s/12s/16s presets. All actions invoke the running C# `Game1` and
  Duack `MusicManager`; the canvas and browser/KNI audio path remain unchanged. No separate audio
  behavior test was added because this is a thin UI event bridge; existing `MusicManager` tests
  cover the delegated transition behavior.
- **Removed the legacy Crossfade canvas UI and static HTML legend.** The Blazor overlay is now the
  only visible control surface, avoiding duplicate track panels and conflicting mouse hit areas.
- **Made the Crossfade transition meter live.** It now reads transition phase, source/target tracks,
  elapsed seconds, total duration, and progress from `MusicManager` through C#; no browser audio
  timing or vanilla-JavaScript fade logic is involved.
- **Fixed the Crossfade sample's duration presets.** Per-track `FadeInSeconds = 0.4f`
  accidentally overrode `MusicManager`'s configured crossfade duration, making the 4s/8s/12s/16s
  controls switch tracks after only 0.4 seconds. Removing those per-track overrides restores the
  configured transition durations.
- **GitHub Pages now deploys the actual Blazor/KNI Crossfade sample at
  `/crossfade-demo/`.** The workflow previously published only the Story sample, so the
  documented Crossfade route was missing and the `.html` link opened the separate standalone
  vanilla-JavaScript showcase instead. The Blazor sample is now published as its own directory,
  preserving the configured 4s/8s/12s/16s Duack crossfade controls.
- **Renamed `AudioLibrary.SfxManageryName` to `AudioLibrary.SfxDirectoryName`.** The
  director→manager rename pass had accidentally rewritten the `SfxDirectoryName` constant
  (no "Director" substring intended) into `SfxManageryName`. Value (`"SFX"`) and behavior
  unchanged; the public constant now has the intended name. Any consumer referencing the
  misspelled name must update to `SfxDirectoryName`. Pins:
  `Convention_directory_constants_have_the_intended_names`.

#### 2026-10-29

- **`MusicManager` no longer reports a stale fade-out duration in transition telemetry on
  non-overlapping (web/KNI) backends.** `TransitionDurationSeconds` is `_fadeOutDuration +
  _transitionDuration` there, but the two standalone fade-in paths (first play with no track
  playing, and a `Repeat=Track` restart after a completed crossfade) never reset
  `_fadeOutDuration`, so first play reported `8.2s` instead of `8s` and a repeat-track
  fade-in reported `12s` (progress started at ~33%). Both paths now set `_fadeOutDuration = 0f`,
  matching the overlap backend. Regression tests:
  `Non_overlap_first_play_reports_only_the_fade_in_duration`,
  `Non_overlap_repeat_track_fade_in_does_not_inherit_the_previous_fade_out`.
  Also made `MusicTransitionSequencer.Advance` consume a delta across phase boundaries so a
  single large `Update()` can complete a multi-phase transition.

## [0.1.0-preview.4] - 2026-10-26

### Added
- **NVorbis `TotalSamples` underestimation pitfall documented.** The adapters skill now documents
  that `VorbisReader.TotalSamples` can underestimate decoded samples (VBR streams), causing
  `IndexOutOfRangeException` in decoders that pre-allocate from the reported total. `OggPcmWav.Decode`
  handles this by adding slack and growing on demand. Any custom decoder must do the same.
- **Release process documented.** `CONTRIBUTING.md` now has a "Releasing a new version" section
  with the full checklist (version bump, changelog, tag, push, verify). Added `tools/release.ps1`
  helper script with dry-run support.

### Changed
- **Music-related test files renamed for consistency.** `MusicDirectorTests` ��' `MusicManagerTests`,
  `MusicDirectorPlayResetTests` ��' `MusicManagerPlayResetTests`, `SfxDirectorTests` ��'
  `SfxManagerTests`, `MusicManagerFadeTests` (was `MusicManagerFadeTests`). Test bodies updated to
  use `MusicManager` naming. No production behavior changed.

#### 2026-10-26

- **Clip-based music/SFX backends moved to core.** `SoundEffectMusicBackend` and
  `SoundEffectSfxBackend` (along with the `IAudioInstance` / `IAudioClip` / `IAudioClipLibrary`
  seams) moved from `Duack.MonoGame` into `Duack` core. Both are pure BCL now and work against
  any `IAudioClipLibrary` implementation — desktop (SoundEffect) or web (WebAudio) — so a KNI
  desktop build can use the overlap crossfade path instead of `MediaPlayerMusicBackend`. The
  `SoundEffectClipLibrary` / `SoundEffectAudioClip` bindings stay in `Duack.MonoGame`. Adapters
  reference the moved types via the nested `Duack` namespace, so existing code keeps compiling.

#### 2026-10-25

- **JSON wire-format tests now live with the core suite.** `AudioConfigTests`,
  `PlaylistJsonTests`, and the config schema-version pins moved from the removed
  `tests/Duack.Config.Tests` project into `tests/Duack.Tests/JsonRoundTrip`. No production
  behavior changed; the tests still use only `src/Duack`.
- **README now documents the benefits over the built-in MonoGame/XNA audio path.** A comparison
  table covers crossfade/playlists, buses, ducking, cue variation, settings persistence,
  desktop/web parity, tooling, dependency footprint, and authoring. It also states what Duack
  deliberately does not do (editor/GUI, FMOD/Wwise replacement, playback engine).
- **Standardized the preview description on "code-first".** AGENTS.md, the audio-core skill, and
  the core JSON comments no longer say "code-only", which incorrectly implied the core has no
  JSON code. The JSON layer remains part of the core for tooling and persistence.
- **Per-user player-settings folder is no longer named after a consumer.**
  `FilePlayerSettingsStore.DefaultRootDirectory()` now uses the host application's entry-assembly
  name (`%APPDATA%\<assembly>` on Windows, `~/.config/<assembly>` elsewhere) and falls back to
  `Duack` when no entry assembly is available. Games that relied on the previous hardcoded folder
  must pass their existing root explicitly. Pinned by
  `FilePlayerSettingsStore_default_root_uses_the_host_application_name`.
- **Browser settings key is no longer consumer-specific.** `WebPlayerSettingsStore.DefaultKey`
  changed to `duack:playerAudioSettings`. Existing web builds that relied on the old default
  should pass their previous key to the constructor (or accept a one-time settings reset); the
  key remains overridable. Pinned by
  `WebPlayerSettingsStoreTests.Key_is_namespaced_so_it_cannot_collide_with_other_saved_data`.
- **Docs, comments, and internal notes are consumer-agnostic.** Removed the remaining
  consumer-project names from AGENTS/CONTRIBUTING, skills, tooling and sample comments, test
  comments, and the audit log. The only remaining upstream name is the required audio-attribution
  link in `samples/AUDIO_ATTRIBUTIONS.md`; supported-engine references (MonoGame/KNI/FlatRedBall2/
  FNA) are unchanged. The test comment that framed a duck-source reload around a hot-reload
  scenario was removed until a host needs that guidance.

#### 2026-10-26

- **Player settings documentation no longer describes the authored configuration as a shipped
  `audio-config.json` file.** Player preferences remain outside authored content and now refer to
  the code-first configuration surface.

### Fixed

#### 2026-10-25

- **`duackcli` help no longer advertises the retired `duack` command.** The usage banner and
  user-facing `[duack]` status prefixes now say `duackcli`, matching the packaged
  `ToolCommandName`. Pinned by `CliTests.No_arguments_prints_usage_and_fails` and
  `CliTests.Init_writes_a_config_next_to_the_content`.

### Deprecated

#### 2026-10-26

- **The legacy `audio-config.json` workflow is deprecated.** `AudioConfigJson`, the build-only
  `Tracks`/`ExtensionData` members, playlist embedding, and the CLI's `init`, `validate`,
  `convert --config`, and `normalize --config`/`--write-config` paths now emit deprecation guidance
  pointing to `legacy/pre-refactor`. Active authoring uses `AudioConfigBuilder` and
  `PlaylistConfigBuilder`; `duackcli convert` accepts standalone playlists or explicit `--all`,
  while `duackcli scan` and `normalize` remain available. Existing JSON APIs remain present as
  obsolete compatibility symbols and are covered by the round-trip tests.

### Removed

#### 2026-10-25

- **Removed the last references to the deleted `Duack.Config` project.** The sample projects
  still pointed at `src/Duack.Config/Duack.Config.csproj`, which produced MSB9008 warnings and
  made the build depend on a project that no longer exists. The JSON layer already lives in
  `src/Duack` (`AudioConfigJson`, `PlaylistJson`, `AudioConfigBuilderJsonExtensions`), so those
  references were dropped and the comments/docs/skills now point at the core. No runtime
  behavior changed. The previous separate-package layout remains on `legacy/pre-refactor` if a
  concrete JSON use case later justifies bringing it back.

### Security