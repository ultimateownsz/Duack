# Deferred ideas

Every idea considered but not built, with the condition under which it
reopens. A "no" is not forever, but the reopen trigger is a real issue,
never a hypothetical. Do not re-propose an entry here without either
matching its reopening condition or bringing a real issue.

Format:

    ### <idea>
    - What: <one line>
    - Why not built: <one line>
    - Reopens when: <concrete issue/repro/user report> | "never" (say why)

Entries below are examples of the register's tone; replace with the real
backlog.

### Native audio backends (desktop)
- What: bypass SoundEffectInstance with a native (e.g. miniaudio) backend.
- Why not built: MonoGame backend covers current needs; adds a native
  dependency per platform for no reported pain yet.
- Reopens when: a real game hits a SoundEffectInstance limitation (voice
  count, per-voice routing) with a reproduction.

### Gapless looping on web (KNI/WASM)
- What: seamless track loops via WebAudio scheduling instead of
  MediaPlayer-based looping.
- Why not built: browser loop gap is real but unmeasured on current KNI.
- Reopens when: someone reports an audible gap with the track and browser
  that produce it. Until measured, an experiment belongs in
  design/experiments/ before this becomes an initiative.
