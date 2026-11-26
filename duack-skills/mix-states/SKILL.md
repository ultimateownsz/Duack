---
name: mix-states
description: Duack named mix states — authoring per-bus target vectors with AudioConfigBuilder.WithMixState, applying instantly with MixStateManager.ApplyState or easing over a duration with TransitionTo (one-pole smoothing with crossfade-style telemetry — IsTransitioning, elapsed/duration, from/to), and how states compose with buses, ducking, and player settings. Use when a game needs named mix identities (peace, combat, low-health) over the same buses.
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
