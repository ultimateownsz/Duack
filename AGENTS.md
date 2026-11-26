# AGENTS.md

Duack is a small, backend-agnostic audio library (mixing, ducking, crossfade, player
settings) extracted from a production game and published as preview NuGet packages.

## Changelog discipline

Every code change is recorded in `CHANGELOG.md` under `## Unreleased`, in the appropriate
`### Added` / `### Changed` / `### Fixed` / `### Known Limitations` category and a dated
`#### YYYY-MM-DD` subsection before it lands. For fixes, record the symptom plus enough
reproduction detail to re-verify or revert. Reference the related PR/issue (e.g. `(#123)`)
so it surfaces in the auto-generated release notes. Do not edit code without reflecting it there.

Identify changes by date and PR/issue number only — never by plan-slice or
phase names ("slice F's readback", "phase 02 work", and so on). The dated
subsection and the issue reference already say everything a reader needs;
slice names rot: every entry that names a slice is a manual fix-up none of
the tooling can keep consistent. Slice/phase tracking belongs where it is
maintained automatically: the plan workspace (`docs/plans/vnext/`, `plan/`).

Documentation itself (plain-English tone, minimal why-only comments, the
XML doc contract for public API) is the `docs-writer` agent's brief; consult
`.opencode/agents/docs-writer.md` before writing or reviewing prose docs.

## Test-Driven Development (non-negotiable)

Any refactor of existing behavior and any creation of new behavior is written test-first:

1. **Red** — write a failing test that pins the expected behavior. Run it and confirm it
   fails for the reason you expect.
2. **Green** — implement the minimum production change to make it pass.
3. **Refactor** — clean up, then re-run the whole suite (both `CodeOnly` and
   `JsonRoundTrip`), not just the new test.

A change that ships with no test needs a written reason in the changelog entry.

## Hard architectural rules

- `src/Duack` is **pure BCL** (`System.Text.Json` only). It must build and test with no
  engine/XNA/MonoGame/KNI package present. Never add a package or project reference to it.
- Backend glue (engine, XNA, browser interop) lives in adapter packages, not the core.
- **This preview is code-first.** The configuration surface is the C# API
  (`AudioConfigBuilder`, `PlaylistConfigBuilder`, model objects). Authored audio configuration
  has no `audio-config.json` compatibility layer in master; the old implementation remains on
  `legacy/pre-refactor`. JSON remains only for active persistence/tooling formats such as
  standalone playlists, audio manifests, and player settings.
- Playlists have their OWN source-generated context (`Duack.PlaylistJson`), so the AOT web
  head can read standalone playlist files at runtime; a game may author them in code with
  `PlaylistConfigBuilder`. The CLI's `PlaylistFile` delegates to `PlaylistJson`, so tool and
  runtime never drift on the format.

## Layout

- `src/Duack` — the library (pure BCL).
- `src/Duack.MonoGame` — desktop adapter: `IMusicBackend` over MonoGame `SoundEffectInstance`,
  plus `SoundEffectSfxBackend` (the core `ISfxBackend`) over the clip seams.
- `src/Duack.KNI` — web adapter: `IMusicBackend` over the platform `MediaPlayer`, plus the browser
  `localStorage` settings storage.
- `tests/Duack.Tests/CodeOnly` — behavior with no serialization surface.
- `tests/Duack.Tests/JsonRoundTrip` — active player-settings and manifest wire-format pins.
- `tests/Duack.Adapters.Tests` — behavior of the two adapter packages, driven through their
  headless seams (fake clip library / fake media-player host); no audio device or browser needed.
- `tools/DuackCli` — the desktop-only `duackcli` CLI (`convert`/`scan`/`normalize`); the only
  place `NAudio`/`OggVorbisEncoder` live. Never referenced by `src/` or `samples/`. See its
  `README.md` for the CLI and standalone playlist format.
- `tests/Duack.Tools.Tests` — the tool's pure logic plus CLI end-to-end (exit codes +
  filesystem effects) against temp directories.
- `samples/Duack.Sample.Core` — the engine-free sample core both heads share: the scripted
  storyline (`StoryController`) and the demo wiring (`SampleAudio`), pure BCL over `src/Duack`.
- `samples/Duack.Sample.Desktop` — MonoGame storyline demo (walk → dialog → battle) over the
  two-directory convention; the storyline/wiring come from `Duack.Sample.Core`, pinned by…
- `tests/Duack.Sample.Tests` — …the sample test project (references the core only; headless,
  no graphics device).
