---
name: mix-states
description: Duack named mix states — authoring per-bus target vectors with AudioConfigBuilder.WithMixState, applying instantly with MixStateManager.ApplyState or easing over a duration with TransitionTo (one-pole smoothing with crossfade-style telemetry — IsTransitioning, elapsed/duration, from/to), and how states compose with buses, ducking, and player settings. Use when a game needs named mix identities (peace, combat, low-health) over the same buses, including authoring them with a game-side enum adapter for typed state names.
---

# Duack — Named Mix States

## What a mix state is

A named vector of authored per-bus volume targets in the same flat bus table
buses live in — a "combat" state, a "low-health" state — authored in code, never
captured from runtime. Buses the state does not name keep their current volume,
so a state changes only what it declares.

## Setup (authoring)

```csharp
var config = new AudioConfigBuilder()
    .WithMixState("calm",   s => s.Bus("music", 1f).Bus("ambience", 1f))
    .WithMixState("combat", s => s.Bus("music", 0.4f).Bus("ambience", 0.6f))
    .Build();   // Build clamps every target to [0,1]
```

## Applying and transitioning

```csharp
var states = new MixStateManager(
    config.MixStates,
    musicManager.Buses,   // MusicManager.Buses — the music-side mixer
    sfxManager.Buses);    // SfxManager.Buses    — the SFX-side mixer

states.ApplyState("combat");          // instant: targets written this frame
states.TransitionTo("calm", 2f);       // eases over 2 seconds (0 or less = instant)

states.Update(deltaSeconds);           // once per frame, same delta the managers get —
                                      // unscaled, like fades and ducks

states.IsTransitioning;               // telemetry, crossfade pattern:
states.ElapsedSeconds;                //   0 .. DurationSeconds while a transition runs
states.DurationSeconds;
states.FromState; states.ToState;      // null outside a transition
states.CurrentState;                   // the settled state's name
```

A state writes **both** runtime mixers (music and SFX own one bus table each,
seeded from the same config), so a transition moves the whole mix.

## How it composes

Targets are **bus volume inputs** to the same single resolver: duck contributions
stay live on top of them (never embedded in bus state), per-bus mute/pause and the
master mute still fold at resolution, and the player's settings sliders still layer
over the state via their own bus writes. Unknown state names report loudly —
`ApplyState`/`TransitionTo` return `false` and raise `states.UnknownState` with the
name, and the mix is left untouched.

## Authoring states with a game enum

State names live in exactly two places you wrote yourself — the authoring call
and the runtime `ApplyState`/`TransitionTo` call — with no filesystem, library
fixed name, or third source of truth to drift from. That makes mix states the
best surface for a typed key, and the core takes strings, so the type lives in
your game as a small adapter:

```csharp
public enum GameMix { Peace, Combat, LowHealth }

public static class GameMixAudio
{
    public static string Name(this GameMix mix) => mix.ToString().ToLowerInvariant();
    public static bool Apply(this MixStateManager m, GameMix mix) => m.ApplyState(mix.Name());
    public static bool TransitionTo(this MixStateManager m, GameMix mix, float seconds)
        => m.TransitionTo(mix.Name(), seconds);
}

// authoring
new AudioConfigBuilder()
    .WithMixState(GameMix.Combat.Name(), s => s.Bus("music", 0.4f))
    .WithMixState(GameMix.Peace.Name(),  s => s.Bus("music", 1f));

// runtime — your enum, no raw strings at the call site:
states.TransitionTo(GameMix.Combat, 1.5f);
```

Why an enum over a constants class (`static class GameMix { public const string
Peace = "peace"; }`), which the other name surfaces use:

- **The closed set is the point.** `switch` over the enum warns when you add
  `LowHealth` and forget to author it; iterating `Enum.GetValues<GameMix>()`
  against `config.MixStates` at startup proves every state has authored targets.
  Constants have no set to enumerate and no missing-member warning.
- **No cross-assignment.** Any string constant can be passed where a state name
  is expected (`ApplyState(PlaylistNames.Battle)` compiles); an enum value from
  a different domain does not convert, so a `GameMix` cannot leak into a
  playlist or bus parameter.
- **A misspelling is a compile error, not a runtime report.** `UnknownState` still
  covers the seams (a state authored but never switched to, a removed config
  entry), but the typo class of miss never reaches runtime at all.

The core stays string-keyed on purpose: `AudioConfig.MixStates` is a
`Dictionary<string, MixStateConfig>` that round-trips JSON and is cloned by the
player-settings overlay, and every other name surface (buses, contexts,
playlists, duck sources) is string-keyed the same way. The enum is your game's
vocabulary over that table, not a library type.
