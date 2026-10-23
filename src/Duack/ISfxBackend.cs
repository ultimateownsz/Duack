namespace Duack;

/// <summary>
/// Platform playback layer for one-shot sound effects. The core's SfxManager
/// validates cues and triggers music ducking; a backend only plays what the
/// core hands it. Implementations: MonoGame (desktop), WebAudio (browser).
/// </summary>
public interface ISfxBackend
{
    /// <summary>
    /// Plays a cue. Pitch is in XNA units (-1..1, 0 = natural); volume is 0..1.
    /// Returns false when nothing played (unknown cue, not loaded, engine refusal),
    /// which also tells SfxManager not to duck the music.
    /// </summary>
    bool Play(string cueName, float pitch, float volume);

    /// <summary>Hard-stops all currently playing one-shots (scene changes).</summary>
    void Stop();
}