- `samples/Duack.Sample.Concurrency` — standalone MonoGame example for SFX voice-limiting
  (50-cue burst, global cap 8, on-screen active-voice counter). Feature examples get their own
  project; the storyline samples stay unchanged.
- `samples/Duack.Sample.Web` — BlazorWASM/KNI music + storyline demo (same `Duack.Sample.Core`
  as desktop; story cues play through `WebAudioSfxBackend` (WebAudio `AudioBufferSourceNode`),
  overlapping music while `DuckMixer` dips the mix). Manifest-driven
  (checked-in `audio-manifest.json` from `duackcli scan`); kept out of `Duack.slnx` (CI may lack
  the WASM workload), so build it explicitly.

## Two-directory audio convention

An audio root holds `SFX/` (small one-shot sounds, wav preferred) and `Tracks/` (loops and
songs, ogg preferred) — users never register cue names by hand. `AudioLibrary.ScanDirectory`
classifies live where the filesystem is available (desktop); browsers cannot enumerate
directories, so `duackcli scan` writes the checked-in `audio-manifest.json` web hosts read back
via `AudioLibraryManifestJson` (source-generated, AOT-safe). Classification is
case-SENSITIVE on purpose (`SFX`/`Tracks` and lowercase `.wav`/`.ogg` only): case-sensitive
web hosts 404 on any mismatch, and `duackcli scan` warns about files it therefore cannot load.
The legacy flat layout (`Content/Audio/{name}.ogg|.wav`) remains a working fallback — the
last candidates in `AudioLibrary.CandidatePaths` order, and the exact probe order of
`SoundEffectClipLibrary.CandidateUris` when no library is supplied — so existing flat
content keeps loading unchanged.
## Planning workspace

Durable state lives in files, never in agent memory. A fresh session
orients from these files alone:

- `plan/plan.md`: index of initiatives and phase status. Read this first.
- `plan/<NN-initiative>/overview.md`: the initiative's problem, pinned
  metric, decision/gotcha ID ranges, and cross-cutting caveats.
- `plan/<NN-initiative>/phase-NN-<slug>.md`: the current phase only.
- `design/deferred.md`: parked ideas with reopening conditions. Read
  before proposing any feature.
- `design/experiments/README.md`: the experiment protocol; outcomes live
  beside it, one doc per experiment.
- `design/TODOS.md`: small one-off items that are not being done this
  minute.
- `design/<subsystem>.md`: subsystem design write-ups. Load on demand.
- `CHANGELOG.md`: shipped history only. Read only when asked about
  releases.

Rules of engagement:

- Work that needs sequencing across independently shippable chunks gets
  a plan initiative; `plan/plan.md` states how the plan lane works.
  Anything else is a one-off: do it now, or put it in `design/TODOS.md`.
  Never inline phase content in `plan.md`.
- Features require a real issue. If `design/deferred.md` already contains
  the idea, check its reopening condition before re-proposing it.
- Experiments answer "would this work", not "should we build this". Docs
  live in `design/experiments/<NN>-<slug>.md`, code lives on
  `exp/<slug>` branches and is never merged as-is, and every experiment
  ends Adopted, Parked, or Rejected with evidence in its doc.
- Mark checkboxes and index statuses in the same commit as the work they
  describe. If a state changed but no file changed, it did not happen.
  Unplanned work becomes a new box or a new phase, never quiet widening
  of the ones that exist. When a good qa verdict on the PR is the gate
  that completes a checkbox, the review pass itself lands a small docs
  commit ticking the box and moving its plan index row, ending with the
  usual `Refs: #<issue>` footer. A checkbox whose acceptance items remain
  open (human listening, samples) stays unticked.
- Commits and branches follow CONTRIBUTING.md: Conventional Commits,
  one branch per phase, named after what it implements.

GitHub issues are an inbox, not state. An initiative records its tracking
issue number in the index row; status and progress never live in issue
comments. A feature request filed as an issue is recorded in
`design/deferred.md` with its reopening condition and closed with a
pointer to that entry. Do not leave wishlist issues open.

Every checkbox names its issue (`- [ ] <step> (#123)`), created before the
work starts. Its title follows Conventional Commits, exactly like a
commit title (`fix:`, `feat:`; a scope may narrow it, e.g.
`docs(audio-core): ...`), and names what the issue ships — never the slice
or phase it belongs to; the plan index row already says which slice, and
AGENTS.md forbids slice names as identifiers. The issue holds the
problem, acceptance criteria and repro; the plan file holds status. End every implementing commit with a
`Refs: #123` footer, so `git log --grep '#123'` lists exactly the commits
behind that box — the set to review, document, or revert when a change
turns out to be a bug or a misunderstanding. Post evidence (review
verdicts, listening results, commit links) to the issue as comments, never
status. Issue templates for work items and bugs live in
`.github/ISSUE_TEMPLATE/`.

