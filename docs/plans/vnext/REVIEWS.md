# Duack vNext — PR review record

Durable record of review verdicts that affected this plan workspace. Each entry is a snapshot of
the PR head at review time, so findings stay re-derivable after the branch moves. Produced by the
`/pr-review <number>` workflow (`.opencode/commands/pr-review.md`).

## PR #22 — chore: add plan splitter and per-slice vNext workspace

- Reviewed: 2026-11-22
- Head at review: `c09b05414ac51f3d25185139af56e4ccdbee6646`
- Base: `master`
- Verdict: **blocked**
- Reviewer: `qa` subagent; blocking findings independently verified against the live PR head

### Blocking

**F1 — Changelog entry missing from the PR head.**
- Rule: `AGENTS.md:8-12` — every code change needs a dated, categorised entry under `## Unreleased`
  with a PR/issue reference before it lands.
- Proof: `gh pr diff 22 --name-only` at head `c09b054` lists 15 files and no `CHANGELOG.md`. That
  file has `#### 2026-11-22` (the PR #23 agent-crew entry) but no `2026-11-20`, no `plan_split`, no
  "plan splitter", though the PR body claims a dated 2026-11-20 entry under `### Added`. The old
  local tip `0838e14` did contain it; the merge commit that is the real PR head dropped it in
  conflict resolution. Even that version lacked `(#22)`.
- Required: restore under `Unreleased → Added → #### <date>` with `(#22)`.
- Status: fixed in the follow-up commit on this branch that adds this record.

### High

**F2 — Slice A state describes landed code that is absent from this base.**
- Rule: `AGENTS.md:84-85,134-139` (durable, walk-testable state); `CONTRIBUTING.md:44`.
- Proof: `voice-concurrency/STATUS.md:5` and `:7-9` say the core, tests, skill, sample and 392 green
  tests "landed" and call `feat/voice-concurrency` "not pushed"; `README.md:14` repeats it. At the
  PR head there is no `samples/Duack.Sample.Concurrency/`, no `duack-skills/concurrency/SKILL.md`,
  no `VoiceLimits`/`PlayDetailed`/`ActiveVoiceCount`, and `SfxManager` has no `Update`. The branch
  *is* on the remote (`29aa0c6b…`).
- Required: record Slice A as unmerged work on this base, or move the state docs to the branch that
  carries the code.
- Status: open.

### Medium

**F3 — Contradictory slice-folder convention.**
- Proof: `SHARED.md:101` says unstarted slices keep a spec-only folder; `README.md:6-8` and
  `plan/01-vnext/overview.md:12-13` say the folder is created only when the slice starts; the PR
  ships folders for B–E.
- Required: pick one rule and align all three files.
- Status: open.

**F4 — Index advertises a phase with no phase doc; `plan_check` does not cover `docs/`.**
- Proof: `plan/plan.md:55` has `| Duack vNext | 01 | plan/01-vnext/overview.md | In progress |`, but
  `plan/01-vnext/` has no `phase-01-*.md`. `tools/plan_check.py:24-27` excludes `docs/` and
  `:114-125` only validates phase docs that exist, so its `pass` says nothing about this layout.
- Required: add the phase doc or change the index row; extend the checker to cover the new docs.
- Status: open.

### Low

**F5 — Broken antecedent in AGENTS.md.**
- Proof: `AGENTS.md:141` says "split it into the per-slice shape above", but the shape is defined in
  `SHARED.md:100-103`, not above the instruction.
- Required: point at the shape explicitly.
- Status: open.

**F6 — Splitter matches `##` inside fenced code blocks.**
- Proof: `tools/plan_split.py:18,31-40` matches `^## ` with `re.M`, fence-unaware. Repro: a plan
  with a fenced `## Slice Z - Fake` produced `fake/PLAN.md` and removed that line from `SHARED.md`.
  No checked-in docs were corrupted.
- Required: fence-aware parsing plus a regression test, or a written reason in the changelog.
- Status: open.

### Checked and clear

- TDD: 12/12 pass at head; red reproduced with a stub (10 `NotImplementedError`, two CLI assertion
  failures).
- Architecture: no `src/` changes, no package/project references, no NAudio/OggVorbisEncoder leakage.
- `python tools/plan_check.py` → `pass` (see F4 caveat).
- Scope: `.gitattributes` is no longer in the PR diff because master advanced to `52cb505`;
  `.gitignore:9-12` ignores Python bytecode.

## 2026-11-22 — re-review at head `9c13104` (base `52cb505`): good

Re-ran the `qa` review against the live PR head after the fix commit. Every finding from the
`c09b054` review was independently re-verified against `9c13104` (via the GitHub contents API,
not the local tree). All findings are fixed; none remain open.

- F1 (changelog missing): fixed in `650df42` — `CHANGELOG.md` carries the dated `### Added`
  entry with `(#22)`. Verified in the head snapshot.
- F2 → issue #25 (closed): `README.md` and `voice-concurrency/STATUS.md` now state Slice A
  lives on `feat/voice-concurrency` (pushed at `29aa0c6b`), unmerged; the "landed"/"not pushed"
  claims are gone. Verified in the head snapshot.
- F3 → issue #26 (closed): one slice-folder convention (folder with `PLAN.md` from the split;
  `STATUS.md`/`slice.md` when the slice starts) stated identically in `README.md`,
  `SHARED.md`, and `plan/01-vnext/overview.md`; matches the shipped tree.
