---
name: config
description: Duack configuration — AudioConfig/AudioConfigBuilder, JSON wire format (snake_case), SchemaVersion, Sanitize clamping, ExtensionData round-trip, source-generated contexts, hot reload, Tracks build-only section. Use when editing audio-config.json, authoring config in code, validating config, or preserving unknown fields.
---

# Duack — Configuration

## Two doors, same objects

Code-first:
```csharp
var config = AudioConfigBuilder.Create()
    .WithMasterVolume(1f)
    .WithMusicMasterVolume(0.9f)
    .WithSfxMasterVolume(0.7f)
    .WithCrossfadeSeconds(1.5f)
    .WithContext("battle", 0.8f)
    .WithDuckSource("sfx", new DuckSourceConfig { Ratio=0.25f, AttackSeconds=0.06f, ReleaseSeconds=0.35f })
    .WithMusicCueVariation(new CueVariation(0.1f, 0.05f))
    .WithSfxCueVariation(new CueVariation(0.15f, 0.1f))
    .WithPlaylists(playlists)
    .Build();
```

JSON: `AudioConfig.Load()` / `TryLoad()` read `Content/Audio/audio-config.json`;
`AudioConfig.Parse(json)` / `TryParse`; `config.Serialize()`; `AudioConfig.Save(config, path)`.

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
  `PlaylistConfigJsonContext`, `PlayerAudioSettingsJsonContext`, `AudioLibraryManifestJsonContext`.
- Snake_case keys, case-insensitive on read, indented write.
- `Tracks` section (build-only loudness/compress targets) null-omitted on write.
- `ExtensionData` (`[JsonExtensionData]`) preserves unknown sections/fields across load→save.

## Hot reload

Re-parse config, apply via `PlayerSettings.ApplyOverrides` or rebuild director. Duck state
preserved through `DuckMixer.ReloadSources` (existing sources swap params in place).

## Tracks section (build-only)

`Dictionary<string, TrackConfig>` with `LoudnormConfig` (I/Tp/Lra) and optional
`CompressConfig`. Consumed by tooling; ignored at runtime but preserved on save.

## OpenStream

`AudioConfig.OpenStream` defaults to `File.OpenRead`; games replace with `TitleContainer.OpenStream`
so the same relative path resolves on DesktopGL and Blazor.

## Tips

- `TryParse` returns false instead of throwing (raw-JSON editors).
- `Load()` falls back to defaults when file missing.
- Save is no-op on browser (`OperatingSystem.IsBrowser()`).

See also: `cli` (validate), `player-settings`, `audio-engine`.
