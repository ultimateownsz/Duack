# Slice F — bus unification — decisions made while building

Populated as implementation decisions land. ADR deviations belong in
[STATUS.md](STATUS.md) with rationale, never here.

### Structure decisions (Nov 24, 2026, before any code — inherited, binding)

- **D1–D5 of slice C stand** (user-buses/slice.md): flat buses only, lowercase Ordinal names,
  `sfx` fixed, `Changed`/methods as the mutation surface, live per-play resolution, duck
  outside bus state. This slice adds no parent, solo, snapshot, effect, or resolve-once surface.
- **D6 — Config-level wrapper compatibility.** `WithMasterVolume`,
  `WithMusicMasterVolume`, `WithSfxMasterVolume`, and `WithBus` keep their exact signatures and
  write the one flat `BusConfig` table; the wrapper-equivalence tests are the regression
  contract and are written red first.
- **D7 — Runtime constructor signatures stay** (issue #36 non-goal): the bus table reaches the
  managers through the existing `SfxConfig` transport; no `SfxManager`/`MusicManager`
  constructor parameter is added, removed, or re-typed.

### Working decisions

- **D8 — One table, same-instance transport** (work item 2, `c45e39c`). Physical storage is
  `AudioConfig.Buses: Dictionary<string, BusConfig>` (Ordinal, lowercase); `SfxConfig.Buses`
  and `MusicConfig.Buses` are nullable *references* to that same instance wired by
  `AudioConfig`'s setters/constructor, so replacing a section via object initializer cannot
  fork storage (pinned: `Assert.Same(built.Buses, built.Sfx.Buses)`). Chosen over keeping an
  independent `SfxConfig` dictionary: one truth, while D7's "existing transport" still holds.
- **D9 — Pre-registration lives in `AudioConfig.CreateDefaultBuses()`, not the builder**
  (deviation from the issue's "builder pre-registers" wording): hand-composed graphs
  (`PlayerSettings.ApplyOverrides`, object initializers) must carry `master`/`music`/`sfx`
  at full volume too; builder-only seeding would leave every hand-built runtime config without
  the fixed three. Names exposed as `BusNames.Master/Music/Sfx`.
- **D10 — Dedicated volume fields removed, not delegated** (`AudioConfig.MasterVolume`,
  `MusicConfig.MasterVolume`, `SfxConfig.MasterVolume` gone; preview-breaking): a delegating
  keep-alive property was considered and rejected — it would hide the one-truth change and
  leave dead surface for work item 3 to remove anyway. Called out in the changelog
  (`## Unreleased → Changed → #### 2026-11-25`) with the compile symptoms (CS0117/CS1061/CS0029/CS1503).
- **D11 — Runtime seed stays behavior-identical until work item 3**: `SfxManager` projected
  only user buses (filtering the now-present `master`/`music`/`sfx`) into the unchanged
  `BusMixer` seed; `MusicManager.Compose` read the `music` table entry with a
  standalone-`MusicConfig` fallback of 1. Mechanical, zero audible change; superseded by
  D12-D17.
### Resolver decisions (work item 3, `a2ff83e` + `0e63d08`)

- **D12 — `BusMixer` seeds the whole table.** Ctor re-typed to
  `IEnumerable<KeyValuePair<string, BusConfig>>?`; master/music/sfx always exist (missing
  entries start at full volume; table entries override; user buses alongside), so a
  table-less `SfxConfig` can no longer turn `Resolve("master")` into a silent sfx fallback.
- **D13 — Two mixer states, one table of volumes** (deviation from a literal "one resolver"
  reading): `SfxManager` and `MusicManager` each own a `BusMixer` seeded from the same config
  table, because D7 freezes every constructor and there is no channel between managers.
  Changelogged consequences: per-bus mute/pause is per-manager, volumes snapshot at
  construction, and a `SetBus` on one manager's mixer does not move the other's.
- **D14 — Manager volume properties are thin delegating writers.**
  `SfxManager.MasterVolume`/`SfxBusVolume` and `MusicManager.MasterVolume` get/set the mixer's
  master/sfx entries (`BusMixer.VolumeOf` is the new tiny read-side surface). Ctor float
  params still win over config at construction — pre-existing behavior, kept.
- **D15 — `BusView` gains `DuckRatio`** (appended; compile-breaking, changelogged).
  `BusMixer` takes an optional `Func<string,float>? duckRatioFor` (injected by `SfxManager`),
  read live at snapshot — D4 holds: duck never embeds in bus state. A mixer without the
  function reports 1 per bus. `MusicManager.DuckRatioFor` stays as a delegating wrapper.
- **D16 — The `ZeroEffectiveGain` fixed-path pin was already green** under slice C (the mixer
  zero-folded `Resolve(null)`), so the "new" pin is an honest regression pin, not a red.
- **D17 — Fold split across two commits.** (a) behavior-silent unification, (b) the audible
  fold (duck exemption for `bus is null` removed: `* _music.DuckRatioFor(resolution.Bus)`
  always) — each breaking surface has its own changelog entry and revert unit.

### WebAudio decisions (work item 4, `78fba13`)

- **D18 — Honest subset; core seam is the blocker.** `ISfxBackend.Play` hands the adapter
  only the fully composed final volume — no bus name crosses the seam. Full per-user-bus node
  routing would change `ISfxBackend` (core contract) and either alter desktop audibly or drift
  the cross-backend API (ADR-8); both out of scope. Blocker changelogged, not papered over.
- **D19 — One persistent gain node per bus this adapter actually plays.** JS builds
  `sfx → master → destination` at `initAudioContext`; every per-source gain routes through
  `sfxNode` instead of ad-hoc connections to the destination. Zero math change, identical
  audible output, `ISfxBackend` untouched.
- **D20 — `SetBusVolume(bus, volume)` on three layers** (`IWebAudioNative` — contract change
  for bridge implementors, changelogged; the `[JSImport]`; `WebAudioSfxBackend`, dispose-guarded
  and clamped). Deliberately NOT on `ISfxBackend`: web-only surface until sample wiring (item 5).
- **D21 — JS `setBusVolume` semantics:** clamp [0,1]; `cancelScheduledValues` + 3 ms
  `linearRampToValueAtTime` (matches the onset ramp, no slider zipper); unknown bus names
  become on-demand leaf nodes under master, unrouted until the core seam carries buses —
  no invented routing.
- **D22 — No `music` node in this backend:** it plays one-shots only; web music is the
  MediaPlayer `<audio>` host's own volume, outside this chain.
### Sample decisions (work item 5a, `53fd0ae`)

- **D23 — the "one mixer UI path" is the shared readback helper, not shared key handlers.**
  `SampleAudio.DescribeBuses(BusMixer)` lives in the engine-free core; both heads print it
  verbatim (pinned headlessly), fixed `sfx` and user `ambience` bus shown through one
  `BusMixer.Views` path including `DuckRatio`. Key handling stays per-head (engine territory);
  no adapter type check in `Sample.Core`.
- **D24 — web seam wired at the `sfx` bus only**: `N` pushes the mixer mute plus
  `WebAudioSfxBackend.SetBusVolume` so playing one-shots dip live; muting `ambience` pushes
  nothing (its JS node is unrouted — pushing would be theater; comment in code).
- **D25 — desktop `N` pushes nothing** (MonoGame path is arithmetic-only, applies from the
  next play) — the documented cross-backend acoustic difference, ADR-8 pattern, changelogged.
- **D26 — bus-targeted ducks stay out of the sample** (maintainer call stands); the `sfx` fold
  is pinned in core tests only. Human-eyeball list for both heads recorded with D23-D26 in
  the work item report; the checkbox cannot tick before it.

### Listening-feedback decisions (Nov 25, 2026, `2ce3d5a`)

- **D27 — Maintainer ruling: no cue survives a screen switch.** From human listening on the
  desktop sample (same week): the fainting blow's hit cue bled over the walk screen — the
  classic XNA/MonoGame forum complaint about one-shots overlapping a freshly loaded screen.
  Ruling: a beat's cue never shares its frame with a scene switch. The battle now ends ON the
  battle screen (KO hit, then `_battleEnding` beat), and the next tick returns a clean walk
  frame with music only. Entry stings stay (battle-start sting untouched — ruled an entry
  beat, not a leftover). The dead duck-exception (`duck = frame.Music is null`) was removed
  from both heads since no storyline frame carries both `Sfx` and `Music` anymore.
- **D28 — D27 extended to every entry beat** (listening round 2, `623d665`): the notice cue,
  line-1 dialog cue, and battle-start sting carried `Sfx` + `SceneChanged` on one frame — same
  bleed at walk→notice, notice→dialog, dialog→battle-intro. Mechanism: three entry beats
  (`_noticeCueBeat`/`_dialogCueBeat`/`_stingBeat`, 0.5/0.5/0.6 s) fire each cue on its OLD
  screen; every switch frame loads clean. Uniform skip rule: Space fires the next exit cue but
  never skips a beat or the battle; `Restart`/`EnterBattle` clear all beat state. Crossfade
  starts with the sting on the dialog screen (heads key on the cue, not the flag). Loop pacing
  +~1.6 s (≈23 s total with D27's +1.2). A full-loop pin now asserts no frame ever mixes a cue
  with a switch. (D29 supersedes the Space rule's observable side: with heads non-interactive,
  no key fires exit cues; the rule lives in the controller and its pins only.)
- **D29 — The demo heads are non-interactive** (listening round 3, `55b750c`): maintainer ruling —
  the samples auto-play; the keyboard controls advertised an interaction that does not exist.
  All key branches and their plumbing removed from both heads (Space/R/S/1-2/M/N, ESC exit);
  the story advances purely on its timers; the unified readback prints one
  `DescribeBuses` line per scene switch (slice F's one-mixer-path acceptance stays visible in
  the console without interaction). Controller methods (`Advance`/`Restart`) stay public and
  pinned in `Sample.Core`; the web `SetBusVolume` seam (work item 4) stays and keeps its
  adapter tests. Full suite 423/423 green (phase-end check).
- **D30 — No truncation either** (listening round 4, `c5d5c71`): the maintainer clarified D27's
  intent — cues must play out naturally, not be clipped at the switch. Measured from the
  shipped WAV headers: notice 1.08 s (beat was 0.5 — half-cut), battle-start 0.60 s (beat
  0.6 — tail clipped), longest KO hit variant 1.73 s (ending beat was 1.2). Beats are now
  sized asset + `BeatTailSeconds` (0.1 s): `NoticeCueBeatSeconds` 1.18, `StingBeatSeconds`
  0.70, new `BattleEndingSeconds` 1.83 (no longer reusing `BattleRoundSeconds` for the
  ending). Pinned red-first against the actual WAV durations (test reads the shipped
  headers). `DialogCue` has no asset on any head (pre-existing silent gap, confirmed, left
  as-is). Rationale recorded: `ISfxBackend` has no completion telemetry, so beats are
  asset-sized; event-driven switching needs the deferred per-playback-handles experiment.

Deviations from PLAN wording, all changelogged: "one resolver" landed as two mixer instances
over one table (D13, forced by D7); `BusMixer` ctor signature and `BusView` shape changed
(PLAN said "public surface unchanged") — forced by whole-table seeding and the readback field;
"one gain node per bus" landed as one node per bus of the chain this adapter can actually
resolve (`sfx`, `master`; user buses on demand, unrouted), reason D18.