- F4 → issue #27 (closed): `plan/01-vnext/phase-01-vnext-slices.md` exists (template shape,
  ID range reserved in the overview); `tools/plan_check.py` now link-checks `docs/plans/`
  and tolerates `file:line` backtick references.
- F5 → issue #28 (closed): `AGENTS.md` names the per-slice shape and links `docs/plans/vnext/`;
  no dangling "above".
- F6 → issue #29 (closed): `_parse` is fence-aware (`FENCE_RE`, tracked ``` / ~~~ fences);
  regression test `test_fenced_heading_is_not_a_section` written red first, then green.

Verification at `9c13104`: `python -m unittest discover -s tools/tests` 13/13;
`python tools/plan_check.py` → `pass`; `dotnet build Duack.slnx` clean;
`dotnet test Duack.slnx` 293+15+28+43 green.

Verdict: **good**. Human sign-off (perception, "does it sound right") remains out of scope for
agents and is not required for this PR — it is tooling and docs only.

## 2026-11-24 — slice F recording review at head `c74d3c0`: good

Pre-PR `qa` review of the working diff that records slice F (bus unification, #36) in the
plan workspace before any implementation: `docs/plans/vnext/bus-unification/PLAN.md` (new)
plus the slice F row in `docs/plans/vnext/README.md`. Docs only, no code, no public API.

- Head at review: `c74d3c0` (base `master`)
- Verdict: **good** — diff contains exactly the two claimed files (37 insertions, no quiet
  scope); PLAN.md faithfully carries issue #36's problem, seven scope bullets, non-goals,
  gates M/D and acceptance criteria with nothing dropped or added beyond the issue; every
  relative link resolves; quoted claims verified line-by-line against `user-buses/slice.md`
  (qa verdict lines, D1–D5), `user-buses/review-structure.md` (W1/W2, Non-work list),
  `user-buses/STATUS.md` (Gate 2), `user-buses/PLAN.md` ("per-bus readback of the stages"),
  and `SHARED.md` (§0/§3/§8/§9 citations); slice lettering F does not collide with A–E and
  contradicts no SHARED.md §11 sequencing reference; `STATUS.md`/`slice.md` correctly absent
  per the start-the-slice convention; walk test passes from the files alone;
  `python tools/plan_check.py` → `pass`.
- Non-blocking, fixed in the follow-up commit carrying this record:
  - "Docs ride in the same PR (SHARED.md §8)" cited the wrong section — the discipline is
    §9 (preview-API, matching skills updates). Corrected.
  - D1 attribution pointed at review-structure.md §4; D1 is recorded in §3, §4 holds the
    Non-work list. Corrected to name both.
- Non-blocking, left as-is: the pre-existing slice E README row says "needs bus tree" while
  D1 records hierarchy as rejected — wording predates slice F and is maintainer scope.

The same follow-up commit adds the slice F checkbox (`(#36)`) to
`plan/01-vnext/phase-01-vnext-slices.md`, which the review caught as missing from the
original recording.

## 2026-11-25 — slice F phase-gate review of `feat/bus-unification` (`c74d3c0..8facac5`): good

Full plan-phase PR-gate `qa` on the whole slice F working diff (14 commits, 32 files,
+1127/−190 against `master`) before any PR opens. Contract: `bus-unification/PLAN.md`,
constrained by issue #36, `user-buses/review-structure.md` D1–D5, and `slice.md` D6–D26.

Evidence run independently by the reviewer: `dotnet test Duack.slnx` 420/420 green across
all four test projects; `samples/Duack.Sample.Web` builds clean; `python tools/plan_check.py`
→ `pass`.

Verdict **good** — no blocking findings: every scope bullet implemented (config table,
unified resolver, the `"sfx"` fold with `BREAKING CHANGE` footer as a single-line revert
unit pinned both ways, `Views` duck readback, web per-bus gain nodes, routing unchanged);
non-goals respected (no parent/solo/snapshots/effects, ctor signatures unchanged D7, no
per-bus player settings); all slice C pins hold with equivalent coverage (W1/W2,
`BusFellBack`, both `ZeroEffectiveGain` cases, ref-counted holds, composition order);
changelog entries match behavior with dated `(#36)` refs; commit discipline clean;
SKILL.md/README verified against the compiling API with no planning vocabulary leaked;
core purity intact (empty-cache restore passes upstream); recorded deviations (D13, D18,
surface re-types) properly decisioned and changelogged. Checkbox correctly unticked —
pending the human Gate D listening on both backends.

Non-blocking findings, applied in the follow-up commits carrying this record:
- CS8602 null-deref in the web sample's new `M`/`N` handlers → fixed with the file's
  existing `_sfx!` idiom; sample builds warning-clean.
- BOM introduced in 20 diff files (three more carried it on master, left untouched) →
  stripped; trailing newlines restored.
- `SampleAudio`'s Linq usage was implicit-usings-dependent → explicit `using`.
- `buses` SKILL.md stated custom-bus creation only via a code comment → an explicit
  sentence added in Setup (maintainer feedback during review).
Fourth observation (slice decisions recorded in preceding `docs(plans)` commits rather than
inside each work-item commit) left as-is: it matches master's established practice
(d547980, 78195d8) and each decision names its work-item commit explicitly.

## 2026-11-25 — slice F phase-gate re-review of the grown diff (`8facac5..5a9592e`): good

Re-review before the slice F checkbox tick, after five maintainer listening rounds and the
fixes they demanded (D27–D31: no-bleed KO, entry cues on old screens, non-interactive heads,
asset-sized beats, web warm-up). Reviewer: same `qa` session as the phase-gate review
(full prior context); delta scope deep-checked, full-diff gates re-verified.

Evidence run independently: `dotnet test Duack.slnx` **427/427 green** (329+21+34+43);
`samples/Duack.Sample.Web` builds with 0 warnings (the two earlier CS8602 are gone);
`python tools/plan_check.py` → `pass`; keyboard remnant grep clean on both story heads
(`Advance`/`Restart` called by no head, matching the D29 changelog claim).

Verdict **good** — no blocking findings: the delta adds no unplanned library scope
(`src/Duack` changes are BOM-strip only; behavior change confined to the sample core,
the KNI adapter seam, and changelog, exactly what the listening rounds demanded); the
beat machine is coherent (all four flags reset on `Restart`/`EnterBattle`, beats unskippable,
full-loop pin asserts no frame mixes a cue with a scene or music switch); `IWebAudioNative`
growth (`WarmHttpCache`, `ReleaseAllBuffers`) is changelogged per the D20 pattern with
dispose semantics pinned; every delta surface has a dated `(#36)` changelog entry that
matches the mechanics; customer docs carry no planning vocabulary and their examples compile
against the current API; commit discipline holds (one change per commit, `Refs: #36`, cliff
types parse); the earlier non-blocking findings (BOM, newlines, CS8602, implicit Linq) all
addressed.

Non-blocking observations from this round, applied (`d5a273d`) or recorded: the
`STATUS.md` open-questions closure and this record ride the tick commit; the one-line
brace/attribute pair in `WebAudioSfxBackendTests` and the web README's "Start story"
naming fixed beforehand; the WAV-header pin's location-sensitive lookup recorded as the
accepted tradeoff of sizing beats from the actual assets (D30).

Ruling: the slice F checkbox may tick — all agent-verifiable acceptance criteria green
(427 tests incl. wrapper-equivalence and both-way fold pins), deferrals honestly recorded
(per-bus voice counts, bus-targeted-duck validation), and the human listening obligations
met and decisioned by the maintainer across five rounds. The tick, the README row flip
to landed, and `STATUS.md` closure land as one commit with this record, per the plan-phase
gate; nothing in it is code, so no changelog entry attaches.


## feat/voice-concurrency — Slice A — voice concurrency limiting (resumed 2026-11-25)

- Reviewed: 2026-11-25
- Head at review: the branch rebased onto `master` this session (`06a67fa`…`3a2a0ba`, plus the
  fixes below)
- Base: `master` (`9029c12` — the bus-unification fold, #37)
- Verdict: **good** — after in-session fixes; Slice A remains unmerged pending the human
  listening check (#38)
- Reviewer: agent (`/pr-review` gate run without the OpenCode harness; qa criteria applied
  from AGENTS.md — the diff against the slice PLAN is the contract)

### In-session fixes (blocking findings, all verified)

**F1 — Branch conflicts with the landed bus unification (#37).** `git merge-tree` reported
content conflicts in `CHANGELOG.md`, `src/Duack/AudioConfig.cs` and `src/Duack/SfxManager.cs`;
the branch predated #37 (old `Dictionary<string, float>` user buses vs the flat
`BusConfig` table). Rebased onto `master`; in `PlayDetailed` the bus-unified composition
(`_buses.Resolve(BusNames.Master).Gain * resolution.Gain * _music.DuckRatioFor(...)`) is kept
and the limiter admission, zero-gain short-circuit (before the limiter — a mute/zero play
consumes no slot, reporting `Played` + `ZeroEffectiveGain` exactly as master does) and the
additive `PlayDetailed`/`LastPlayResult` API sit on top. Suite green after the rebase
(440 tests: 342 + 34 + 21 + 43).

**F2 — Stale pre-split slice doc.** The branch still carried
`docs/plans/vnext/slices/A-concurrency.md`, referencing the deleted
`docs/plans/vnext/PLAN.md §4`; it duplicated `voice-concurrency/` (removed in `ff47a4f`).

**F3 — Missing override pins.** The limiter honored per-cue `DurationSeconds`/`StealingPolicy`
but no test pinned either at the limiter level. Added
`Per_cue_lifetime_and_policy_overrides_win_over_the_defaults` (passes; coverage pin, no
production change).

### Verified against the slice PLAN acceptance

Caps enforced at exact boundaries with the victim sequence asserted (oldest default, quietest
with oldest tiebreak, priority refuse/steal at `<=` the weakest); per-cue cap override over
global; every refusal reported (`UnknownCue`/`RejectedBelowPriority`/`BackendRefused`) with
`LastPlayResult` readable for plain `Play` callers and `ActiveVoiceCount` for HUD readback;
backend refusal releases the reserved slot; unknown cues never touch the limiter; CHANGELOG
entry, `duack-skills/concurrency/SKILL.md` and the dedicated desktop example present.

### Non-blocking / still open

- The human listening check on `Duack.Sample.Concurrency` (maintainer; #38 criterion).
- `PlayDetailed` collapses a zero-gain play into `Played` (master's inherited semantics —
  reported via `ZeroEffectiveGain`); a distinct outcome member would be more honest in the
  detailed API if the maintainer wants it.
- The cross-platform follow-up (web `Update(dt)` wiring, audible-stealing seam) landed on this
  branch in the follow-up commit of 2026-11-25 (#38): additive `ISfxVoiceControlBackend` seam,
  desktop/web implementations, web-head limits + `Update(unscaledDt)`. Web audible verification
  remains a browser-listening obligation; the JS audio graph is pinned at bridge-call level only.


## feat/voice-concurrency — full re-review after follow-up + rebase (2026-11-25, second pass)

- Reviewed: 2026-11-25
- Head at review: `28dee53` (branch fully rebased onto `origin/master@7b56ce7` — bus unification
  #37 and changelog-convention #39 both in; the audible-stealing follow-up, clickable buttons,
  docs-writer rounds and de-slicing all landed)
- Base: `origin/master`
- Verdict: **good** — one real defect found and fixed in this pass; analytics below re-verified
  against the live head

### Defect found and fixed this pass

**JS bridge id-map corruption (fixed in `webaudio-sfx.js`).** `playSource`'s `onended` closure
deleted `nextSourceId` (the mutable module counter), not the id of the source that ended:
after any voice ended naturally, the latest source's mapping was deleted, so a later
`StopVoice(handle)` for a still-playing voice could no-op — audible stealing silently failing
on the web head. Fixed by capturing `const id = nextSourceId++` at creation. The C# seam tests
pin the bridge call contract but cannot exercise the JS graph (the honest-subset limitation
already recorded for the web backend); this defect is exactly the class of bug that pattern
cannot catch, and the browser listening check remains the real verification.

### Re-verified in this pass

- Diff-vs-contract: the branch implements the slice PLAN (caps, policies, per-cue overrides,
  observable results, dedicated example) plus the recorded cross-platform follow-up (seam, web
  wiring); no unplanned scope. Merged `PlayDetailed` flow verified end-to-end: bus composition
  (master→bus→mute/pause→duck) → final clamp → zero-gain short-circuit (event + `Played`, no
  backend call, no slot) → limiter acquire (refusals reported) → steal-stop via the seam →
  tracked/plain play → slot release on backend refusal → duck pulse.
- Consumer-surface hygiene per the #39 rule: no slice/phase/ADR/decision identifiers remain in
  `src/`, `tests/`, `samples/`, `duack-skills/` or this branch's changelog entries
  (case-insensitive scan). Master's own "D5"/"Gate D fold" comment in `SfxManager.PlayDetailed`
  predates this branch and was left untouched.
- Changelog claims match the code, including "registry cleaned on onended/stopSource/StopAll"
  (true after the JS fix above) and the `IWebAudioNative` implementor contract note.
- Green: build 0 errors, 450 tests (348+38+21+43), both sample projects build (web explicitly,
  it is out of the solution), `python tools/plan_check.py` → `pass`.

### Still open (unchanged, maintainer-owned)

- Human listening: `Duack.Sample.Concurrency` (PLAY/MANY/STOP; now also judging that stolen
  voices audibly stop) and the browser check of the wired web head.
- Ruling wanted: `PlayDetailed` reports zero-gain plays as `Played` (master semantics).


## feat/voice-concurrency — final PR-review gate (2026-11-25, third pass)

- Reviewed: 2026-11-25
- Head at review: reviewed at `1a5ed3a8` with the AGENTS.md/web-comment fixes in the working
  tree; those fixes (plus this record) land in the follow-up commit, so the verdict's head is
  the commit carrying this entry. 20 commits over `origin/master` `7b56ce78`; 29 files,
  +1489/−59
- Base: `origin/master`
- Verdict: **good — PR-ready.** All agent-verifiable slice criteria and follow-up items pass;
  the two human checks remain maintainer obligations and are not ticked.

### Checks in this pass

- Sweep of the full tracked diff (all 29 files) against the slice PLAN and the recorded
  follow-up: caps, policies, per-cue overrides, observable results, dedicated example, seam,
  web wiring, docs, changelog — no unplanned scope, nothing incomplete in the diff itself.
- Hygiene scan forced over every git-tracked file (previous scans accidentally included
  build-output directories and missed bare "slice" in three comments): zero planning-vocabulary
  or conflict-marker hits outside the planning workspace, the plan-tooling docs, and master's
  pre-existing changelog lines — all of which are the vocabulary's legitimate home per #39's
  own text. Found and fixed in this pass: "Slice A" in AGENTS.md's sample listing and two
  "(PLAN addendum)" references in the web-head comments (now `#38`).
- Consumer-facing surfaces (docs-writer rules): README sample table now lists the demo;
  config skill's See-also now routes to `buses` and `concurrency`; button labels are the
  vocabulary-free PLAY/MANY/STOP; the skill doc claims only numeric observable behavior
  (counter never exceeds 08) and explicitly defers the audible verdict to the human check.
- JS bridge re-read after the id-capture fix: sourcesById is keyed by the captured per-source
  id, cleaned on natural end, stopSource, and stopAll; StopVoice no-ops on unknown/stale ids.
- Verification: solution build 0 errors/0 warnings; 450 tests green (348 + 38 + 21 + 43);
  web sample builds explicitly (solution-excluded by design); plan_check → pass; plan tool
  unittests → OK.

### Maintainer obligations before merge

- Human listening: `Duack.Sample.Concurrency` (PLAY fires one cue; MANY fires 50 overlapping
  cues against the cap — judge whether stolen voices audibly stop; STOP resets); browser check
  of the wired web head (user-gesture start, worst-moment standard).
- Optional ruling: `PlayDetailed` reports zero-gain plays as `Played` (inherited master
  semantics; `ZeroEffectiveGain` is the report channel).

## feat/spatial-sfx — restart review of the unmerged slice B work (2026-11-25): blocked

- Head at review: `d4a1a23` (merge of `master` into `feat/spatial-sfx`)
- Reviewer: `qa` subagent, run ahead of the slice B restart on #34.
- Verdict: **blocked — do not merge, do not resume from it.** The slice restarts
  fresh from `master` on `feat/positional-sfx`.

### Blocking

**F1 — the branch tip's tests pin an API its own tree does not contain.** The merge
`d4a1a23` resolved `SfxManager.cs` in favor of master, dropping `f8d22ee`'s entire
positional wiring (`SetListener`/`PlayAt`/`UpdatePosition`/`IPositionalSfxBackend` absent
from the tip), while `SfxSpatialTests.cs` still calls them and the changelog claims they
shipped. Verify: `git show feat/spatial-sfx:src/Duack/SfxManager.cs | grep -c PlayAt` -> 0.

**F2 — the changelog documents phantom adapter features.** The 2026-11-23 entry claims
MonoGame `IAudioInstance.Pan` application and a KNI `playPannedSource`/`StereoPannerNode`
bridge that exist in no commit (`git grep -i playPannedSource feat/spatial-sfx` matches
only CHANGELOG.md).

### Carried into the restart design

- A zero-gain positional play consumed a limiter slot (the mute floor must release it).
- The cross-backend parity claim was false: desktop `SoundEffectInstance.Pan` is a linear
  platform taper, web `StereoPannerNode` is constant-power; the restart records the
  difference (STATUS.md) instead of asserting agreement.
- The dead `SetCueMix` seam keyed live re-mixing by cue name, so two overlapping voices
  of the same cue re-mixed as one; re-mix is restarted per voice handle.
- Non-finite positions propagated unguarded into the pan curve.
- Surface cut to the slip-rule defaults: two distance models (linear, inverse) and two
  pan knobs (`PanSaturation`, `PanDistance`).

Salvaged in spirit and re-derived from scratch: the `SpatialMath`/`SpatialConfig` shapes
and their SHARED section-12 test properties, the ADR-11 input-seam API, and the web voice
lifecycle pattern (onended disconnect + tracked id map) the panner calls layer on.

## feat/positional-sfx — slice B restart phase-gate review (`7cb71c6..26df41e`, 2026-11-25): good

- Head at review: `26df41e`
- Reviewer: `qa` subagent; ran `dotnet test` (393 CodeOnly/JsonRoundTrip, 43 adapters, all
  green) before ruling.
- Verdict: **good.** The restart resolves every blocking finding of the previous entry:
  the mute floor composes to zero before the limiter (no slot consumed, reported through
  `ZeroEffectiveGain`), positional gain rides the unified bus chain and re-mix re-resolves
  it live, re-mix keys by voice handle (two overlapping same-cue voices independent), the
  remix-record map is pruned at 2× the assumed voice lifetime, and the JS bridge is one
  leak-free contract in both shipped copies.
- Non-blocking caveats recorded: desktop pan reproduces through the platform's linear taper
  vs the web constant-power law (documented, unquantified); stereo correctness at
  `|pan| -> 1` rests on the human-listening gate (open on #34); per-voice bus re-resolve in
  `RemixVoiceNow` is fine at current voice counts, a note if they grow.

## PR #45 — docs: resync slice B plan state with merged #41, retarget acceptance remainder to #44 (2026-11-25): good

- Head at review: `3c0f6c8` (commits `9ab6fbe`, `3c0f6c8`), base `master`.
- Reviewer: qa pass per `.opencode/commands/pr-review.md`; CI `build-and-test` green, `plan_check` pass.
- Verdict: **good.** The three-file resync is accurate against the live state: #41 merged
  2026-11-25, #34 closed, the phase checkbox keeps #34/#41 and names #44 for the remainder,
  the README slice B row and the STATUS.md "Awaiting" heading both retarget #34 → #44, and
  no code is touched. Both commits end with `Refs: #44`.
- The same PR adds the qa-verdict checkbox rule (AGENTS.md rules of engagement,
  pr-review.md step 6): a good verdict that completes a phase checkbox lands a small docs
  commit ticking it and moving its plan index row; boxes with remaining acceptance
  (listening, samples) stay unticked. Consistent with "a state changed but no file did
  not happen"; not ticked here because PR #45 completes no checkbox.
- Non-blocking observations, pre-existing on master and out of scope here: the phase doc
  ticks Slice C `[x] (#31)` while the README slice row still reads "in progress" after the
  Gate 2 adoption note; Slice E has no issue number on its checkbox
  (`plan/01-vnext/phase-01-vnext-slices.md`) and the index row for E in this doc says
  "draft", both worth their own resync pass.

## PR #47 — feat: named mix states (core), slice E start

- Reviewed: 2026-11-26
- Head at review: `d39895422209a3ae2fbd4b1a990ffc647775c75d`
- Base: `master`
- Verdict: **good**
- Reviewer: `qa` pass against `.opencode/agents/qa.md`; blocking candidates independently verified on the head (CI `build-and-test` green at 39s — the tests the sandbox could not run)

### Blocking

- (none)

### Medium

**F1 — PR body would auto-close the tracking issue before acceptance is done.**
- Rule: workspace convention (slice B precedent — the acceptance remainder got its own open issue #44); `AGENTS.md` changelog/issue discipline.
- Proof: the PR #47 body ends with `Closes #46`, so merging would close #46 while the slice's acceptance remainder (two-backend sample, human listening) is still open and recorded in `mix-states/STATUS.md`.
- Required: reword the PR body so #46 stays open, or retarget the remainder to a #44-style issue.
- Status: fixed — PR body reworded the same day; the remainder stays on #46.

### Low

**F2 — Red-first TDD not provable from the commit shape.**
- Rule: `AGENTS.md:21-30` (write the failing test first, confirm red).
- Proof: `efd90d0` adds tests and implementation in one commit, so no file-only reader can confirm the tests were red before the code existed; the session that wrote them wrote them first but persisted nothing of the red run (no local SDK existed to run one).
- Required: nothing for this PR; the pattern to prefer is a red-test state observable in history (green CI on a test-only commit, or the commit message recording the observed red).
- Status: open (informational).

**F3 — `AudioConfig.MixStates` setter's null fallback drops the Ordinal comparer.**
- Proof: `src/Duack/AudioConfig.cs` — setter builds `new Dictionary<string, MixStateConfig>(StringComparer.Ordinal)` only on null; a hand-assigned non-Ordinal dictionary is accepted as-is, unlike the field initializer.
- Required: none for this PR (authoring is builder-only, Ordinal); note for any future hand-composition.
- Status: open (informational).

### Checked and clear

- Pure BCL held: no package or project reference added to `src/Duack` (only `System` namespaces; `MusicManager.Buses` and all mix-state surface are additive, no preview-breaking change — the changelog entry says so accurately).
- Changelog: dated `#### 2026-11-26` under `### Added` with `(#46)`, matching the diff.
- Tests targeted and green: `tests/Duack.Tests/CodeOnly/MixStateTests.cs` pins authoring/clamping, instant apply on both mixers, omitted buses untouched, the §12 one-pole closed form (`tau = duration / ln(1/SettleEpsilon)`), monotone easing settling exactly at the duration, telemetry, loud `UnknownState`, composition order (state target = bus volume, duck live on top, player slider layers after), player-settings carryover, and the `MusicManager.Buses` readback; CI `build-and-test` pass at the reviewed head.
- Edge cases: unknown state names (reported, mix untouched), NaN and zero/negative duration (NaN/≤0 ease = instant apply), state naming a bus missing from a mixer (the `SetBus` authoring asymmetry, slice D2), a transition restarted while running (recaptured from current volumes).
- Hot path: `MixStateManager.Update` allocates nothing per frame.
- Plan-phase gate: the slice E checkbox stays unticked on purpose — the verdict clears the core but acceptance items remain (two-backend sample, human listening), so per the `/pr-review` rule nothing is ticked and the remainder stays on #46 (mirroring the #34 → #44 retarget).
- Walk test: `tools/plan_check.py` prints `pass`; `docs/plans/vnext/README.md` slice E row and `mix-states/STATUS.md` describe the true branch state.
