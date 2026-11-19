# Contributing

## Commit messages

Commits use Conventional Commits (`feat:`, `fix:`, `refactor:`, `docs:`,
`test:`, `chore:`). Breaking changes use the `!` suffix (`feat!:`,
`fix!:`) or a `BREAKING CHANGE:` footer, and release notes render them
into a separate Breaking changes section at the top.

## Deprecation policy (preview)

No `[Obsolete]` markings while the project is in preview. Preview state is
a blanket deprecation of the whole API (the README says so); members are
either current or deleted, and release notes carry the old-to-new mapping
instead of the compiler. Never leave Obsolete members to clean up later.

Post-1.0 the contract inverts: breaking changes only in major versions,
`[Obsolete]` with a stated removal version becomes the standard
instrument, and intended removals get at least one minor version of
warning. Decision parked in `design/deferred.md`; reopens when the first
1.0 release is scheduled.
