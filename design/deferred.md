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

### Post-1.0 deprecation policy
- What: adopt `[Obsolete]` with a stated removal version as the standard
  breaking-change instrument.
- Why not built: in preview, the whole API is blanket-deprecated; the
  compiler instrument adds nothing until there are users to warn.
- Reopens when: the first 1.0 release is scheduled.
