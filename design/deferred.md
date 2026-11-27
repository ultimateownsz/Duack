# Deferred ideas

Every idea considered but not built, with the condition under which it
reopens. A "no" is not forever, but the reopen trigger is a real issue,
never a hypothetical. Do not re-propose an entry here without either
matching its reopening condition or bringing a real issue. When a real
issue matches a condition, the idea moves out of the register into
plan/ as an initiative, and the entry is replaced by a line pointing at
the plan folder.

Format:

    ### <idea>
    - What: <one line>
    - Why not built: <one line>
    - Reopens when: <concrete issue/repro/user report> | "never" (say why)

Entries below are examples of the register's tone; replace them with the
real backlog.

### Native audio backends (desktop)
- What: bypass SoundEffectInstance with a native (e.g. miniaudio)
  backend.
- Why not built: MonoGame backend covers current needs; adds a native
  dependency per platform for no reported pain yet.
- Reopens when: a real game hits a SoundEffectInstance limitation
  (voice count, per-voice routing) with a reproduction.

### Gapless looping on web (KNI/WASM)
- What: seamless track loops via WebAudio scheduling instead of
  MediaPlayer-based looping.
- Why not built: browser loop gap is real but unmeasured on current KNI.
- Reopens when: someone reports an audible gap with the track and
  browser that produce it. Until measured, an experiment belongs in
  design/experiments/ before this becomes an initiative.

### Startup validation of string name references (buses, duck targets, contexts, playlists)
- What: an eager, additive validation pass that cross-checks declared string references —
  duck source `Target`s against the bus table and registered sources, bus names referenced
  in plays, music context names — and fails fast on the first run instead of relying on
  play-time events or silent no-ops.
- Why not built: no reported bite yet. The play-time reports (`BusFellBack`,
  `ZeroEffectiveGain`) cover the hottest surface (buses), and userland constants close the
  rest at the call site. The typed-ref alternative (readonly struct end-to-end) was
  evaluated and rejected: it only helps if strings stop compiling everywhere at once, costs
  a custom JSON converter per wrapped name in the source-generated contexts, and
  `default(...)` bypasses constructor validation anyway (qa-verified Nov 22, 2026, evidence
  in issue #31's comment thread / slice C review).
- Reopens when: a user reports a typo-caused silent failure on any surface that never
  reports at play time (unknown context silently at scale 1, `SetDuck`/`BeginDuck`/`EndDuck`
  on an unregistered source, `DuckSourceConfig.Target` naming a never-declared bus), or
  when slice E's named mix states add more cross-referencing string surfaces that make the
  silent paths materially likelier.

### Context → playlist routing with context-filtered track selection
- What: a context → playlist name table authored on the mix config
  (`WithContextPlaylist(contextName, playlistName)` storage on
  `MusicConfig`), a `MusicManager.PlayForContext(contextName)` call that
  resolves it, and an `onlyContextTracks` option so a playlist draws
  only tracks whose `PlaylistTrack.Context` matches the active context
  (bag-shuffle cycle accounting counts drawn tracks, not all).
- Why not built: the consumer mapping layer (map/trigger/faint checks in
  the game choosing `manager.Play(...)`) covers the need today, and the
  names/semantics were only just settled (docs-writer pass Nov 26,
  2026); also entangled with per-track repeat (issue #53), which should
  land first or together so single-track routed playlists are
  first-class.
- Reopens when: a real game reports the hand-built mapping layer or the
  same-track-plays-regardless selection hole biting in practice (a
  Pokemon-style map/battle/hospital flow with a shared playlist is the
  canonical repro), or when issue #53 lands and leaves the routing
  still hand-rolled in a sample or game.

### Post-1.0 deprecation policy
- What: adopt `[Obsolete]` with a stated removal version as the standard
  breaking-change instrument.
- Why not built: in preview, the whole API is blanket-deprecated; the
  compiler instrument adds nothing until there are users to warn.
- Reopens when: the first 1.0 release is scheduled.
