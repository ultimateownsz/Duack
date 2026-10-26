---
name: cli
description: Duack CLI (tools/DuackCli) — duackcli convert, scan, normalize commands, NAudio/OggVorbisEncoder (desktop-only tooling). Use when converting WAV to OGG, scanning audio manifests, or normalizing loudness.
---

# Duack CLI

`tools/DuackCli` is desktop-only .NET. NAudio and OggVorbisEncoder live here only — never
referenced by `src/` or `samples/`. Ships commands: `convert`, `scan`, `normalize`.
The CLI references the `Duack` core package.

## Commands

### `duackcli convert <contentDir> <outDir> [--playlists <file>] [--all]`

WAV → OGG for web. SFX stay WAV by default unless `--all` is used (playlist is the
authoritative song list). A content directory may also hold pre-encoded `.ogg` files (a cue
with no WAV master): `convert` copies them through verbatim (`copied=` in the summary).
Outputs newer than their source are skipped.

Convention: keep WAV masters in a gitignored `_masters/` directory (sibling to `Content/`) and
convert into `Content/Audio/Tracks/` (OGG) so the committed tree only ships the encoded tracks.
SFX masters stay as WAV in `Content/Audio/SFX/`. Normalize masters first
(`duackcli normalize _masters _masters_normalized`) when equal loudness across tracks matters.

```
dotnet run --project tools/DuackCli -- convert <app>/Content/Audio/_masters <app>/Content/Audio/Tracks --all
```

### `duackcli scan <audioRoot> [--out <file>]`
Writes `<audioRoot>/audio-manifest.json` (or `--out`) from the two-directory layout. Exits 1
on missing root. Warns (without failing) about files the loader can never open (wrong type
or upper-cased extension).

### `duackcli normalize <dir> [<outDir>]`
Measures each WAV track's RMS/peak loudness, reports the loudest track, and (when an output
directory is given) writes gain-normalized WAV copies so every track matches the loudest
track's RMS level. Aliases: `loudness`, `analyze`, `analyse`, `measure`, `loudnorm`.

## Deprecated commands

Legacy `audio-config.json` scaffolding and validation (`init`, `validate`, `convert --config`,
`normalize --write-config`) are deprecated and moved to the `legacy/pre-refactor` branch.

## Tooling rules

- Tools target desktop .NET only.
- Never referenced by `src/` or `samples/`.
- Editor-adjacent mixer UI stays in the host game — not part of Duack tools.

## Tips

- convert keeps SFX as WAV (web decodes WAV fine for short clips); tracks become OGG.
- scan output is the checked-in manifest web hosts read at runtime.

See also: `audio-library`, `config`.

