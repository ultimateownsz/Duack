# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Detailed per-change notes live in [`.opencode/audit-log.md`](.opencode/audit-log.md).

## [Unreleased]

### Added

### Changed

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