# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Detailed per-change notes live in [`.opencode/audit-log.md`](.opencode/audit-log.md).

## [Unreleased]

### Changed

#### 2026-11-25
- **Preview breaking (audible): a duck source targeting `"sfx"` now scales the fixed
  (un-routed) SFX path too.** Symptom before: with a duck source authored as
  `Target = "sfx"`, only bus-routed plays dipped; plain `SfxManager.Play(cue)` (the fixed
  path, what every project without user buses uses) ignored the duck entirely — the same
  authored config did different things depending on how a cue was routed. Now the fixed path
  resolves on the `sfx` bus entry like any other bus, so the duck ratio applies to it
  equally (Gate D fold: one semantic, one path). What breaks for consumers: existing mixes
  whose duck sources name `"sfx`" get audibly quieter fixed-path cues while that duck is
  active — no code change required, but re-listen to any `"sfx"`-targeted duck source; duck
  sources targeting `music` (the default) or another bus are unaffected, pinned both ways in
  tests. Zero-gain interplay: such a play can now compose to zero (e.g. ratio 0) and then
  reports through `ZeroEffectiveGain` like every other silent play instead of playing
  inaudibly. (#36)

#### 2026-11-25
- **Preview breaking: one resolver — `BusMixer` now owns the whole bus table.**
  `SfxManager.Buses` (the `BusMixer`) seeds the fixed `master`/`music`/`sfx` entries from
  the config table alongside the user buses, and both managers resolve every bus — fixed
  three included — through that one mixer chain (master → bus volume → mute/pause): the SFX
  path composes `master gain × bus gain` per play, `MusicManager` composes master × music
  gain per frame. What breaks for consumers: `BusView` **gains a `DuckRatio` field** (any
  code constructing, pattern-matching or deconstructing a `BusView` must add it), and
  `SfxManager`'s `ZeroEffectiveGain` now also fires for the fixed (un-routed) SFX path when
  it composes to zero — pinning the zero-fold that path already had via the fixed SFX bus.
  The `SfxManager.MasterVolume`/`SfxBusVolume` and `MusicManager.MasterVolume` hot-update
  properties keep compiling and behave the same, but are now thin delegating writers into
  the mixer's `master`/`sfx` entries (`BusMixer.VolumeOf` is the new read side), so setting
  e.g. `SfxBusVolume` and `Buses.SetBus("sfx", v)` touch the same single storage instead of
  two parallel floats. Readback unification: `Buses.Views` carries, for every bus including
  the fixed three, the effective gain and the live duck contribution (`DuckRatio`, read from
  the duck system at snapshot time — duck ratios never embed in bus state);
  `MusicManager.DuckRatioFor` stays and delegates unchanged. `MusicManager` keeps its own
  mixer state seeded from the same table (constructor signatures are frozen), so per-bus
  mute/pause state is per-manager and volumes are snapshotted from the table at
  construction, as before. No audible change in this commit: duck sources still scale only
  the buses they target; the `"sfx"` fold lands as its own preview-breaking note with the
  next entry. (#36)

#### 2026-11-25
- **Preview breaking: volumes now live in one flat bus table.**
  `AudioConfig.Buses` (a `Dictionary<string, BusConfig>` with lowercase, Ordinal-compared
  names) is THE one storage for every bus volume: it comes pre-registered with the
  `master`/`music`/`sfx` entries, and user buses authored via
  `AudioConfigBuilder.WithBus(name, volume)` land in the same table
  (`BusConfig { Volume }`; `Sanitize`/`Build` clamp every table volume to [0, 1] as before).
  What breaks for consumers: the dedicated `AudioConfig.MasterVolume`,
  `MusicConfig.MasterVolume` and `SfxConfig.MasterVolume` properties are **removed** — read
  and write `config.Buses[BusNames.Master/Music/Sfx].Volume` instead;
  `SfxConfig.Buses` is **re-typed** from `Dictionary<string, float>` (its own storage) to
  `Dictionary<string, BusConfig>` (a reference to the same one table, wired by `AudioConfig`,
  null when a `SfxConfig` is constructed standalone) — `sfx.Buses["ambience"] = 0.4f` no
  longer compiles; use `buses["ambience"].Volume = 0.4f` on the shared table; and
  `MusicConfig` gains the same `Buses` table reference so `MusicManager` construction is
  unchanged. Symptom of staying on the old members: `CS0117`/`CS1061` compile errors on
  `MasterVolume` and `CS0029`/`CS1503` on the old side-dictionary writes. No audible
  behavior change: `WithMasterVolume`/`WithMusicMasterVolume`/`WithSfxMasterVolume`/`WithBus`
  keep their exact signatures as thin writes into the one
  table (`WithBus("master", v)` replaces the same single entry), and the runtime still
  composes the fixed fields from the table values exactly as before. (#36)

#### 2026-11-25
- **The sample heads are non-interactive demos now** (maintainer ruling, Nov 25, human
  listening round 3: the storyline auto-plays, so the keyboard controls were dead surface
  advertising an interaction that does not exist). Removed in both heads: Space
  (advance/skip), R (restart), S (random cue), 1/2 (playlist), M (ambience-bus mute), N
  (sfx-bus mute — including the web head's `SetBusVolume` push), and the ESC exit branches,
  plus all keyboard plumbing that served only them (`WasKeyPressed`, `_previousKeyboard`,
  `KeyboardState`, the XNA `Input` using). The story advances solely on its timers;
  `StoryController.Advance`/`Restart` stay public on the engine-free core (their semantics
  are pinned headlessly; no head calls them). The unified one-mixer-path acceptance stays
  visible without interaction: each scene switch now prints ONE line combining the scene
  name with the shared bus readback
  (`SampleAudio.DescribeBuses`: master/music/sfx + ambience with live gains and duck
  ratios). No library behavior change; `WebAudioSfxBackend.SetBusVolume` remains the web
  seam surface (pinned in the adapter tests) — its sample caller is what was removed. (#36)

#### 2026-11-22
- `BusMixer.SetBus` change detection compares volumes bit-exactly
  (`BitConverter.SingleToInt32Bits`) instead of `==`: no behavior change (both values pass
  the same clamp, and the warning the change answers is about tolerance, which change
  detection must not apply) - it makes the exactness intent explicit and answers the
  float-equality analyzer warning. Ships with no new test because the behavior is covered by
  the existing same-value/different-value `Changed` pin tests. (#31)
- Skills and code comments are now self-contained descriptions of behavior, without
  references to internal planning artifacts. Docs only; no behavior change. (#31)

#### 2026-11-23
- Removed `planner` skill from `duack-skills/` — planner is repo tooling for project-specific planning workflow, not marketplace content. Users keep local copy at `.vibe/skills/planner/` or equivalent. No behavior change. (#35)

#### 2026-11-01
- Added a separate `.github/workflows/release-cli.yml` that publishes `DuackCli` as its own optional NuGet .NET tool (triggered by `cli-v*` tags), plus a README install note and CLI skill install instructions. (#11)

#### 2026-10-29
- Player-settings diagnostics are now silent by default and opt-in via an injectable `IPlayerSettingsDiagnostics` sink (`PlayerSettings.Diagnostics`); consumers enable them by assigning `ConsolePlayerSettingsDiagnostics` or a custom sink. Web store read/save failure messages route through the sink. Removed the per-play `"[WebAudioSfxBackend] Playing ..."` success log line.

#### 2026-10-29
- Clarified docs that "configuration-driven" means ducking/crossfades/playlists/volumes are driven by config objects authored in C# via `AudioConfigBuilder`/`PlaylistConfigBuilder`. Updated README and audio-core/crossfade skill docs to remove the apparent "config-driven (code-only)" contradiction.


### Added

#### 2026-11-25

- **The concurrency example is clickable (#38).** `Duack.Sample.Concurrency` grew three
  on-screen buttons — **ONE** (single cue), **BURST** (the 50-cue stress burst), **STOP**
  (silence everything mid-listen) — so the human listening check no longer requires knowing
  the keyboard bindings; Space/T/Escape keep working and Escape still exits. Labels use the
  same no-asset 3x5 pixel-glyph approach as the seven-segment counter (the project
  deliberately has no content pipeline). Sample-only: no library behavior change, and the
  sample has no headless test surface (MonoGame `Game` needs a real window) — the click path
  is verified by the human listening check itself.

#### 2026-11-25
- **Slice A follow-up: audible voice stealing and web voice aging (#38).** A stolen voice is
  now silenced, not just unbookkept: the optional additive `ISfxVoiceControlBackend` seam
  (`PlayTracked` returning a handle, `StopVoice`) is implemented by `SoundEffectSfxBackend`
  (handle = the started instance) and by `WebAudioSfxBackend` (handle = the JS source id; the
  WebAudio bridge gains `playSourceTracked`/`stopSource` and keeps a per-source registry, so
  one voice can be stopped without `StopAll`). `SfxManager` stops a chosen victim through the
  seam before starting the new voice and falls back to slot-only stealing when the backend
  does not implement the seam — the observable config/result API is identical across backends
  (ADR-8 pattern); only the acoustic result differs. Contract notes: `IWebAudioNative`
  implementors must add `PlaySourceTracked(string, float, float): long` and
  `StopSource(long)`; the per-source JS registry is cleaned on `onended`, `stopSource` and
  `StopAll`, and stopping an already-finished or unknown handle is a no-op.
  The web head (`Duack.Sample.Web`) now authors the voice limits (global cap 8, 1 s assumed
  lifetime, oldest stealing) and ages them by calling `SfxManager.Update(unscaledDt)` every
  frame — previously the web head never aged voices, so a sustained burst would have
  saturated the cap forever. Real-browser listening check for the web head and the desktop
  `Duack.Sample.Concurrency` stress example remain pending (human verification).

#### 2026-11-25
#### 2026-11-25

- **Sample demo: the fixed sfx bus and the user buses through one mixer path.** The demo
  heads now surface the bus unification through the shared storyline: both heads' HUD/debug
  line prints one `SampleAudio.DescribeBuses(_sfx.Buses)` readback (pinned in
  `Duack.Sample.Tests`) listing every bus of the resolved table — `master`, `music`, the
  fixed `sfx` and the user `ambience` bus — with live effective gain and duck contribution,
  so the fixed bus and the user bus are shown through the same `BusMixer.Views` machinery.
  Hot mute got the same treatment: the desktop head keeps `M` (ambience) and gains `N`
  (fixed sfx bus) through the identical `_sfx.Buses.SetMute` path, and the web head mirrors
  both keys — with `N` also pushing the bus volume into the WebAudio seam
  (`WebAudioSfxBackend.SetBusVolume`), so currently playing one-shots dip live in the
  browser while the desktop stays arithmetic-only (applies from the next play), the
  documented cross-backend difference. Bus-targeted ducks stay out of the sample per the
  Nov 22 maintainer call; the `sfx` fold stays pinned in core tests only. (#36)
#### 2026-11-25

- **Web sample prefetch: no first-play waits after the start gesture** (maintainer ruling,
  Nov 25, human listening round 5: the first music track started with an audible network
  delay on the web head - the desktop loads all Content up front). Investigation found the
  SFX side already prefetching: `WebAudioSfxBackend`'s constructor (which runs after the
  boot-screen gesture, `Index.razor.cs`) hands every cue URL to the bridge in one call and
  the JS `preloadBuffers` fetches + `decodeAudioData`s all of them up front - now pinned
  headlessly. The music path is the honest subset, like D18: the KNI
  `MediaPlayer`/`IMediaPlayerHost` seam (`KniMediaPlayerHost.cs`) exposes only
  `Play(uri)`/`Stop` - no preload/dual-buffer capability - so the BlazorGL `<audio>` fetches
  its OGG on first play. What landed: `WarmHttpCache(urls)` on the backend/bridge
  (`IWebAudioNative` contract addition, JS bridge `warmHttpCache` fetches, fire-and-forget)
  and the sample warms every track URL right after the start gesture, so the first track
  starts from the browser HTTP cache (~3.4 MB of demo OGGs) instead of the network.
  Documented residue (ADR-8 pattern): the element's one-time OGG decode on first play
  remains, and no pin can cover browser fetch behavior - it needs the human browser check.
  Dispose semantics extended: `IWebAudioNative.ReleaseAllBuffers()` + JS
  `releaseAllBuffers()` (contract additions), and `WebAudioSfxBackend.Dispose` now releases
  the JS-side decoded SFX buffers (~1.4 MB for the demo set) instead of leaking them.
  (#36)
- **Per-bus gain nodes on the web SFX head.** The WebAudio JS bridge now keeps one
  persistent gain node per bus of the playing chain — the fixed `sfx` node every play's
  per-source gain routes through into the `master` node, then the destination — instead of
  connecting each source straight to the destination, and gains a `setBusVolume(bus,
  volume)` bridge call with a matching `WebAudioSfxBackend.SetBusVolume` (clamped to
  [0, 1], 3 ms ramp inside JS) so a live bus volume change moves the currently playing
  voices through that node, not only future plays. Contract change: `IWebAudioNative`
  implementors (custom test fakes, alternative bridges) must add
  `SetBusVolume(string, float)`; `ISfxBackend`/`ISfxBackend.Play` are unchanged and the
  per-play volume the core hands the backend is applied exactly as before, so the
  observable API is identical across backends. Honest subset (known limitation): the core
  seam hands the adapter only the fully composed per-play volume — no bus name — so of the
  flat table's buses only the fixed `sfx`/`master` chain carries audio today; user-bus
  nodes are created on demand and ready to drive, but nothing routes into them until the
  core seam passes the resolved bus per play (a core `ISfxBackend` contract change, out of
  this adapter's scope). Real-browser listening still pending: the headless seam tests pin
  the bridge calls, not the JS audio graph. (#36)

#### 2026-11-22

- **User-defined buses.** `AudioConfigBuilder.WithBus("ambience", volume)`
  names any number of buses under the existing chain; `SfxManager.Play(..., bus: "ambience")`
  routes a cue by per-play argument; contexts stay music-only.
  `SfxManager.Buses` is a `BusMixer` with per-bus volume, `SetMute`, `SetPaused` and a global
  `MasterMuted`, all hot-updatable and reported through per-bus readback views (`Views`) and a
  `Changed` event so a frontend mixer can render this state without owning it. Unknown bus names
  fall back to the SFX bus and raise `BusFellBack`; a play whose composed effective gain is zero
  raises `ZeroEffectiveGain` (no backend call, no duck) instead of audibly disappearing. Ducking
  extends to buses: `DuckSourceConfig.Target` names a bus (`"ambience"`) instead of the music, and
  `DuckMixer.BeginDuck`/`EndDuck` are ref-counted so overlapping duck holds cannot release the
  duck early. Known limitation: per-bus active-voice counts need backend completion telemetry the
  `ISfxBackend` seam does not expose yet and are deferred. The sample demo covers bus routing
  and the mute/hot-volume path; no bus-targeted duck is demoed until a real project proves
  that scenario. Human listening on the ambience sample still pending. Bus-targeted ducks
  (`DuckSourceConfig.Target` on a bus) are carried unvalidated: no worked example in the
  skills until a real project proves the dialogue-style scenario. (#31)
- Sfx full-suite notes: `DuckMixer.Set(name, false)` now also clears ref-counted holds
  (releases like `ReleaseAll` for that source, at that source's release constant); the fixed
  SFX path (`Play` without `bus`) stays outside bus-duck composition, so a duck source
  targeting the literal `"sfx"` bus applies only to bus-routed plays. Both behaviors unit-pinned. (#32)
- **OpenCode review agent crew.** `.opencode/agents/` gains three subagents modeled on the
  FlatRedBall2 discipline-agent subset:
  `qa` reviews diffs and PRs against AGENTS.md and issues a good or
  blocked verdict at a new plan-phase PR gate before a phase checkbox
  may tick; `refactoring-specialist` refactors test-first and runs
  only the targeted suites a diff touches; `docs-writer` maps
  landed changes to the changelog, skills, docs and README.
  AGENTS.md documents when each is used. Ships untested on purpose:
  agent prompts and documentation only, no production code touched. (#23)
- Every plan checkbox now names its issue (`- [ ] <step> (#N)`), created before work starts, and implementing commits carry a `Refs: #N` footer. `git log --grep '#N'` lists the exact commits behind a box so a change can be reviewed, documented or reverted as a unit. Added matching human/agent issue templates under `.github/ISSUE_TEMPLATE/` and documented the convention in `AGENTS.md`, `CONTRIBUTING.md`, `plan/plan.md`, `design/TODOS.md` and both plan templates. (#24)

#### 2026-11-22
- **Plan splitter, per-slice vNext workspace, and PR-review loop.** `tools/plan_split.py` turns a monolithic markdown plan into `README.md`, `SHARED.md` and one `<slug>/PLAN.md` per slice, with 12 stdlib tests under `tools/tests/`. `docs/plans/vnext/` is now in that shape. `AGENTS.md` documents the splitter and the new `/pr-review <number>` OpenCode command, which launches the `qa` agent, verifies blocking findings and records a dated verdict in `docs/plans/vnext/REVIEWS.md` — the first entry covers this PR. Python bytecode is gitignored. (#22)

#### 2026-11-19
- Slice A of the vNext plan: SFX voice concurrency. `AudioConfigBuilder.WithVoiceLimit(...)` and
  `WithCueVoiceLimit(...)` author global/per-cue caps, stealing policies (oldest, quietest, priority),
  and assumed voice lifetimes. `SfxManager.PlayDetailed(...)` and `LastPlayResult` report why a cue
  was refused (unknown cue, priority rejection, backend refusal); `Play(...)` keeps its `bool`
  contract. `SfxManager.Update(unscaledDt)` ages reserved voices; a backend refusal releases its slot.
  A dedicated `Duack.Sample.Concurrency` example fires 50 overlapping cues at cap 8 (T key) with
  an on-screen active-voice counter; the existing storyline samples are untouched. Backends are unchanged: lifetimes are authored, not observed (documented in
  `docs/plans/vnext/voice-concurrency/PLAN.md`). Tests pin cap boundaries, victim selection, policy
  overrides, refusal results, and lifetime release. (#38)

#### 2026-10-31
- `MusicManager.CurrentTrackElapsedSeconds` reports elapsed playback time for the current track on both backends; `CurrentTrackDurationSeconds` is nullable for consumer-provided resolvers.

#### 2026-10-28

- **GitHub Pages workflow for the web sample.** New `.github/workflows/pages.yml` builds and
  publishes `samples/Duack.Sample.Web` (WASM workload) to GitHub Pages on push to `master` or
  manual dispatch, so the crossfade + ducking demo is viewable in a browser without a local
  build. Autoplay requires a click ("Start story") — documented up front. (#3)
- Added MusicManager transition telemetry: IsTransitioning, elapsed/duration seconds, and source/target track names for UI, diagnostics, and progress indicators.


#### 2026-10-29

- **`MusicManager` no longer reports a stale fade-out duration in transition telemetry on
  non-overlapping (web/KNI) backends.** `TransitionDurationSeconds` is `_fadeOutDuration +
  _transitionDuration` there, but the two standalone fade-in paths (first play with no track
  playing, and a `Repeat=Track` restart after a completed crossfade) never reset
  `_fadeOutDuration`, so first play reported `8.2s` instead of `8s` and a repeat-track
  fade-in reported `12s` (progress started at ~33%). Both paths now set `_fadeOutDuration = 0f`,
  matching the overlap backend. Regression tests:
  `Non_overlap_first_play_reports_only_the_fade_in_duration`,
  `Non_overlap_repeat_track_fade_in_does_not_inherit_the_previous_fade_out`.
  Also made `MusicTransitionSequencer.Advance` consume a delta across phase boundaries so a
  single large `Update()` can complete a multi-phase transition. (#7)

### Fixed

#### 2026-11-25
- **Every screen now loads clean — the entry cues land on their own screen too** (maintainer
  ruling, Nov 25, human listening round 2, extending the KO fix to the story's ENTRY
  transitions). Symptom: the same Sfx-over-switch bleed existed at walk→notice (the notice
  cue played into the freshly loaded notice screen), notice→dialog (the first line's cue)
  and dialog→battle-intro (the battle-start sting) — each transition frame carried BOTH the
  sfx and the scene switch, the same overlap the KO fix removed. Fix: `StoryController` now
  runs the same beat pattern on every entry — the cue fires while the OLD screen is still up
  (`NoticeCueBeatSeconds`/`DialogCueBeatSeconds` 0.5 s, `StingBeatSeconds` 0.6 s; the storyline
  loop is ~1.6 s longer per full pass), and the switch frame one beat later carries the scene
  change and NO sfx. The battle-music crossfade still starts with the sting (both heads key
  on `BattleStartCue` itself), firing a beat earlier on the dialog screen; the intro screen
  keeps its `BattleIntroSeconds` pause. Space still kicks a pace break's exit cue off
  (skipping the wait), but no beat — entry, ending, or the battle — can be skipped;
  `Restart()` and a new battle clear all beat state. Pinned headlessly: the full-loop
  no-mixed-frames pin (no frame ever carries both a cue and a scene/music switch) plus
  per-beat pins for all three entries. (#36)
- **Beats let each cue finish instead of clipping it at the switch** (maintainer clarification,
  Nov 25, human listening round 4: the no-bleed rulings D27–D29 mean "no lingering sound
  reaches the next screen", NOT truncation; the change should make sounds play the way a
  player expects, not act as a workaround). Symptom: the battle-start sting's tail was
  clipped at the dialog→intro switch (the 0.6 s beat equaled the cue, so the switch-frame
  `Stop()` chopped it) and the notice cue was cut roughly in half at the walk→notice switch
  (0.5 s beat vs a 1.08 s asset); a drawn 1.73 s hit-2 could also be clipped by the
  battle-ending beat's 1.2 s. Fix: every beat is sized to its cue's shipped asset plus one
  named tail margin (`BeatTailSeconds` 0.10 s, documented as covering the Update tick
  boundary): `NoticeCueBeatSeconds` 0.5 → 1.18 s, `StingBeatSeconds` 0.6 → 0.70 s, and the
  battle-ending beat gains its own constant `BattleEndingSeconds` = 1.83 s (longest hit
  draw) instead of reusing `BattleRoundSeconds`. `ISfxBackend` exposes no completion
  telemetry, so the beats are sized from the shipped WAVs (event-driven switching needs
  per-playback handles — the deferred experiment). The storyline loop grows ~1.41 s
  (~24.5 s); heads' `Stop()` on scene change stays and now cuts nothing. Pinned headlessly
  against the shipped WAV durations (`Every_beat_outlasts_its_cue...`). Pre-existing,
  confirmed, not fixed here (out of scope): the `dialog` cue has no shipped asset, so it
  silently plays nothing. (#36)
- **The KO hit no longer bleeds over the walk screen** (maintainer ruling from human
  listening, Nov 25: no cue from a finished gameplay screen may survive into the next
  screen). Symptom: play either sample head through a battle; on the KO, the fainting
  "hit" cue played over the freshly loaded walk screen while the music crossfaded back to
  the overworld — the classic XNA/MonoGame forum complaint about one-shots overlapping a
  freshly loaded screen. Fix: `StoryController` gains a battle-ending beat — the fainting
  tick returns the plain hit frame (the battle screen stays up and the music is still the
  battle track, so the KO is heard on the battle screen), and only the NEXT round tick
  (~1.2 s later) returns the walk-switch frame carrying the overworld music and NO sfx.
  The ending beat resets on `Restart()` and `EnterBattle()` and, like the battle itself,
  cannot be skipped by Space. Entry stings are untouched. The heads' dead
  "duck only when no music switch" exception in `ApplyFrame` (no frame carries both an sfx
  and a music switch anymore) was simplified away: every story cue now ducks. Pinned
  headlessly in `Duack.Sample.Tests` (KO-on-battle-screen, clean walk switch, no skip,
  mid-ending restart). (#36)
- The web sample's (`M`/`N`) no longer rely on nullable-dereference
  suppression of `CS8602` warnings: they use the same post-initialization idiom
  (`_sfx!`) the story loop already uses, so the sample head builds warning-clean. Also makes
  `SampleAudio`'s `System.Linq` usage explicit instead of implicit-usings-dependent. No
  library behavior change; sample code only. (#36)

#### 2026-11-22
- `BusMixer.SetBus` no longer fires the `Changed` event when the volume value is unchanged -
  a per-frame hot-update loop re-notified all subscribers every frame. Creating a bus by the
  first call still notifies (it is a state change). Repro: set the same value twice and
  assert one event (BusMixerTests.SetBus_SameValueOnKnownBus_SecondCallDoesNotRaiseChanged). (#32)
- Documented (no behavior change, pinned by test): `BusMixer.SetBus` on an unknown name
  intentionally creates the bus - it is the authoring surface, matching the
  `DuckMixer.ReloadSources` hot-update pattern - while `Resolve` on unknown names falls back
  to SFX and reports via `BusFellBack`; the asymmetry between authoring (creates) and
  routing (falls back loudly) is deliberate. (#31)

#### 2026-11-22
- **PR #22 review findings (#25–#29).** `tools/plan_split.py` no longer treats a `##` line
  inside a fenced code block as a section (regression test:
  `test_fenced_heading_is_not_a_section`; repro was a plan example line
  `## 9. Slice Z — Fake` producing a `fake/` folder and vanishing from `SHARED.md`).
  `AGENTS.md` now names the per-slice shape (`README.md` index, `SHARED.md`, one
  `<slug>/PLAN.md` per slice) instead of pointing at "the shape above".
  `plan/01-vnext/` gains `phase-01-vnext-slices.md` (the phase that was advertised
  without a doc) and `tools/plan_check.py` now link-checks `docs/plans/` too, so the
  vNext workspace is no longer invisible to the walk-test check.
  The slice-folder convention is stated identically in `docs/plans/vnext/README.md`,
  `SHARED.md` and `plan/01-vnext/overview.md` (folder with `PLAN.md` from the split;
  `STATUS.md`/`slice.md` when the slice starts), matching the shipped tree.
  Slice A's status in `docs/plans/vnext/README.md` and its `STATUS.md` now states the
  code lives on the unmerged `feat/voice-concurrency` branch (pushed at `29aa0c6b`),
  not merged into `master`.

### Changed

#### 2026-10-28

- **Removed authored `audio-config.json` compatibility from master.** `AudioConfigJson`,
  playlist embedding, build-only track metadata, CLI `init`/`validate`/`--config` compatibility,
  and their scaffolding/validation tests are gone from the active codebase. The prior
  implementation remains available on `legacy/pre-refactor`; active configuration is authored
  with `AudioConfigBuilder` and `PlaylistConfigBuilder`. Standalone playlist, audio-manifest,
  and player-settings JSON remain supported because current CLI and runtime flows use them. (#10)
- **README adds a browser/WASM gotcha for copied Duack skills.** If a project targets the
  browser, keep the audio flow in Duack's C# API and adapters instead of re-implementing it in
  vanilla HTML/JS or raw browser audio code. Desktop builds are already C# and do not involve
  HTML/JS. (#4)
- **README clarifies the CLI is desktop-only build-time tooling**, not a runtime dependency:
  it runs on the dev machine and never ships to the browser; the web head reads the checked-in
  `audio-manifest.json`. README also notes that files the loader cannot open are reported (scan
  warnings + runtime `[Audio] Could not load ...` lines) rather than failing silently, adds a
  compact five-line config sample, an autoplay/user-gesture note, a "who adopts first"
  (MonoGame, then FlatRedBall2) answer, and an old-way-vs-new-way crossfade table. (#4)
- **Web sample README brought up to date**: it now documents `WebAudioSfxBackend` for
  overlapping one-shot cues instead of the obsolete "no sfx backend" note. (#4)
- **README gains a Samples table** with a Play column linking to the GitHub Pages live demo. (#4)
- **Web sample gains a live crossfade toggle.** Press `C` to flip `CrossfadeSeconds` between
  the authored value and 0 (instant hard cut); a corner indicator shows green (ramp) / red
  (hard cut). README points at it. (#3)
- **New `samples/Duack.Sample.Crossfade`** minimal showcase: click tracks or press 1/2/3/Space,
  click preset buttons for crossfade duration (4s/8s/12s/16s), toggle crossfade on/off.
  Config mirrors `WithCrossfadeSeconds` usage. (#3)
- **Crossfade demo UX clarified.** The player now presents a guided choose-track/play flow,
  labeled transport actions, larger touch targets, clearer transition and playback feedback,
  visible keyboard guidance, and an expandable explanation of the desktop and web transition
  paths. The UI remains a sample-only Blazor surface over Duack's C# API. (#3)
- **Added `crossfade-demo.html`** standalone WebAudio page (slider + play buttons) under the
  web sample's wwwroot for a lightweight feature showcase. (#3)

### Fixed

#### 2026-10-28

- **Web player settings now propagate a storage rejection.** `WebPlayerSettingsStore.Save`
  previously returned success whenever `IKeyValueStorage.Set` returned normally, even when the
  storage adapter explicitly returned `false`; the settings UI could therefore report "Saved!"
  although the new values were not persisted. A regression test now covers this path. (#9)
- **Added an HTML/Blazor control overlay to the Crossfade sample.** The overlay now mirrors the
  former two-column player layout with track list, previous/play/next controls, status, meter,
  crossfade toggle, and 4s/8s/12s/16s presets. All actions invoke the running C# `Game1` and
  Duack `MusicManager`; the canvas and browser/KNI audio path remain unchanged. No separate audio
  behavior test was added because this is a thin UI event bridge; existing `MusicManager` tests
  cover the delegated transition behavior. (#3)
- **Removed the legacy Crossfade canvas UI and static HTML legend.** The Blazor overlay is now the
  only visible control surface, avoiding duplicate track panels and conflicting mouse hit areas. (#3)
- **Made the Crossfade transition meter live.** It now reads transition phase, source/target tracks,
  elapsed seconds, total duration, and progress from `MusicManager` through C#; no browser audio
  timing or vanilla-JavaScript fade logic is involved. (#3)
- **Fixed the Crossfade sample's duration presets.** Per-track `FadeInSeconds = 0.4f`
  accidentally overrode `MusicManager`'s configured crossfade duration, making the 4s/8s/12s/16s
  controls switch tracks after only 0.4 seconds. Removing those per-track overrides restores the
  configured transition durations. (#2)
- **GitHub Pages now deploys the actual Blazor/KNI Crossfade sample at
  `/crossfade-demo/`.** The workflow previously published only the Story sample, so the
  documented Crossfade route was missing and the `.html` link opened the separate standalone
  vanilla-JavaScript showcase instead. The Blazor sample is now published as its own directory,
  preserving the configured 4s/8s/12s/16s Duack crossfade controls. (#3)
- **Renamed `AudioLibrary.SfxManageryName` to `AudioLibrary.SfxDirectoryName`.** The
  director→manager rename pass had accidentally rewritten the `SfxDirectoryName` constant
  (no "Director" substring intended) into `SfxManageryName`. Value (`"SFX"`) and behavior
  unchanged; the public constant now has the intended name. Any consumer referencing the
  misspelled name must update to `SfxDirectoryName`. Pins:
  `Convention_directory_constants_have_the_intended_names`. (#2)

## [0.1.0-preview.4] - 2026-10-26

### Added
- **NVorbis `TotalSamples` underestimation pitfall documented.** The adapters skill now documents
  that `VorbisReader.TotalSamples` can underestimate decoded samples (VBR streams), causing
  `IndexOutOfRangeException` in decoders that pre-allocate from the reported total. `OggPcmWav.Decode`
  handles this by adding slack and growing on demand. Any custom decoder must do the same.
- **Release process documented.** `CONTRIBUTING.md` now has a "Releasing a new version" section
  with the full checklist (version bump, changelog, tag, push, verify). Added `tools/release.ps1`
  helper script with dry-run support.

### Changed
- **Music-related test files renamed for consistency.** `MusicDirectorTests` ��' `MusicManagerTests`,
  `MusicDirectorPlayResetTests` ��' `MusicManagerPlayResetTests`, `SfxDirectorTests` ��'
  `SfxManagerTests`, `MusicManagerFadeTests` (was `MusicManagerFadeTests`). Test bodies updated to
  use `MusicManager` naming. No production behavior changed.

#### 2026-10-26

- **Clip-based music/SFX backends moved to core.** `SoundEffectMusicBackend` and
  `SoundEffectSfxBackend` (along with the `IAudioInstance` / `IAudioClip` / `IAudioClipLibrary`
  seams) moved from `Duack.MonoGame` into `Duack` core. Both are pure BCL now and work against
  any `IAudioClipLibrary` implementation — desktop (SoundEffect) or web (WebAudio) — so a KNI
  desktop build can use the overlap crossfade path instead of `MediaPlayerMusicBackend`. The
  `SoundEffectClipLibrary` / `SoundEffectAudioClip` bindings stay in `Duack.MonoGame`. Adapters
  reference the moved types via the nested `Duack` namespace, so existing code keeps compiling.

#### 2026-10-25

- **JSON wire-format tests now live with the core suite.** `AudioConfigTests`,
  `PlaylistJsonTests`, and the config schema-version pins moved from the removed
  `tests/Duack.Config.Tests` project into `tests/Duack.Tests/JsonRoundTrip`. No production
  behavior changed; the tests still use only `src/Duack`.
- **README now documents the benefits over the built-in MonoGame/XNA audio path.** A comparison
  table covers crossfade/playlists, buses, ducking, cue variation, settings persistence,
  desktop/web parity, tooling, dependency footprint, and authoring. It also states what Duack
  deliberately does not do (editor/GUI, FMOD/Wwise replacement, playback engine).
- **Standardized the preview description on "code-first".** AGENTS.md, the audio-core skill, and
  the core JSON comments no longer say "code-only", which incorrectly implied the core has no
  JSON code. The JSON layer remains part of the core for tooling and persistence.
- **Per-user player-settings folder is no longer named after a consumer.**
  `FilePlayerSettingsStore.DefaultRootDirectory()` now uses the host application's entry-assembly
  name (`%APPDATA%\<assembly>` on Windows, `~/.config/<assembly>` elsewhere) and falls back to
  `Duack` when no entry assembly is available. Games that relied on the previous hardcoded folder
  must pass their existing root explicitly. Pinned by
  `FilePlayerSettingsStore_default_root_uses_the_host_application_name`.
- **Browser settings key is no longer consumer-specific.** `WebPlayerSettingsStore.DefaultKey`
  changed to `duack:playerAudioSettings`. Existing web builds that relied on the old default
  should pass their previous key to the constructor (or accept a one-time settings reset); the
  key remains overridable. Pinned by
  `WebPlayerSettingsStoreTests.Key_is_namespaced_so_it_cannot_collide_with_other_saved_data`.
- **Docs, comments, and internal notes are consumer-agnostic.** Removed the remaining
  consumer-project names from AGENTS/CONTRIBUTING, skills, tooling and sample comments, test
  comments, and the audit log. The only remaining upstream name is the required audio-attribution
  link in `samples/AUDIO_ATTRIBUTIONS.md`; supported-engine references (MonoGame/KNI/FlatRedBall2/
  FNA) are unchanged. The test comment that framed a duck-source reload around a hot-reload
  scenario was removed until a host needs that guidance.

#### 2026-10-26

- **Player settings documentation no longer describes the authored configuration as a shipped
  `audio-config.json` file.** Player preferences remain outside authored content and now refer to
  the code-first configuration surface.

### Fixed

#### 2026-10-25

- **`duackcli` help no longer advertises the retired `duack` command.** The usage banner and
  user-facing `[duack]` status prefixes now say `duackcli`, matching the packaged
  `ToolCommandName`. Pinned by `CliTests.No_arguments_prints_usage_and_fails` and
  `CliTests.Init_writes_a_config_next_to_the_content`.

### Deprecated

#### 2026-10-26

- **The legacy `audio-config.json` workflow is deprecated.** `AudioConfigJson`, the build-only
  `Tracks`/`ExtensionData` members, playlist embedding, and the CLI's `init`, `validate`,
  `convert --config`, and `normalize --config`/`--write-config` paths now emit deprecation guidance
  pointing to `legacy/pre-refactor`. Active authoring uses `AudioConfigBuilder` and
  `PlaylistConfigBuilder`; `duackcli convert` accepts standalone playlists or explicit `--all`,
  while `duackcli scan` and `normalize` remain available. Existing JSON APIs remain present as
  obsolete compatibility symbols and are covered by the round-trip tests.

### Removed

#### 2026-10-25

- **Removed the last references to the deleted `Duack.Config` project.** The sample projects
  still pointed at `src/Duack.Config/Duack.Config.csproj`, which produced MSB9008 warnings and
  made the build depend on a project that no longer exists. The JSON layer already lives in
  `src/Duack` (`AudioConfigJson`, `PlaylistJson`, `AudioConfigBuilderJsonExtensions`), so those
  references were dropped and the comments/docs/skills now point at the core. No runtime
  behavior changed. The previous separate-package layout remains on `legacy/pre-refactor` if a
  concrete JSON use case later justifies bringing it back.

### Security
