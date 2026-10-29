# AGENTS.md

Duack is a small, backend-agnostic audio library (mixing, ducking, crossfade, player
settings) extracted from a production game and published as preview NuGet packages.

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
- **This preview is code-first.** The configuration surface is the C# API
  (`AudioConfigBuilder`, `PlaylistConfigBuilder`, model objects). Authored audio configuration
  has no `audio-config.json` compatibility layer in master; the old implementation remains on
  `legacy/pre-refactor`. JSON remains only for active persistence/tooling formats such as
  standalone playlists, audio manifests, and player settings.
- Playlists have their OWN source-generated context (`Duack.PlaylistJson`), so the AOT web
  head can read standalone playlist files at runtime; a game may author them in code with
  `PlaylistConfigBuilder`. The CLI's `PlaylistFile` delegates to `PlaylistJson`, so tool and
  runtime never drift on the format.

## Layout

- `src/Duack` — the library (pure BCL).
- `src/Duack.MonoGame` — desktop adapter: `IMusicBackend` over MonoGame `SoundEffectInstance`,
  plus `SoundEffectSfxBackend` (the core `ISfxBackend`) over the clip seams.
- `src/Duack.KNI` — web adapter: `IMusicBackend` over the platform `MediaPlayer`, plus the browser
  `localStorage` settings storage.
- `tests/Duack.Tests/CodeOnly` — behavior with no serialization surface.
- `tests/Duack.Tests/JsonRoundTrip` — active player-settings and manifest wire-format pins.
- `tests/Duack.Adapters.Tests` — behavior of the two adapter packages, driven through their
  headless seams (fake clip library / fake media-player host); no audio device or browser needed.
- `tools/DuackCli` — the desktop-only `duackcli` CLI (`convert`/`scan`/`normalize`); the only
  place `NAudio`/`OggVorbisEncoder` live. Never referenced by `src/` or `samples/`. See its
  `README.md` for the CLI and standalone playlist format.
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
  (checked-in `audio-manifest.json` from `duackcli scan`); kept out of `Duack.slnx` (CI may lack
  the WASM workload), so build it explicitly.

## Two-directory audio convention

An audio root holds `SFX/` (small one-shot sounds, wav preferred) and `Tracks/` (loops and
songs, ogg preferred) — users never register cue names by hand. `AudioLibrary.ScanDirectory`
classifies live where the filesystem is available (desktop); browsers cannot enumerate
directories, so `duackcli scan` writes the checked-in `audio-manifest.json` web hosts read back
via `AudioLibraryManifestJson` (source-generated, AOT-safe). Classification is
case-SENSITIVE on purpose (`SFX`/`Tracks` and lowercase `.wav`/`.ogg` only): case-sensitive
web hosts 404 on any mismatch, and `duackcli scan` warns about files it therefore cannot load.
The legacy flat layout (`Content/Audio/{name}.ogg|.wav`) remains a working fallback — the
last candidates in `AudioLibrary.CandidatePaths` order, and the exact probe order of
`SoundEffectClipLibrary.CandidateUris` when no library is supplied — so existing flat
content keeps loading unchanged.
## Build & test

```
dotnet build Duack.slnx
dotnet test Duack.slnx
```

The tool project uses a distinct assembly identity (`AssemblyName duackcli`): naming it bare
`duack` collides case-insensitively with the core `Duack.dll` on Windows, and MSBuild then
silently drops the tool assembly from the test project's references (`CS0103` against a type
that clearly exists). `duackcli` is both the assembly name and the command users invoke
(`ToolCommandName`); the package ID remains `DuackCli`.

To prove the core stays BCL-only, restore into an empty package cache:

```
$env:NUGET_PACKAGES = "$env:TEMP\duack-empty-cache"
dotnet test tests/Duack.Tests
```