Provenance: this workspace is built on the primitives of Interpretable
Context Methodology (ICM: sequence, shelf, catalog; folder structure as
agent architecture, plain files as state). It is not a stock ICM form:
the plan conventions follow FlatRedBall2's public plan workspace, and
the deferred register and experiment protocol are Duack's own. In this
workspace the phase doc serves as the stage contract and its checkbox
tick as the check; every other ICM invariant holds as written.

Walk test you must pass: given only this repo, answer, without any
conversation history: what initiatives are active, which phase is next,
what is blocked and why, and which checkboxes in that phase are still
open. If any answer needed memory, you failed. The scripted portion:
`python tools/plan_check.py` (or `py tools/plan_check.py` where that is
the name) must print `pass`.

When a large plan is made or handed in as one monolithic file, split it into
the per-slice shape `docs/plans/vnext/` uses — an index `README.md` with one
row per slice, a shared `SHARED.md`, and one `<slug>/PLAN.md` per slice —
with `python tools/plan_split.py <PLAN.md> [--out
<dir>] [--slug A=<slug>] [--force] [--remove-input]`. It writes an index
(`README.md`), a `SHARED.md` holding every non-slice section with its `##`
headings intact (so `§N` references still resolve), and one `<slug>/PLAN.md`
per `Slice X — Name` section. Slugs come from the slice name unless overridden;
generated statuses are `draft` and must be filled in by hand. It never
overwrites existing output without `--force` and never deletes the input
without `--remove-input`. Create `STATUS.md` and `slice.md` only when a slice
starts. Tool tests: `python -m unittest discover -s tools/tests`.

Maintainer release notes: `git-cliff --tag <version>` (conventional commits).
Release authority is enforced by repository permissions (write access + tag
creation), not by documentation.

## OpenCode agent

`.opencode/agents/` holds three subagents launched with the `subagent`
tool. They treat this file as their constraint source and never
restate its rules. None of them keeps state in memory: verdicts and
notes land in the PR review or the planning files, re-derivable by a
fresh session (the walk test applies to agents too).

- `qa` - skeptical reviewer, never fixes. Run it on the working diff
  before opening a PR and on any open PR diff before merge.
- `refactoring-specialist` - the only agent that edits code.
  Refactors test-first and runs targeted suites only; the unfiltered
  whole-suite run happens once at phase end, not as a reflex.
- `docs-writer` - maps landed changes to `CHANGELOG.md`,
  `duack-skills/`, `docs/` and `README.md` before the shipping PR
  opens.

Use `/pr-review <number>` (`.opencode/commands/pr-review.md`) to run the full loop: `qa` on the
PR diff, independent verification of blocking findings, and a dated verdict recorded in
`docs/plans/vnext/REVIEWS.md`. It reports findings without editing code or posting a GitHub
review unless asked.

No agent listens to audio: perceptual verdicts (does the crossfade
sound right) belong to a human, while numeric ones (curve shape, dB
depth, tick accounting) are agent-verified.

Plan-phase PR gate: before its checkbox may tick, `qa` reviews the PR
diff against the phase doc, which is the contract. The verdict is
good only when the diff implements exactly the contract, adds no
unplanned scope, carries green targeted tests and its changelog
entry, and the checkbox and index status are ticked in the same
commit. Otherwise the verdict is blocked with the failing criterion
and file and line proof, recorded as the PR review. A blocked PR
never ticks its checkbox.

## Build & test
```
dotnet build Duack.slnx
dotnet test Duack.slnx
```

The tool project uses a distinct assembly identity (`AssemblyName duackcli`): naming it bare
`duack` collides case-insensitively with the core `Duack.dll` on Windows, and MSBuild then
silently drops the tool assembly from the test project's references (`CS0103` against a type
that clearly exists). `duackcli` is both the assembly name and the command users invoke
(`ToolCommandName`); the package ID remains `DuackCli`.

To prove the core stays BCL-only, restore into an empty package cache:

```
$env:NUGET_PACKAGES = "$env:TEMP\duack-empty-cache"
dotnet test tests/Duack.Tests
```
