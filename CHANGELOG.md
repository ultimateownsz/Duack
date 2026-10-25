# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Detailed per-change notes live in [`.opencode/audit-log.md`](.opencode/audit-log.md).

## [Unreleased]

### Added

### Changed

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

### Fixed

#### 2026-10-25

- **`duackcli` help no longer advertises the retired `duack` command.** The usage banner and
  user-facing `[duack]` status prefixes now say `duackcli`, matching the packaged
  `ToolCommandName`. Pinned by `CliTests.No_arguments_prints_usage_and_fails` and
  `CliTests.Init_writes_a_config_next_to_the_content`.

### Deprecated

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