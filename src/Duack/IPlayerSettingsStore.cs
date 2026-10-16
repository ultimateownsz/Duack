namespace Duack;

/// <summary>
/// Durable home for the player's audio preferences. Implementations must report failure instead of
/// throwing: the settings overlay turns the result of a save into the "Saved!" flash, so a swallowed
/// exception would put the old false-success lie back.
/// </summary>
public interface IPlayerSettingsStore
{
    /// <summary>
    /// Reads the saved preferences. Returns false when nothing is stored yet or the stored data is
    /// unreadable, in which case the caller keeps the authored defaults.
    /// </summary>
    bool TryLoad(out PlayerAudioSettings? settings);

    /// <summary>Writes the preferences. Returns false when the write could not be completed.</summary>
    bool Save(PlayerAudioSettings settings);

    /// <summary>Removes the stored preferences, if any. Never throws.</summary>
    void Clear();
}
