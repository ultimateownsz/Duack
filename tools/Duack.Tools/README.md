# Duack.Tools (`duack`)

Build-time tooling for Duack content: scaffold, validate, and convert an
`audio-config.json` and its WAV masters. Desktop-only and never referenced by `src/` or
`samples/`; it is the only place `NAudio` and `OggVorbisEncoder` are used.

```
duack init     <contentDir> [--out <file>] [--force]
duack validate <config.json> [--playlists <file>]
duack convert  <contentDir> <outDir> [--config <file>] [--playlists <file>] [--quality <0..1>] [--force] [--all]
```

- **init** scans `<contentDir>` for `*.wav` and writes a starting `audio-config.json`
  (default `<contentDir>/audio-config.json`) with the default music/sfx sections, the
  `sfx`/`dialogue` duck sources, and one placeholder `tracks` entry per WAV. Refuses to
  overwrite an existing file unless `--force` is passed.
- **validate** reports schema and range problems with path-qualified messages and exits
  non-zero when there is an **error** (a warning still exits zero). It never mutates the
  file, so a hand-edited config can be checked before the runtime's `Sanitize` silently
  clamps it. `--playlists` cross-checks playlist tracks against the configured contexts.
- **convert** encodes the **songs** to OGG Vorbis for the web build; **sound effects stay
  WAV** (they are short and small, so converting them buys nothing). The **playlist is the
  authoritative song list**, so a playlist source is **required**; output is skipped when
  the OGG already exists and is at least as new as its WAV, and `--force` re-encodes anyway.
  `--all` is the explicit opt-in to convert **every** WAV (SFX included) and is mutually
  exclusive with a playlist source.

The playlist source is `--playlists <file>` (a standalone playlist JSON) or an embedded
`playlists` section read from `--config`. If `--config` is given but has no `playlists`
section, `convert` fails and asks for `--playlists` or `--all`. In this project playlists
live in the game's `audio-config.json` under `playlists`, so one file drives both playback
and conversion. See the wire format below.

## `audio-config.json` wire format and the ExtensionData round-trip contract

The config is snake_case JSON. The runtime model (`Duack.AudioConfig`) describes the mix
sections (`master_volume`, `music`, `sfx`) and the build-only `tracks` section. It also
carries `[JsonExtensionData] ExtensionData`: **any property the model does not describe is
captured on load and re-emitted on save.** This is load-bearing for tooling:

- Build-only sections such as `playlists` (and any section a future tool adds) are **not**
  in the runtime model, yet they survive an `AudioConfig.Load()` → `Serialize()` round trip
  because they land in `ExtensionData`.
- `duack convert --config <file>` reads the embedded `playlists` section straight out of
  `ExtensionData`, so the same file the game loads also drives conversion.
- **An external editor that produces `audio-config.json` MUST preserve unknown sections.**
  Deserialize into a model that keeps extension data (as `Duack.AudioConfig` does), or edit
  the JSON surgically; a model that drops unknown properties will silently delete
  `playlists` and every other build-only section on the next save.
- Section keys are camel/snake as written in the file. `duack` reads the embedded playlists
  map directly from the `playlists` value (the map IS the section's value; there is no
  second `playlists` wrapper inside it).

A standalone playlist file (`--playlists`) is a document whose root has a `playlists`
property:

```json
{
  "playlists": {
    "overworld": {
      "tracks": [ { "name": "theme_a", "context": "Overworld" } ],
      "repeat": "playlist",
      "shuffle": "bag"
    }
  }
}
```

Enums accept string (`off`/`playlist`/`track`, `off`/`bag`) or numeric values; comments and
trailing commas are allowed so hand-edited files stay friendly.

## Tests

```
dotnet test tests/Duack.Tools.Tests
```

Covers the validator rules, the song-set / conversion-decision policy, the scaffold shape,
the WAV→OGG round trip (temp directory), the playlist loader, and the CLI end-to-end
(exit codes + filesystem effects) for `init` / `validate` / `convert`.
