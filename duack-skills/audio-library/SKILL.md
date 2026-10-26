---
name: audio-library
description: Duack's two-directory audio convention — AudioLibrary classification/scan, CandidatePaths, ToPlaylist/BuildPlaylists, AudioLibraryManifest for web hosts, duackcli scan. Use when organizing audio files, scanning content, supporting web manifests, or wiring cue names without hand-registration.
---

# Duack — Audio Library (Two-Directory Convention)

Audio root holds `SFX/` (small one-shots, wav preferred) and `Tracks/` (loops/songs, ogg
preferred). Dropping a file in is all it takes — no cue registration by hand.



## Classification

- `AudioLibrary.FromFiles(IEnumerable<string> relativePaths)` — classify relative paths.
- `AudioLibrary.ScanDirectory(audioRoot)` — scan live (desktop). Missing subdir = empty side;
  missing root throws.
- Case-SENSITIVE: `SFX`/`Tracks`, lowercase extensions only. Web hosts 404 on mismatch.

Extensions:
- SFX: `.wav`, `.ogg`
- Tracks: `.ogg`, `.wav`
- Legacy flat: `.ogg`, `.wav` (final fallback)

Only direct children of convention dirs count; root files, unknown dirs, nested paths,
unsupported types, dotfiles ignored. Cue name = filename without extension, sorted/deduped.

## Querying

```csharp
library.Sfx;            // IReadOnlyList<string>
library.Tracks;         // IReadOnlyList<string>
library.IsSfx(name);
library.IsTrack(name);
library.CandidatePaths(name);  // ordered relative probe paths (owning dir first, flat last)
```

## Playlists from library

```csharp
library.ToPlaylist(context, repeat, shuffle);   // one looping playlist of all tracks
library.BuildPlaylists(options);                 // per-track + all-tracks bag-shuffled
```

`BuildPlaylists` options (`AudioLibraryPlaylistOptions`): `AllTracksPlaylistName` (default
"music", null/blank skips; collision with track throws), `SingleTrackRepeat`,
`AllTracksRepeat`, `AllTracksShuffle`, `ContextForTrack`.

## Web manifest

Browsers can't enumerate directories. `duackcli scan <audioRoot>` writes `audio-manifest.json`.

```csharp
AudioLibraryManifest manifest = library.ToManifest();
AudioLibrary library = AudioLibrary.FromManifest(manifest);
```

Manifest: snake_case (`schema_version`, `sfx`, `tracks`), source-generated (AOT-safe),
missing stamp reads as current, future stamp preserved, unknown fields survive load→save,
explicit null lists normalize to empty, malformed JSON throws `JsonException`.

## Web host wiring

```csharp
// fetch checked-in wwwroot/Content/Audio/audio-manifest.json
var manifest = AudioLibraryManifestJson.Deserialize(json);
var library = AudioLibrary.FromManifest(manifest);
```

## Candidate paths & legacy flat

`CandidatePaths` returns owning convention dir first, then legacy flat `{name}.ogg|.wav` last,
so existing flat content keeps loading unchanged. `SoundEffectClipLibrary`
uses `CandidateUris` with exact legacy order when no library supplied.

## duackcli scan

```
duackcli scan <audioRoot> [--out <file>]
```
Writes `<audioRoot>/audio-manifest.json` (or `--out`). Exits 1 on missing root. Warns (no
fail) about files the loader can never open (wrong type or upper-cased extension).

## Tips

- Classification is case-sensitive on purpose — don't lowercase.
- `ScanDirectory` throws on missing root; missing subdir is fine.
- Manifest round-trips through `ToManifest`/`FromManifest`.

See also: `playlists`, `cli`, `audio-core`.
