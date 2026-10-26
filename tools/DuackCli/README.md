# DuackCli (`duackcli`)

Build-time tooling for Duack content: convert WAV masters, scan audio manifests, and normalize track loudness.
Desktop-only and never referenced by `src/` or `samples/`; it is the only place `NAudio`
and `OggVorbisEncoder` are used.

## Commands

### `duackcli convert`

```bash
duackcli convert <contentDir> <outDir> [--playlists <file>] [--quality <0..1>] [--force] [--all]
```

Encodes the **songs** to OGG Vorbis for the web build; **sound effects stay WAV**. The
**playlist is the authoritative song list**, so a playlist source (`--playlists`) or `--all` is
**required**; output is skipped when the OGG already exists and is at least as new as its WAV, and `--force`
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
duackcli normalize <dir> [<outDir>] [--out <dir>]
```

Measures each WAV track's RMS and peak loudness, reports the loudest track, and (when an
output directory is given) writes gain-normalized WAV copies so every track matches the
loudest track's RMS level. Aliases: `loudness`, `analyze`, `analyse`, `measure`,
`loudnorm`.

## Standalone playlists

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

The format is defined **once in core** (`Duack.PlaylistJson`, source-generated so the AOT web
head can read it at runtime); this tool's `PlaylistFile` is a thin wrapper over it, so the CLI
and the game never drift on the format.

## Deprecated: `audio-config.json`

The legacy `audio-config.json` file format and associated CLI commands (`init`, `validate`,
`--config`, `--write-config`) are deprecated and moved to the `legacy/pre-refactor` branch.
Duack configuration is code-first via `AudioConfigBuilder` and `PlaylistConfigBuilder`, and
asset discovery uses the two-directory convention (`SFX/`, `Tracks/`) with `duackcli scan`.

## Tests

```bash
dotnet test tests/Duack.Tools.Tests
```

Covers the song-set / conversion-decision policy, the WAV to OGG round trip (temp directory),
the loudness analyzer, the playlist loader, and the CLI end-to-end (exit codes + filesystem
effects) for `convert`, `scan`, and `normalize`.

