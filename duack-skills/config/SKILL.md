---
name: config
description: Duack configuration — AudioConfig/AudioConfigBuilder, JSON wire format via Duack.Config (snake_case), SchemaVersion, Sanitize clamping, ExtensionData round-trip, source-generated contexts, host-driven hot reload (no built-in file watcher), Tracks build-only section. Use when editing audio-config.json, authoring config in code, validating config, or preserving unknown fields.
---

# Duack — Configuration

## Two doors, same objects

This preview is **code-first**: author the mix in C# via `AudioConfigBuilder`. The JSON file
is a secondary door via the separate **provisional** package `Duack.Config`
(`AudioConfigJson` / `PlaylistJson`). The core has no JSON API.

Code-first:
```csharp
var config = new AudioConfigBuilder()
    .WithMasterVolume(1f)
    .WithMusicMasterVolume(0.9f)
    .WithSfxMasterVolume(0.7f)
    .WithCrossfadeSeconds(1.5f)
    .WithContext("battle", 0.8f)
    .WithDuckSource("sfx", new DuckSourceConfig { Ratio=0.25f, AttackSeconds=0.06f, ReleaseSeconds=0.35f })
    .WithMusicCueVariation(new CueVariation(0.1f, 0.05f))
    .WithSfxCueVariation(new CueVariation(0.15f, 0.1f))
    .WithPlaylists(playlists)  // via Duack.Config extension
    .Build();
```

JSON (`Duack.Config`): `AudioConfigJson.Load()` / `TryLoad()` read `Content/Audio/audio-config.json`;
`AudioConfigJson.Parse(json)` / `TryParse`; `AudioConfigJson.Serialize(config)`; `AudioConfigJson.Save(config, path)`.

## Schema & versioning

- `AudioConfig.CurrentSchemaVersion = 1`.
- `SchemaVersion` field (serialized `schema_version`). Missing → reads as current (1).
  `Sanitize()` normalizes 0/legacy up to 1 and preserves a higher (future) stamp.
- Unknown/absent version loads with a warning; higher-than-known fails loudly (tooling).

## Sanitize() clamping

- MasterVolume, Music.MasterVolume, Sfx.MasterVolume → [0,1].
- Music.CrossfadeSeconds → [0.1, 10].
- Contexts → [0,1].
- Duck sources: Ratio [0,1], Attack/Hold/Release >= 0.
- CueVariation rebuilt (ranges normalized).
- SchemaVersion normalized.

`PlayerAudioSettings.Sanitize()` same bounds, NaN → low bound.

## JSON wire format

- Source-generated contexts (no reflection; WASM/trimming safe): `AudioConfigJsonContext`,
  `PlaylistConfigJsonContext`, `PlayerAudioSettingsJsonContext`, `AudioLibraryManifestJsonContext`
  (all in `Duack.Config` except `PlayerAudioSettingsJsonContext` which lives with the core
  settings).
- Snake_case keys, case-insensitive on read, indented write.
- `Tracks` section (build-only loudness/compress targets) null-omitted on write.
- `ExtensionData` (`[JsonExtensionData]`) preserves unknown sections/fields across load→save.
- `AudioConfigJson.OpenStream` defaults to `File.OpenRead`; games replace with
  `TitleContainer.OpenStream` so the same relative path resolves on DesktopGL and Blazor.

## Hot reload (host integration)

Duack provides the pieces to support hot reload if your host wants it: re-parse via
`AudioConfigJson.Load()` / `TryLoad()` or `Parse()`, apply player overrides with
`PlayerSettings.ApplyOverrides(authored)`, and tell the music director to refresh duck
sources with `MusicDirector.ReloadDuckSources()` (duck state is preserved; existing sources
swap params in place). Duack has **no built-in file watching** — file monitoring is the
host's responsibility (and varies by engine; not standard in MonoGame/KNI).

## Tracks section (build-only)

`Dictionary<string, TrackConfig>` with `LoudnormConfig` (I/Tp/Lra) and optional
`CompressConfig`. Consumed by tooling; ignored at runtime but preserved on save.

## OpenStream

`AudioConfigJson.OpenStream` (in `Duack.Config`) defaults to `File.OpenRead`; games replace
with `TitleContainer.OpenStream` so the same relative path resolves on DesktopGL and Blazor.

## Tips

- `TryParse` returns false instead of throwing (raw-JSON editors).
- `Load()` falls back to defaults when file missing.
- Save is no-op on browser (`OperatingSystem.IsBrowser()`).

See also: `cli` (validate), `player-settings`, `audio-engine`.
