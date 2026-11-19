# Contributing to Duack

Duack is a small audio library for MonoGame, KNI, and FlatRedBall2
games, desktop and web: crossfades, playlists, ducking, volume
management. MIT-licensed, free for commercial games.

It is in preview. What it is today is what it is; what it will be is
still being decided by what real games turn out to need. A real issue
says more here than any roadmap.

## Three routes, pick one

- A bug: open an issue with a minimal repro. For audio, the repro is
  partly what you heard: engine and version, desktop or web, file
  format, what you expected, what you heard, and the smallest
  AudioConfigBuilder chain or Play sequence that triggers it. Send it
  in whatever shape you have; narrowing it down is our half of the
  work.
- A feature idea: open an issue describing the problem you are hitting,
  not the API you imagine. One use case per request.
- Docs, samples, small fixes: open a PR directly.

It is all the GitHub flow you already use; nothing here is a new
process to learn.

## What happens next

- Every feature request is recorded in `design/deferred.md` with the
  concrete condition under which it reopens: a real issue, a repro, a
  second report. The issue then closes with a pointer to that entry,
  so the last thing you see is the door back in, not a wall.
- Reproduced bugs become fixes under `plan/` and ship with a
  regression test.
- The planning workspace under `plan/` and `design/` is public. Read it
  to see what is in flight; you never have to write to it. Its
  irreducible complexity stays on our side of the desk.

Small PRs land faster: one change per PR, and effort accelerates as
the finish line gets close.

## Pull requests

1. An agreed issue first, except typos and tiny doc fixes.
2. One change per PR.
3. A test that fails before your bug fix and passes after it.
4. Run it on at least one target (desktop or web) and say which.
5. Touches the public API? Say so in the description. Preview churn
   is expected and gets announced in release notes.

## AI-assisted work

Use whatever tools help you. You own all of it: understand the code,
test it, and be able to explain it in review. Say in the PR
description if a substantial part was AI-generated. PRs nobody is
responsible for close without review.

## Commits

Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`, `test:`,
`chore:`); breaking changes use the `!` suffix or a `BREAKING CHANGE:`
footer. If your tooling does not set them, the maintainer sets the
type in the squash-merge title. The changelog is generated from commits;
that is the only reason this rule exists.

## Scope, provisionally

The decline test is simple: is this a real audio problem a real game is
hitting? In preview, even that boundary is provisional. The value
proposition of Duack shifts as consumers use the library, so what
matters is evidence, not a fixed promise: refused ideas still get
their reopening condition in `design/deferred.md`, and ideas that turn
out to be needed become the roadmap.

## License

MIT. By contributing you license your work under the same terms.
