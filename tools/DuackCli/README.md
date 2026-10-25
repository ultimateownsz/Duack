# DuackCli (`duackcli`)

Build-time tooling for Duack content: scaffold, validate, and convert WAV masters.
Desktop-only and never referenced by `src/` or `samples/`; it is the only place `NAudio`
and `OggVorbisEncoder` are used.

## Commands

### `duackcli init`

```bash
duackcli init <contentDir> [--out <file>] [--force] [--skills] [--skills-dir <dir>]
```

Scans `<contentDir>` for `*.wav` and writes a starting `audio-config.json` (default
`<contentDir>/audio-config.json`) with the default music/sfx sections, the `sfx`/`dialogue`
duck sources, and one placeholder `tracks` entry per WAV. Refuses to overwrite an existing
file unless `--force` is passed. `--skills` optionally installs the agent skill files.

### `duackcli validate`

```bash
duackcli validate <config.json> [--playlists <file>]
```

Reports schema and range problems with path-qualified messages and exits non-zero when there
is an **error** (a warning still exits zero). Never mutates the file. `--playlists`
cross-checks playlist tracks against the configured contexts.

### `duackcli convert`

```bash
duackcli convert <contentDir> <outDir> [--config <file>] [--playlists <file>] [--quality <0..1>] [--force] [--all]
```

Encodes the **songs** to OGG Vorbis for the web build; **sound effects stay WAV**. The
**playlist is the authoritative song list**, so a playlist source is **required**; output is
skipped when the OGG already exists and is at least as new as its WAV, and `--force`
re-encodes anyway. `--all` converts **every** WAV (SFX included) and is mutually exclusive
with a playlist source. Already-encoded `.ogg` files are copied through verbatim.

### `duackcli scan`

```bash
duackcli scan <audioRoot> [--out <file>]
```

Reads the two-directory audio convention (`<audioRoot>/SFX/*` for one-shot sounds,
`<audioRoot>/Tracks/*` for loops/songs) and writes `audio-manifest.json` (default
`<audioRoot>/audio-manifest.json`) listing the discovered cue names. Exits 1 when the audio
root does not exist.

### `duackcli normalize`

```bash
duackcli normalize <dir> [<outDir>] [--out <dir>] [--config <file>] [--write-config]
```

Measures each WAV track's RMS and peak loudness, reports the loudest track, and (when an
output directory is given) writes gain-normalized WAV copies so every track matches the
loudest track's RMS level. Aliases: `loudness`, `analyze`, `analyse`, `measure`,
`loudnorm`.

The playlist source is `--playlists <file>` (a standalone playlist JSON) or an embedded
`playlists` section read from `--config`. If `--config` is given but has no `playlists`
section, `convert` fails and asks for `--playlists` or `--all`. In this project playlists
live in the game's `audio-config.json` under `playlists`, so one file drives both playback
and conversion. See the wire format below.

The format is defined **once in core** (`Duack.PlaylistJson`, source-generated so the AOT web
head can read it at runtime); this tool's `PlaylistFile` is a thin wrapper over it, so the CLI
and the game never drift on the format.

## Authoring playlists in code

A game can define playlists in C# and let them reach the CLI as JSON (the "define in code,
convert from JSON" loop):

```csharp
var playlists = new PlaylistConfigBuilder()
    .WithPlaylist("overworld", p => p
        .Track("theme_a", "Overworld")
        .Track("theme_b", "Overworld")
        .Repeat(PlaylistRepeatMode.Playlist)
        .Shuffle(PlaylistShuffleMode.Bag))
    .Build();

var config = new AudioConfigBuilder()
    .WithPlaylists(playlists)   // embeds them under the "playlists" key
    .Build();
File.WriteAllText("audio-config.json", config.Serialize());
// then: duackcli convert <contentDir> <outDir> --config audio-config.json
```

`WithPlaylists` stores the map in `AudioConfig.ExtensionData`, so it survives `Serialize()` /
`Load()` exactly like a hand-authored section. `PlaylistJson` exposes the same conversion
directly: `Parse` / `TryParse` / `Serialize` / `ToElement` / `FromElement` / `FromConfig` /
`Embed`.

## `audio-config.json` wire format and the ExtensionData round-trip contract

The config is snake_case JSON. The runtime model (`Duack.AudioConfig`) describes the mix
sections (`master_volume`, `music`, `sfx`) and the build-only `tracks` section. It also
carries `[JsonExtensionData] ExtensionData`: **any property the model does not describe is
captured on load and re-emitted on save.** This is load-bearing for tooling:

- Build-only sections such as `playlists` (and any section a future tool adds) are **not**
  in the runtime model, yet they survive an `AudioConfig.Load()` / `Serialize()` round trip
  because they land in `ExtensionData`.
- `duackcli convert --config <file>` reads the embedded `playlists` section straight out of
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

```bash
dotnet test tests/DuackCli.Tests
```

Covers the validator rules, the song-set / conversion-decision policy, the scaffold shape,
the WAV to OGG round trip (temp directory), the playlist loader, and the CLI end-to-end
(exit codes + filesystem effects) for `init` / `validate` / `convert`.

