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
- **Phase 1 is code-only.** The configuration surface is the C# API
  (`AudioConfigBuilder`, `PlaylistConfigBuilder`, model objects). The JSON wire format lives
  in the separate **provisional** package `src/Duack.Config` (not part of the `Duack` package's
  public contract for this preview): `AudioConfigJson` / `PlaylistJson`, the snake_case
  serialization contexts, and the playlist-embedding builder extension. The tooling
  (`tools/Duack.Tools`) and consumers that need file-based config reference `Duack.Config`
  explicitly; the core stays serialization-free beyond the attributes it already carries for
  extension-data preservation. JSON round-trip tests live in `tests/Duack.Config.Tests`; the
  core test suite (`tests/Duack.Tests`) must not require `Duack.Config`. When the preview ships
  the JSON door will become a documented, supported surface (and may be folded back into the
  core or published as its own package at that time).
- Playlists have their OWN source-generated context (`Duack.PlaylistJson`), so the AOT web
  head can read them at runtime; a game may author them in code with `PlaylistConfigBuilder`
  and embed them via `AudioConfigBuilderJsonExtensions.WithPlaylists` (Duack.Config). The CLI's
  `PlaylistFile` delegates to `PlaylistJson`, so tool and runtime never drift on the format.

## Layout

- `src/Duack` — the library (pure BCL).
- `src/Duack.MonoGame` — desktop adapter: `IMusicBackend` over MonoGame `SoundEffectInstance`,
  plus `SoundEffectSfxBackend` (the core `ISfxBackend`) over the clip seams.
- `src/Duack.KNI` — web adapter: `IMusicBackend` over the platform `MediaPlayer`, plus the browser
  `localStorage` settings storage.
- `tests/Duack.Tests/CodeOnly` — behavior with no serialization surface.
- `tests/Duack.Tests/JsonRoundTrip` — config/player-settings wire-format pins.
- `tests/Duack.Adapters.Tests` — behavior of the two adapter packages, driven through their
  headless seams (fake clip library / fake media-player host); no audio device or browser needed.
- `tools/Duack.Tools` — the desktop-only `duack` CLI (`init`/`validate`/`convert`/`scan`); the only
  place `NAudio`/`OggVorbisEncoder` live. Never referenced by `src/` or `samples/`. See its
  `README.md` for the CLI and the `ExtensionData` round-trip contract.
- `tests/Duack.Tools.Tests` — the tool's pure logic plus CLI end-to-end (exit codes +
  filesystem effects) against temp directories.
- `samples/Duack.Sample.Core` — the engine-free sample core both heads share: the scripted
  storyline (`StoryController`) and the demo wiring (`SampleAudio`), pure BCL over `src/Duack`.
- `samples/Duack.Sample.Desktop` — MonoGame storyline demo (walk → dialog → battle) over the
  two-directory convention; the storyline/wiring come from `Duack.Sample.Core`, pinned by…
- `tests/Duack.Sample.Tests` — …the sample test project (references the core only; headless,
  no graphics device).
- `samples/Duack.Sample.Web` — BlazorWASM/KNI music + storyline demo (same `Duack.Sample.Core`
  as desktop; story cues play through `WebAudioSfxBackend` (WebAudio `AudioBufferSourceNode`),
  overlapping music while `DuckMixer` dips the mix). Manifest-driven
  (checked-in `audio-manifest.json` from `duack scan`); kept out of `Duack.slnx` (CI may lack
  the WASM workload), so build it explicitly.

## Two-directory audio convention

An audio root holds `SFX/` (small one-shot sounds, wav preferred) and `Tracks/` (loops and
songs, ogg preferred) — users never register cue names by hand. `AudioLibrary.ScanDirectory`
classifies live where the filesystem is available (desktop); browsers cannot enumerate
directories, so `duack scan` writes the checked-in `audio-manifest.json` web hosts read back
via `AudioLibraryManifestJson` (source-generated, AOT-safe). Classification is
case-SENSITIVE on purpose (`SFX`/`Tracks` and lowercase `.wav`/`.ogg` only): case-sensitive
web hosts 404 on any mismatch, and `duack scan` warns about files it therefore cannot load.
The legacy flat layout (`Content/Audio/{name}.ogg|.wav`) remains a working fallback — the
last candidates in `AudioLibrary.CandidatePaths` order, and the exact probe order of
`SoundEffectClipLibrary.CandidateUris` when no library is supplied — so existing flat
content (MonsterProjectWeb) keeps loading unchanged.
## Build & test

```
dotnet build Duack.slnx
dotnet test Duack.slnx
```

The tool projects carry a distinct assembly identity (`AssemblyName Duack.Tools`): a bare
`duack` collides case-insensitively with the core `Duack.dll` on Windows, and MSBuild then
silently drops the tool assembly from the test project's references (`CS0103` against a type
that clearly exists). `duack` is a packaging concern (`ToolCommandName`), not the assembly name.

To prove the core stays BCL-only, restore into an empty package cache:

```
$env:NUGET_PACKAGES = "$env:TEMP\duack-empty-cache"
dotnet test tests/Duack.Tests
```
