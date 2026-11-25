# Duack.Sample.Concurrency — SFX voice limiting stress example

Standalone desktop example for Slice A (voice concurrency limiting). Unlike the
storyline samples, it owns no storyline: it exists to make the cap observable and
audible in isolation.

## Run it

    dotnet run --project samples/Duack.Sample.Concurrency

The SFX assets are reused from `Duack.Sample.Desktop` by the project file (no
content pipeline in this project); the run fails fast with a clear message when
none are discovered.

## Try it

- **ONE** — fires a single cue through the shuffle bag (no immediate repeats).
- **BURST** — fires 50 overlapping cues; the manager is capped at 8 voices with
  oldest-voice stealing, so the on-screen counter must never exceed 08 and the
  oldest sounding voices are cut off as new ones start.
- **STOP** — silences everything and clears the reserved slots (the counter and
  slot bars reset).
- `Space`/`T` mirror ONE/BURST; `Escape` exits.

## What proves the behavior

Numeric: the two-digit counter and the eight slot bars are `SfxManager`
ActiveVoiceCount, capped by `GlobalMaxVoices = 8`, with lifetimes aged by
`SfxManager.Update(unscaledDt)` — 50 plays against a cap of 8 can hold at most
eight reserved slots. Audible: whether the stealing sounds right is a human
verdict by design — no agent checks audio; that judgment belongs to the
maintainer listening check (issue #38).
