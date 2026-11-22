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
