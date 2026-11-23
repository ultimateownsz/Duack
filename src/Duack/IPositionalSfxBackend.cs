namespace Duack;

/// <summary>
/// Backend support for spatialized one-shots: play a cue with a pan in [-1, 1] already
/// composed into its volume, and re-mix the still-playing voices of a cue (a moving source).
/// Backends without positional playback simply do not implement this; the SFX manager then
/// plays such cues unpositioned.
/// </summary>
public interface IPositionalSfxBackend
{
    /// <summary>Plays a cue with a pan in [-1, 1]; returns false when nothing played.</summary>
    bool Play(string cueName, float pitch, float volume, float pan);

    /// <summary>
    /// Re-mixes every still-playing voice of a cue to a new pan (a moving source). Returns
    /// false when the backend has no such voice.
    /// </summary>
    bool SetCueMix(string cueName, float pan, float volume);
}
