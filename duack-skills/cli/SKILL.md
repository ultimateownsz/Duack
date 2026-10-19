---
name: cli
description: Duack CLI (tools/Duack.Tools) — duack init, validate, convert, scan commands, NAudio/OggVorbisEncoder (desktop-only tooling), ExtensionData round-trip contract. Use when scaffolding config, validating audio-config.json, converting WAV to OGG, scanning audio manifests, or building editors against the tooling layer.
---

# Duack — CLI Tools

`tools/Duack.Tools` is desktop-only .NET. NAudio and OggVorbisEncoder live here only — never
referenced by `src/` or `samples/`. Ships commands: `init`, `validate`, `convert`, `scan`.

## Commands

### `duack init <contentDir>`
Scaffolds `audio-config.json` from a content directory scan. Generates config with discovered
contexts/defaults.

### `duack validate <config.json>`
Schema + range validation with clear messages. Checks SchemaVersion, clamping bounds, unknown
fields preserved. Exit non-zero on failure.

### `duack convert <input> <output>`
WAV → OGG for web. SFX stay WAV, songs become OGG. Supports `--all` to convert a whole
directory tree. Used for web build pipeline (WAV masters → OGG tracks).

```
dotnet run --project tools/Duack.Tools -- convert samples/.../_masters samples/.../Content/Audio/Tracks --all
```

### `duack scan <audioRoot> [--out <file>]`
Writes `<audioRoot>/audio-manifest.json` (or `--out`) from the two-directory layout. Exits 1
on missing root. Warns (without failing) about files the loader can never open (wrong type
or upper-cased extension).

## ExtensionData round-trip contract

For anyone building an external editor against the tooling layer:
- Unknown sections/fields are captured on load into `ExtensionData` and re-emitted on save.
- `Tracks` (build-only) preserved.
- SchemaVersion: missing → current; higher preserved, not downgraded.
- Sanitize clamps values; editors should call validate after edits.

## Tooling rules

- Tools target desktop .NET only.
- Never referenced by `src/` or `samples/`.
- AudioMixer (preview/editor-adjacent) stays in MonsterProjectWeb — not part of Duack tools.

## Tips

- Run validate in CI to catch hand-edited config drift.
- convert keeps SFX as WAV (web decodes WAV fine for short clips); tracks become OGG.
- scan output is the checked-in manifest web hosts read at runtime.

See also: `audio-library`, `config`.
