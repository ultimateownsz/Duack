# Handoff: BusMixer structure — review verdict and work items

Duration: none (design review + task package)
Status: verdict issued, work not started
Constraint source: AGENTS.md at repository root
Reviewer: skeptical-reviewer (verdict only, no fixes)
Owning agent: primary session agent (or refactoring-specialist for the fix items)
Phase context: docs/plans/vnext/user-buses/ (STATUS.md records the Gate 2 default this review ratified)

This file is self-contained. A fresh session with no memory must be able to
orient, act, and report status from this file plus AGENTS.md and the phase doc
alone. Do not rely on conversation history.

## 1. Question reviewed

Whether Duack's user-bus structure should be authored around an alternative
bus model: a hierarchical parent-chain gain tree, solo flags, snapshots, a
per-bus effect hook, and CamelCase bus names. Verdict below. The constraints
come from Duack's own sources (AGENTS.md, README scope promise, existing
config and test surface), so the decision is recorded without referencing any
external engine.

## 2. Verdict

Author the BusMixer as specified in the phase doc: flat authored buses, fixed
`sfx` fallback with `BusFellBack`, `Changed` as the frontend surface, live gain
resolution, no parent/solo/snapshots/effects. Ratified reasons, each verifiable
against Duack sources:

1. A parent-chain gain tree is middleware scope. The README promises "No
   editor, no mixer graph, no content pipeline beyond your audio files and
   none are planned." A hierarchical bus tree with parent gain folding is a
   mixer graph. No observed Duack game has needed nested bus attenuation;
   Master → music/SFX with per-context scales covers the documented cases.
2. A per-bus effect hook is speculative API in a pure-BCL assembly. src/Duack
   must stay BCL-only (System.Text.Json allowed). An interface nothing
   implements is untestable dead surface; "test written first" cannot be
   satisfied against a never-populated hook. When an effect-capable backend
   ever lands, the hook shape would be guesswork anyway.
3. Bus naming stays lowercase and Ordinal-compared. `BusMixer.SfxBusName` is
   `"sfx"`; the existing duck-source name in config is `"sfx"`; the volume
   chain is already Master → music/SFX in authored config. Under Ordinal,
   `"SFX" != "sfx"`: any casing change or rename silently pushes existing
   play/routing strings through the fallback path. See verify step V1.
4. Buses expose no public mutable fields. A Blazor/KNI frontend (the stated
   consumption pattern in the samples) must be able to render bus state
   without owning or polling it; `Changed` is the load-bearing surface.
   Field mutation would break that contract.
5. Gain resolves live, not once at play time. Resolve-once semantics make hot
   volume updates miss in-flight cues, contradicting the advertised
   centralized volume management and hot-update support.
6. Ducking stays in the existing duck system (duck sources with ratio/attack/
   hold/release). Bus gain never embeds duck multipliers; folding duck into
   bus state creates two owners of duck phase accounting, and duck phase
   boundary behavior must stay testable in one place.
7. No solo flag. Duack has no editor, no observed caller, and solo couples
   one call to every bus's effective gain and `Changed` fan-out.
8. No snapshot transitions. That is scene-lifecycle middleware; if demand ever
   surfaces, it gets its own phase doc.

Perceptual questions (does a live gain change sound as smooth as some
alternative) are out of scope for agents; route to the human. Numeric claims
(event counts, ordinal key equality, fallback paths, tick accounting) are
unit-testable in the existing test project.

## 3. Decisions to record

Record these in the phase doc or a decision note under docs/plans/vnext/
user-buses/ so the structure question does not resurface:

- D1: Flat buses only. No parent-chain tree, no per-bus effects, no solo, no
  snapshots.
- D2: Bus naming stays lowercase, Ordinal-compared, config-seeded; `sfx` is
  fixed and must not be renamed or re-cased.
- D3: `BusMixer` keeps `Changed`/`BusFellBack` as the only mutation surface;
  no public mutable fields.
- D4: Ducking stays in the existing duck system; bus gain never embeds duck
  multipliers.
- D5: Live gain resolution, not resolve-once-at-play.

## 4. Work items (TDD order: test first, then fix, then changelog)

Each item: write the failing test, then the production change, then the
changelog entry under `## Unreleased` in the right category and dated
subsection. If this work lands as part of a phase PR, the phase checkbox and
index status tick in the same commit as the work.

### W1 (medium): `SetBus` fires `Changed` unconditionally

Observed: `SetMute` and `SetPaused` guard on same-value; `SetBus` does not. A
hot-update loop calling `SetBus(name, v)` each frame re-notifies all
subscribers per frame.

Repro/verify: call `SetBus(name, v)` twice with the same value; assert `Changed`
fired once.

Fix: same-value guard. Exception: a bus created by the first call is a state
change and must still notify.

### W2 (medium): `SetBus` on an unknown name silently creates a bus

Observed: `Resolve` on unknown names raises `BusFellBack` (no-silent-failure),
but `SetBus` creates a new bus on an unknown name, the opposite policy in one
class. A typo in a hot-update turns a routing mistake into an invisible bus.

Decide: (a) create is intentional for authored-config hot updates, or
(b) raise/notify on unknown. Whatever the phase doc says, pin it with a test
written first and document the asymmetry with `Resolve` in the doc comment.
Do not leave the policies split without a written reason.

### W3: changelog + bookkeeping

- One `## Unreleased` entry per work item, right category and dated subsection,
  in the same commit as the change.
- If this ships inside a plan-phase PR: checkbox and index status tick in the
  same commit.

### Verify step V1 (before any naming change, now or later)

Search every literal `sfx`, `Sfx`, and `SFX` across src/, tests/, samples/,
tools/, and duack-skills/ and count call sites before touching the bus name.
Any rename or re-casing must update all of them in one commit or be rejected.

### Non-work (explicitly rejected scope)

Do not add: parent buses, solo, snapshots, effect/DSP hooks, bus-embedded duck
state, CamelCase bus names, public mutable bus fields, gain resolved once at
play time. If someone re-raises this, point at section 2 and the phase doc
decisions; unknown-bus lookups surfacing via `BusFellBack` is the library's
no-silent-failure principle applied to routing, not a bug to simplify away.

## 5. Handoff procedure

1. Copy this file to docs/plans/vnext/user-buses/ (or wherever review notes for
   the phase live) and commit it before any code changes, so the walk test
   passes: a fresh session orients from files alone.
2. Record the section 3 decisions in the phase doc.
3. Hand W1/W2 to an agent with the existing user-buses phase context, AGENTS.md
   loaded, with the constraint: tests written first, matching changelog entry,
   no scope beyond W1/W2.
4. Re-review: the additions then go back to skeptical-reviewer for a fresh
   verdict against AGENTS.md before merge.
