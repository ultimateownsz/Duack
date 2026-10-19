---
name: player-settings
description: Player audio settings in Duack — PlayerAudioSettings, PlayerSettings layering, persistence stores (file/web/in-memory), PlayerSettingsStoreSelector, VolumeCurve perceptual sliders, save/load/clear overrides. Use when adding a settings overlay, volume sliders, persisting player preferences, or choosing a storage backend.
---

# Duack — Player Settings

Authored `audio-config.json` stays source of truth; the store only carries the handful of
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

`ApplyOverrides` carries over authored sections the player can't edit (contexts, duck sources,
tracks, extension data). A store that fails to load yields authored values.

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

## VolumeCurve (perceptual sliders)

```csharp
float amplitude = VolumeCurve.ToAmplitude(slider);   // slider [0,1] -> amplitude [0,1], quadratic
float slider   = VolumeCurve.ToSlider(amplitude);    // exact inverse
```

Human loudness is closer to logarithmic; quadratic makes midpoint noticeably quieter
(0.5 slider -> 0.25 amplitude). Use for any volume control. NaN -> 0.

## Settings overlay flow

```csharp
// Load
var authored = AudioConfig.Load();
var effective = PlayerSettings.ApplyOverrides(authored);
director = new MusicDirector(backend, controller, effective.Music);

// Save
settings.MusicVolume = VolumeCurve.ToAmplitude(sliderMusic);
settings.SfxVolume   = VolumeCurve.ToAmplitude(sliderSfx);
PlayerSettings.SaveOverrides(effectiveConfig);
```

## Web storage

`WebPlayerSettingsStore` needs an `IKeyValueStorage` (localStorage adapter). Browser autoplay
and storage availability are environment concerns — document, don't solve silently.

## Tips

- Save returns the store's verdict; surface "Saved!" only on true.
- Failed writes log via `Console.WriteLine` (not Debug) so they survive Release/browser builds.
- `ClearOverrides` makes next `ApplyOverrides` use authored values.

See also: `audio-engine`, `audio-engine`.
