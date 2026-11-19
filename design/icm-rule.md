# ICM Rule: Multi-Phase Planning Workspaces

Rule for structuring planning and long-running implementation work as ICM workspaces, combining ICM's "folder structure as agent architecture, plain files as state" with the plan-index mechanics proven in FlatRedBall2's `plan/` folder.

## Principle

Work too large for one session or one PR is not bigger prompts; it is structure. The planning state must live as plain markdown in the repository, versioned and portable. An agent with no memory must orient, act, and report status from the files alone (the walk test). Never rely on built-in agent memory, session history, or changelogs-as-context for in-flight coordination.

## Workspace layout

```
plan/
  plan.md                                  <- index, never content
  <id>-<initiative-slug>/                  <- one folder per initiative
    phase-01-<slug>.md
    phase-02-<slug>.md
    ...
design/
  TODOS.md                                 <- small one-off items, no plan doc
  <subsystem>.md                           <- subsystem design write-ups
CHANGELOG.md / release notes               <- shipped history only, not coordination
```

## Rules

1. Index is routing, not content. `plan.md` holds one row per phase with a link and a status. Never inline phase detail there. An agent reading only `plan.md` must know what exists, where it lives, and what state it is in.
2. Every phase owns exactly one doc, and that doc is all a newcomer gets. It carries the problem being solved, the shape of the resolution, and the work as tickable checkboxes. No external context required, not the tracking issue, not a chat history, not the maintainer's memory.
3. Phase docs are written when the phase is about to start, and not before. A phase planned far in advance stops describing the work and starts predicting it; by the time it is read, it will have been wrong for weeks.
4. The checkboxes are the state of the world. Ticks land in the same commit as the work they describe. Work nobody planned becomes a new box or a new phase, never a quiet widening of one that already exists.
5. Status vocabulary. `Not started` / `In progress` / `Blocked` / `Landed`, updated in the index row the moment the phase state changes. The index is the agent's status report and must always be current.
6. Finished initiatives keep their folders, every row showing `Landed`. The docs remain as the answer to why a design looks the way it does; the workspace deletes nothing that carries reasoning.
7. Threshold gate. Only work that needs sequencing across multiple independently-shippable chunks gets a plan folder. One-off changes go to `design/TODOS.md` or just get done. No plan folder for single-session work, otherwise the structure rots.
8. State transitions are file writes. Progress, decisions, and blockers are recorded by editing the phase doc and the index, in the same commit as the work they describe. If a state change happened but no file changed, it did not happen.

## Record-keeping inside initiatives

Long initiatives accumulate reasoning that must stay citable across phases:

- **Decisions and gotchas get stable IDs.** Decisions are numbered `D<n>`, gotchas `G<n>`, with ranges reserved per phase and never renumbered or reused, because cross-phase references are common and must stay stable. A phase doc cites `D30`, not "the decision in the other phase about X".
- **Each phase doc records its effect on one pinned metric** (a warning count, a failing-fixture count, anything a test can enforce), including the phases that made it worse before it got better. The regressions are what make the metric credible.
- **Discovered work becomes new checkboxes or new phases**, never silent expansion of an existing one. The phase list is stated as non-exhaustive.
- **A completed initiative gets a one-paragraph epilogue in the index**: what the finished feature does from the user's perspective, not a list of Landed rows. This is the only content the index ever holds.
- **Gaps are recorded with stated reasons, not excuses.** Anything the completed initiative deliberately does not do gets a gap table with its reason. A stated reason ends the question; an unexplained gap invites the question forever.
- **Cross-cutting caveats live once, at the initiative level**, cited by every phase they affect; three phase docs restating the same caveat guarantee three drift-apart copies.

## Version control conventions

Branches mirror plan structure, not commit types:

- One branch per phase (or per bug-sweep initiative). Named after what it implements, not after a type: `<type>/<issue>-<slug>-<phase>` is acceptable decoration, but the branch identity is the phase, because a phase lands whatever mix of features, fixes, and refactors the phase needs. A phase that only produces fixes gets a `fix/` branch by accident of content, not by rule.
- Commits use Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`). This is the findability mechanism, not branch names: branches die after merge, but commit messages are permanent and machine-parseable.
- The changelog and release notes are generated from conventional commit history (git-cliff or equivalent). Phase completion corresponds to a merge, so the changelog entry for a landed phase is derivable from the commit range, with no manual double bookkeeping.
- The chain of evidence: index row -> phase doc -> branch -> commit range -> changelog entry. Any link broken (untyped commits, unmarked landed phase, missing release note) weakens the walk test, because an agent can no longer reconcile file state with repository history.

Do not branch per commit type. Type-per-branch means a single phase's work is scattered across feat/fix/refactor branches that must be sequenced by hand, and long-running sweeps (one regression test + fix per item, on one branch) do not fit any single type. Type belongs to the commit; scope belongs to the branch.

## Experiments

An experiment is not a speculative feature and not a plan phase: it is a bounded test of a specific question: "can X work in this codebase at all?" Experiments get their own lane:

```
design/
  experiments/
    <id>-<slug>.md     <- one doc per experiment, archived by outcome
