# Contributing to Duack

Thanks for your interest. Duack is a small, human-maintained project in
preview, so please be patient, and feedback on this process itself is
welcome in any issue. You do not need to contribute to use Duack; it is
free, including for commercial games.

## The short version

- Bugs: open an issue with a minimal repro. For an audio library the repro
  is partly what you heard, so describe it.
- Feature ideas: open an issue describing the problem you are hitting,
  not the API you imagine.
- Anything bigger than a small fix: open an issue first and wait for
  agreement on the approach before writing the code.
- Docs, samples, small fixes: open a PR directly.

## What Duack is, and stays

Duack does crossfades, playlists, ducking, and volume management for
MonoGame, KNI, and FlatRedBall2 games, desktop and web. It is
deliberately small: audio work that every game eventually needs, done
well, with nothing else.

The project is strict about that scope. PRs that drift away from it
will be declined, for example ones that:

- add features with no real problem behind them ("wouldn't it be cool
  if" is not a need)
- add engine- or editor-scale systems Duack has no stake in
- add native or platform dependencies without prior discussion
- change the public API without an agreed issue

That is not ingratitude: a small library that grew everything is the
thing Duack exists to replace. And it is never a no forever. Declined
ideas are recorded in `design/deferred.md` with the concrete condition
under which they reopen, and the closed issue points at that entry.
Reopening happens by demonstrating the problem, not by re-filing.

## Reporting bugs

Real, reproduced problems are the fuel for everything built here. A bug
report is more valuable than a patch, and both are welcome.

Open an issue with:

- Engine and version (MonoGame, KNI, FlatRedBall2) and platform (desktop
  or web/WASM).
- Audio specifics: file format, sample rate, what you expected to hear
  and what you heard instead. For an audio library, "what did it sound
  like" is the reproduction.
- The smallest code that triggers it: an `AudioConfigBuilder` chain or a
  short `music.Play` / `sfx.Play` sequence beats a full game.

A reproduced bug becomes a fix under `plan/` and ships with a regression
test. The project cannot fix what it cannot hear, which is why the sound
description matters more than the stack trace.

## Requesting features

- Describe the problem you are hitting, not the API you imagine. The
  problem statement survives design; the imagined API rarely does.
- One use case per request. A feature with three use cases is three
  decisions.

Every request is recorded in `design/deferred.md` with the condition
under which it reopens: a real issue, a reproduction, a second report.
The issue is then closed with a pointer to that entry. A closed feature
request is a parked idea with a written trigger, not a rejection.

## Pull requests

1. Make sure an issue exists and the approach is agreed (typos and tiny
   doc fixes excepted).
2. Keep the PR focused on one change.
3. Include the failing-case test that a bug fix makes pass.
4. Test on at least one target (desktop or web) and say which.
5. If the PR touches the public surface, say so in the description:
   preview API churn is expected and gets announced in the release
   notes.

The workspace this repository uses to plan work is public in `plan/`
(active initiatives) and `design/` (parked ideas, experiments, design
records). Nothing there requires special access; reading it is the
fastest way to see what is in flight.

## AI-assisted contributions

Duack's maintainer uses AI tooling the way a compiler is used: it speeds
up work and decides nothing. The same applies to contributions. You may
use agentic tools, but you are responsible for every line you submit:
you must understand it, have tested it, and be able to explain it in
review. Say in the PR description if a substantial part was
AI-generated. Large, untested, or unexplained AI-generated PRs will be
closed without review: not because the tool was used, but because
nobody, human or machine, has taken responsibility for it yet.

If you are contributing through an agent, it should read `AGENTS.md` at
the repository root and work like any other contributor.

## Commit messages

Commits use Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`,
`test:`, `chore:`). Breaking changes use the `!` suffix (`feat!:`,
`fix!:`) or a `BREAKING CHANGE:` footer, and release notes render them
into a separate Breaking changes section at the top. Branches follow one
branch per phase, named after what the phase implements; the commit type,
not the branch, carries the classification.

## Deprecation policy (preview)

No `[Obsolete]` markings while the project is in preview. Preview state
is a blanket deprecation of the whole API (the README says so); members
are either current or deleted, and release notes carry the old-to-new
mapping instead of the compiler. Never leave Obsolete members to clean up
later.

Post-1.0 the contract inverts: breaking changes only in major versions,
`[Obsolete]` with a stated removal version becomes the standard
instrument, and intended removals get at least one minor version of
warning. That decision is parked in `design/deferred.md` and reopens when
the first 1.0 release is scheduled.

## Licensing

Duack is MIT-licensed. By submitting a contribution you license it under
the same MIT terms, and you confirm you are entitled to submit it.
