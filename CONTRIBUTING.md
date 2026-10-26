# Contributing to Duack

Duack is a config-driven audio mixing library for XNA-family engines (MonoGame, KNI,
FlatRedBall2), extracted from a production game. It ships as preview (`0.x`): the API may
change between releases.

## Before you contribute

- Read `AGENTS.md`: every code change is recorded in `CHANGELOG.md` under `## Unreleased`,
  in the appropriate `### Added` / `### Changed` / `### Fixed` / `### Known Limitations`
  category and a dated `#### YYYY-MM-DD` subsection, before it lands. For fixes, record the
  symptom plus enough reproduction detail to re-verify or revert.
- Read the project handoff plan (the Duack extraction work order): it fixes the phase order
  (WP0 inventory → WP1 core → WP2 playlist → WP3 adapters → WP4 tooling → WP5 samples → WP6
  consumer migration). Do not skip phases.
- The library targets `net8.0`/`net10.0` pure BCL in `src/Duack`; adapters live in
  `src/Duack.MonoGame` and `src/Duack.KNI`; offline tooling (NAudio/OggVorbisEncoder) lives
  only in `tools/DuackCli`. Never add a package reference to `src/Duack`.

## Test-driven development

Any refactor of existing behavior and any creation of new behavior is written test-first:

1. **Red** — write a failing test that pins the expected behavior. Run it and confirm it
   fails for the reason you expect.
2. **Green** — implement the minimum production change to make it pass.
3. **Refactor** — clean up, then re-run the whole suite (`CodeOnly`, `JsonRoundTrip`,
   `Adapters.Tests`, `Tools.Tests`), not just the new test.

A change that ships with no test needs a written reason in the changelog entry.

## Releasing a new version

The Duack release is **tag-driven**: pushing a `v*` tag triggers the GitHub Actions
workflow (`.github/workflows/release.yml`), which builds, tests, packs, and publishes
to NuGet. The critical rule: **the `<Version>` in `Directory.Build.props` must match the
git tag**, or the publish silently no-ops (a guard step now fails the release if they
disagree).

### Checklist

1. Make your code changes (TDD: red -> green -> refactor).
2. Update `CHANGELOG.md` under `## [Unreleased]` (Keep a Changelog format). Detailed
   notes go in `.opencode/audit-log.md`.
3. Bump `<Version>` in `Directory.Build.props` to the new version (e.g. `0.1.0-preview.4`).
4. Run the full suite: `dotnet test Duack.slnx`
5. Commit, tag, and push:
   ```powershell
   git add -A
   git commit -m "feat: description"
   git tag v0.1.0-preview.4
   git push origin master
   git push origin v0.1.0-preview.4
   ```
6. Monitor the workflow: `gh run list --limit 3`
7. Verify on NuGet: https://www.nuget.org/packages/Duack

Or use the helper script: `.\tools\release.ps1 -Version 0.1.0-preview.4`
(Dry run: `.\tools\release.ps1 -Version 0.1.0-preview.4 -DryRun`)

### Versioning
- Preview releases use `0.x.0-preview.N` (e.g. `0.1.0-preview.4`).
- The version in `Directory.Build.props` is the single source of truth for `dotnet pack`.
- The git tag must be `v{version}` (e.g. `v0.1.0-preview.4`).

## Build and test

```
dotnet build Duack.slnx
dotnet test Duack.slnx
```

To prove the core stays BCL-only, restore into an empty package cache:

```
$env:NUGET_PACKAGES = "$env:TEMP\duack-empty-cache"
dotnet test tests/Duack.Tests
```

## Scope boundaries

- **Do not fold an audio mixer UI into Duack.** Editor-adjacent tooling stays in the host game;
  only `tools/AudioConverter` (as `duackcli convert`) migrated here.
- **Do not delegate playlist state to `MediaQueue` or `MediaPlayer`.** Duack's own
  `MusicPlaylistController` is the single sequencing path for both backends.
- **Mobile is out of scope** until further notice. Desktop (MonoGame DesktopGL) and web
  (KNI/WASM) are first-class.

## Naming

- Core package ID: `Duack`. Adapters: `Duack.MonoGame`, `Duack.KNI`. Tool: `DuackCli`.
- Root namespace: `Duack` (core), `Duack.MonoGame` / `Duack.KNI` (adapters).
- The API term is **context** (OBS-style "scene" framing in docs only). Do not rename it to
  "Scene" in code.