```

Rules:

1. An experiment doc opens with three things before any code exists: the question it answers, the criteria that decide the answer, and the time or scope box, a limit on effort (an afternoon, one spike per backend), never a budget in the money sense. An experiment without a decision criterion is a speculative feature wearing a lab coat.
2. Experiment code lives on a throwaway branch (`exp/<slug>`), never merged as-is. If the experiment succeeds, the code that ships is rewritten under a plan initiative as a first phase; the experiment branch is the evidence, not the product.
3. Every experiment ends in exactly one of three recorded outcomes:
   - `Adopted`: the question resolved yes; it spawns a plan initiative that links back to the experiment doc.
   - `Parked`: unresolved or premature; the idea moves to the deferred register with the reopening condition the experiment revealed.
   - `Rejected`: the question resolved no, with the evidence that showed it. This is a permanent no only if the evidence rules out the whole approach, not just one attempt.
4. The distinction from the deferred register: the register answers "is there a real issue worth building this?"; an experiment answers "would this even work?" A parked-with-condition entry can cite an experiment as the reason the condition is trusted.

## External trackers (GitHub issues and the like)

Issue trackers are an inbox, not state. They live outside the repository, get closed and buried by the platform, and no agent orienting from files alone can rely on them. The relationship is mirrored, not owned:

- An initiative that starts from a real report records its tracking issue number in the index heading. Files remain the source of truth; the issue is the front door and gets a link back to the plan folder.
- An idea filed as an issue never sits open as a wishlist item. It is recorded in the deferred register with its reopening condition and closed with a pointer to the entry. Open issue count stops lying about pending work.
- When a real issue later matches a register condition, the entry becomes an initiative; the new work supersedes the closed issue.
- Checklists, status, and phase progress never live in issue comments. If it matters for orientation, it is a file write.

## Walk test for planning workspaces

A fresh agent, given only the repo, must be able to answer in under a minute:

- What initiatives are active, and which phase is next?
- What is in progress, what is blocked, and why?
- For the next phase: what problem it solves and which checkboxes are still open?

If any answer requires conversation history or memory, the workspace fails.

## Per-feature openness: the deferred register

A rejection is only a no until a real problem reopens it. The workspace models that explicitly:

```
design/
  deferred.md     <- every considered-and-not-built idea, with its reopening condition
```

Rules for the deferred register:

1. One entry per idea: what it is, why it was not built, and the concrete condition under which it reopens (a real issue, a reproduction, a user report, never a hypothetical).
2. An entry with no reopening condition is a permanent no and says so. An entry with a condition is a parked idea, not a rejected one.
3. When a real issue arrives that matches a condition, the idea moves out of the register and into `plan/` as an initiative. The entry in the register is replaced by a line pointing at the plan folder.
4. Speculative proposals get one entry in the register, not repeated debate. Agents do not re-litigate entries; they check whether a new issue satisfies a stated condition.

This is what keeps the feature surface honest: nothing is built without a real issue, and nothing is ruled out forever that a real issue could reopen. The register is cheap to load (a scan of one-liners), expensive to ignore (it is the anti-speculation gate).

## Division of duties

| Artifact | Holds | Context role |
|---|---|---|
| `plan/plan.md` | Phase index with status | Always loaded: orientation |
| Phase doc | Problem, resolution, checkbox steps | Loaded only for the active phase |
| `design/deferred.md` | Parked ideas with reopening conditions | Loaded when evaluating a feature proposal |
| `design/*.md` | Subsystem reasoning | Loaded on demand |
| `CHANGELOG.md` / release notes | Shipped changes | Loaded only when asked about history |

Changelogs and release notes remain the source of truth for what changed and shipped. They are not the source of truth for what is being done next or why; that is the plan workspace's job. Keeping those roles separate is what keeps the agent's working context small: orientation reads the index, work reads one phase doc, history stays out of context until requested.
