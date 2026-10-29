---
name: config
description: Duack configuration — AudioConfig/AudioConfigBuilder, SchemaVersion, Sanitize clamping, source-generated contexts, host-driven hot reload (no built-in file watcher). Use when authoring config in code, configuring buses/contexts/duck sources, or setting player settings.
---

# Duack — Configuration

## Code-First Configuration

Duack is **code-first**: author the mix in C# via `AudioConfigBuilder`.

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
    .Build();
```

## Schema & versioning

- `AudioConfig.CurrentSchemaVersion = 1`.
- `SchemaVersion` field (serialized `schema_version`). Missing → reads as current (1).
  `Sanitize()` normalizes 0/legacy up to 1 and preserves a higher (future) stamp.

## Sanitize() clamping

- MasterVolume, Music.MasterVolume, Sfx.MasterVolume → [0,1].
- Music.CrossfadeSeconds → [0.1, 10].
- Contexts → [0,1].
- Duck sources: Ratio [0,1], Attack/Hold/Release >= 0.
- CueVariation rebuilt (ranges normalized).
- SchemaVersion normalized.

`PlayerAudioSettings.Sanitize()` same bounds, NaN → low bound.

## JSON contexts

- Source-generated contexts (no reflection; WASM/trimming safe): `PlaylistConfigJsonContext`,
  `PlayerAudioSettingsJsonContext`, `AudioLibraryManifestJsonContext` (all in the core).

## Hot reload (host integration)

Duack provides the pieces to support hot reload if your host wants it: apply player overrides with
`PlayerSettings.ApplyOverrides(authored)`, and tell the music manager to refresh duck
sources with `MusicManager.ReloadDuckSources()` (duck state is preserved; existing sources
swap params in place). Duack has **no built-in file watching** — file monitoring is the
host's responsibility (and varies by engine; not standard in MonoGame/KNI).

See also: `cli`, `player-settings`, `audio-core`.
