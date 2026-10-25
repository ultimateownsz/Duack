# Audio attribution

The samples ship **real audio, reused from the
[MonsterProjectWeb](https://github.com/vchelaru/MonsterProjectWeb) game project** (the
project Duack was extracted from) — music from Freesound (e.g. overworld loop by
[sirkoto51](https://freesound.org/people/sirkoto51/), hospital by
[moulaythami](https://freesound.org/people/moulaythami/)) and game SFX. The per-file
source table (original download names, artists, licenses) lives in MonsterProjectWeb's
`MonsterProjectWeb.Common/Content/Audio/ATTRIBUTIONS.md`; check it before redistributing
the samples beyond itch/GitHub. `dialog.wav` and `hit.wav` are authored specifically for
the sample; the sample's `hospital.wav` is a shortened edit of the game's master. The
files exist so `Duack.Sample.Desktop` and `Duack.Sample.Web` build and run on first try
with no external downloads.

If you swap in your own music and SFX, drop them into the two-directory convention and
regenerate:

```
duackcli scan <audioRoot>            # writes <audioRoot>/audio-manifest.json (web hosts read this)
duackcli convert <mastersDir> <tracksDir> --all   # encode Tracks songs to OGG for the web build
```

See `tools/DuackCli/README.md` for the full CLI.

## Your own audio: licensing checklist

Duack itself carries no audio. When you add music/SFX to a Duack-powered game, you are
responsible for the license of each file:

- **CC0 / Public Domain**: safest to bundle. No attribution required (courtesy credit is
  still nice).
- **CC-BY**: bundling allowed with attribution — keep a `NOTICE`/`ATTRIBUTIONS.md` naming
  each track, its creator, and the license version.
- **CC-BY-SA**: allowed only if your whole game ships under SA — usually undesirable.
- **CC-NC**: excludes commercial use — incompatible with most commercial games.
- **All Rights Reserved / proprietary**: do not bundle without a license from the rights
  holder.

Sources worth checking: opengameart.org (filter by CC0/CC-BY), freesound.org (check each
file's individual license), incompetech.com (CC-BY, Kevin MacLeod).

## Where audio goes (the two-directory convention)

```
Content/Audio/
  SFX/       small one-shot sounds — .wav preferred (.ogg allowed)
  Tracks/    loops and music tracks — .ogg preferred (.wav allowed)
```

- `AudioLibrary.ScanDirectory` (desktop) classifies both directories into cue names —
  nothing is registered by hand, dropping a file in is enough.
- Web hosts cannot enumerate directories, so `duackcli scan` writes the checked-in
  `audio-manifest.json` and the page rebuilds the same library from it.
- Directory names and extensions are matched **case-sensitively** (`SFX`, `Tracks`,
  lowercase `.wav`/`.ogg`): case-sensitive web hosts 404 on any mismatch, and `duackcli scan`
  warns about files it therefore cannot load.
- `duackcli convert <mastersDir> <tracksDir> --all` encodes `Tracks/` songs to OGG for the
  web build (SFX stay WAV: short and small, so converting buys nothing).

