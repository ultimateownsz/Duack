---
name: player-settings
description: Player audio settings in Duack — PlayerAudioSettings, PlayerSettings layering, persistence stores (file/web/in-memory), PlayerSettingsStoreSelector, VolumeCurve perceptual sliders, save/load/clear overrides. Use when adding a settings overlay, volume sliders, persisting player preferences, or choosing a storage backend.
---

# Duack — Player Settings

Authored code config stays source of truth; the store only carries the handful of
values the settings overlay exposes. Re-exporting/hot-reloading authored config never
discards a player's volume choice and never writes back into shipped content.

## PlayerAudioSettings

```csharp
public class PlayerAudioSettings
{
    int SchemaVersion = 1;
    float MasterVolume = 1f;       // master bus [0,1]
    float MusicMasterVolume = 1f;   // music bus [0,1]
    float SfxMasterVolume = 1f;     // SFX bus [0,1]
    float CrossfadeSeconds = 1.2f;  // [0.1,10]
    static FromConfig(AudioConfig);
    void Sanitize();
}
```

Serialization: snake_case, source-generated context.

## PlayerSettings (static facade)

```csharp
PlayerSettings.Store = ...;                    // set once at startup
AudioConfig effective = PlayerSettings.ApplyOverrides(authored);          // layered
AudioConfig effective = PlayerSettings.ApplyOverrides(authored, store);   // explicit store
bool ok = PlayerSettings.SaveOverrides(config);                          // persists tunables
bool ok = PlayerSettings.SaveOverrides(config, store);
PlayerSettings.ClearOverrides();               // drop saved prefs
```

`PlayerSettings.ApplyOverrides` carries over authored sections the player can't edit (contexts, duck sources,
tracks, extension data). A store that fails to load yields authored values. `PlayerSettings` lives
in the core (`Duack`); the JSON serialization of `PlayerAudioSettings` is also in the core
(SnakeCaseLower, source-generated context), as is the `AudioConfig` document serialization.
- **Diagnostics are on by default**: `PlayerSettings.Diagnostics` is the console sink, so a
  swallowed storage failure (a failed localStorage read/save, corrupt JSON) is reported somewhere
  findable instead of looking like "nothing saved yet". Tests that need silence assign
  `NullPlayerSettingsDiagnostics` explicitly; a game may replace the sink per platform.

## Stores

- `IPlayerSettingsStore`: `TryLoad(out settings)`, `Save(settings)`, `Clear()`.
- `FilePlayerSettingsStore` — per-user application data (desktop default).
- `WebPlayerSettingsStore(storage)` — localStorage (web).
- `InMemoryPlayerSettingsStore` — tests / ephemeral.

## Store selection (pure function, testable)

```csharp
PlayerSettings.Store = PlayerSettingsStoreSelector.Select(
    isBrowser: OperatingSystem.IsBrowser(),
    fileStore: new FilePlayerSettingsStore(),
    browserStore: new WebPlayerSettingsStore(storage));
// or Select(fileStore, browserStore) uses OperatingSystem.IsBrowser()
```

## Gain / VolumeCurve (perceptual sliders & fader law)

```csharp
float gain     = Gain.ToGain(slider);               // slider [0,1] -> gain [0,1], quadratic fader law
float amplitude = VolumeCurve.ToAmplitude(slider);   // slider [0,1] -> amplitude [0,1]
float slider   = Gain.ToSlider(gain);               // exact inverse
```

Human loudness is closer to logarithmic; quadratic makes midpoint noticeably quieter
(0.5 slider -> 0.25 amplitude/gain). Use for any volume or mixer gain control. NaN -> 0.

## Settings overlay flow

```csharp
// Load (at boot, BEFORE the managers are constructed - they snapshot the bus
// table at construction, so a later layering needs an explicit push):
var effective = PlayerSettings.ApplyOverrides(authoredConfig);
manager = new MusicManager(backend, controller, effective.Music,
    masterVolume: effective.Buses[BusNames.Master].Volume,
    sfxBusVolume: effective.Buses[BusNames.Sfx].Volume);

// Save (the overlay's save button; returns the store's verdict)
PlayerSettings.SaveOverrides(effectiveConfig);   // fields: MasterVolume,
                                                 // MusicMasterVolume, SfxMasterVolume,
                                                 // CrossfadeSeconds (see PlayerAudioSettings)
```

## Web storage

`WebPlayerSettingsStore` needs an `IKeyValueStorage` (localStorage adapter) and takes an optional
storage `key` (default `duack:playerAudioSettings`) - a game keeping its historical key passes it
explicitly. Browser autoplay and storage availability are environment concerns - document, don't
solve silently.

## Tips

- Save returns the store's verdict; surface "Saved!" only on true.
- Failed writes report through `PlayerSettings.Diagnostics` (console by default), and the report
  API survives Release/browser builds - a `Debug.WriteLine`-based diagnostic would be stripped
  from exactly the build the browser runs.
- `ClearOverrides` makes next `ApplyOverrides` use authored values.

## Studio & sound engineering concepts

- **Player settings**: In studio terminology, the player settings act as the **monitor chain / monitor level**. The mix / program is authored by the game, while what the end listener actually hears passes through the user's monitor chain (the volume knob / monitor level).
- **VolumeCurve**: Represents the **fader law** (or audio-taper / logarithmic vs linear response curve) mapping perceptual user slider positions to physical signal amplitudes.

See also: `audio-core`, `config`.
