# Duack.Sample.Desktop

MonoGame DesktopGL sample for Duack as a scripted Pokémon-style storyline (the same
`StoryController` from `samples/Duack.Sample.Core` that the web sample runs): a rectangle
walks the overworld (overworld track), gets noticed by an NPC (notice sfx), chats (dialog
sfx per line), then battles (battle-start sting, battle track) trading pitched hit cues
until one monster faints and the whole story loops back to the walk.

## Run

```
dotnet run --project samples/Duack.Sample.Desktop -c Release
```

## Keys

| Key | Action |
|-----|--------|
| **Space** | Advance/skip the current beat (walk, notice, dialog line, battle intro) |
| **R** | Restart the story from the overworld walk |
| **S** | Play a random discovered sfx (marble bag, no back-to-back repeats) |
| **1** / **2** | Force the `overworld` / `battle` playlist |
| **Esc** | Quit |

## What it demonstrates

- **Two-directory convention with no registration**: `AudioLibrary.ScanDirectory` reads
  `Content/Audio/SFX/` (wav one-shots) and `Content/Audio/Tracks/` (ogg loops) — dropping
  a file in is all it takes to make a cue discoverable.
- **Auto-built playlists**: `AudioLibrary.BuildPlaylists` produces one looping single-track
  playlist per discovered track (scene music addressed by track name) plus the bag-shuffled
  `music` playlist over all of them.
- **`SfxManager`**: plays cues by name through `SoundEffectSfxBackend` (MonoGame) and
  automatically ducks the music (`MusicDirector.Pulse`) while a cue speaks. Unknown cues and
  backend refusals never duck.
- **Pitch variation from one sound**: the marble bag of hit pitches was removed from the showcase
  (it did not read well); hits now play at natural pitch. The `MarbleBag<T>` utility itself
  remains a core feature (see `MarbleBagTests`) and is still used by the bag-shuffled `music`
  playlist. The hit cadence, HP bars, and story beats are the pure, headless-pinned
  `StoryController` in the shared `samples/Duack.Sample.Core` (pinned by
  `tests/Duack.Sample.Tests`) — the web sample runs the exact same storyline from that core.
- The top strip visualizes `MusicDirector.DuckRatio` — ducking you can see.
- `MusicPlaylistController` + `MusicDirector` crossfade scene switches over the
  `Duack.MonoGame` `IMusicBackend` (overlapping `SoundEffectInstance`s).
- Console logs the discovered cues, the auto-built playlist JSON, and each scene change.

## Content layout

```
Content/Audio/
  SFX/          battle-start.wav, dialog.wav, hit.wav, notice-video-game-sfx.wav
  Tracks/       battle.ogg, hospital.ogg, overworld.ogg
_masters/       (gitignored) WAV masters for the Tracks songs
```

The shipped OGGs are regenerated from the gitignored masters with:

```
dotnet run --project tools/Duack.Tools -- convert samples/Duack.Sample.Desktop/_masters samples/Duack.Sample.Desktop/Content/Audio/Tracks --all
```

To swap in real audio, drop your files into `SFX/` (small wav one-shots) and `Tracks/`
(ogg loops) and see `samples/AUDIO_ATTRIBUTIONS.md`. The web counterpart is
`samples/Duack.Sample.Web`.
