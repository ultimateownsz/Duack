# AGENTS.md

Duack is a small, backend-agnostic audio library (mixing, ducking, crossfade, player
settings) extracted from a FlatRedBall2 game and consumed by that game, an Avalonia tool,
and (later) published preview NuGet packages. It is the sibling of these repositories:

- Engine of record: `C:\Users\danil\RiderProjects\FlatRedBall2` (FRB2, net10.0).
- Consumer game: `C:\Users\danil\RiderProjects\MonsterProjectWeb`.

## Changelog discipline

Every code change is recorded in `CHANGELOG.md` under `## Unreleased`, in the appropriate
`### Added` / `### Changed` / `### Fixed` / `### Known Limitations` category and a dated
`#### YYYY-MM-DD` subsection before it lands. For fixes, record the symptom plus enough
reproduction detail to re-verify or revert. Do not edit code without reflecting it there.

## Test-Driven Development (non-negotiable)

Any refactor of existing behavior and any creation of new behavior is written test-first:

1. **Red** — write a failing test that pins the expected behavior. Run it and confirm it
   fails for the reason you expect.
2. **Green** — implement the minimum production change to make it pass.
3. **Refactor** — clean up, then re-run the whole suite (both `CodeOnly` and
   `JsonRoundTrip`), not just the new test.

A change that ships with no test needs a written reason in the changelog entry.

## Hard architectural rules

- `src/Duack` is **pure BCL** (`System.Text.Json` only). It must build and test with no
  engine/XNA/MonoGame/KNI package present. Never add a package or project reference to it.
- Backend glue (engine, XNA, browser interop) lives in adapter packages, not the core.
- The JSON wire format is part of the public contract: `JsonRoundTrip` tests pin it,
  and unknown sections/fields must survive a load→save cycle (`[JsonExtensionData]`).

## Layout

- `src/Duack` — the library (pure BCL).
- `src/Duack.MonoGame` — desktop adapter: `IMusicBackend` over MonoGame `SoundEffectInstance`.
- `src/Duack.KNI` — web adapter: `IMusicBackend` over the platform `MediaPlayer`, plus the browser
  `localStorage` settings storage.
- `tests/Duack.Tests/CodeOnly` — behavior with no serialization surface.
- `tests/Duack.Tests/JsonRoundTrip` — config/player-settings wire-format pins.
- `tests/Duack.Adapters.Tests` — behavior of the two adapter packages, driven through their
  headless seams (fake clip library / fake media-player host); no audio device or browser needed.

## Build & test

```
dotnet build Duack.slnx
dotnet test Duack.slnx
```

To prove the core stays BCL-only, restore into an empty package cache:

```
$env:NUGET_PACKAGES = "$env:TEMP\duack-empty-cache"
dotnet test tests/Duack.Tests
```
