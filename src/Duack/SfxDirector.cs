using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// Plays one-shot cues by name against a discovered <see cref="AudioLibrary"/> and keeps the music
/// mix out of the way while they speak: every successful play pulses the music director's duck
/// source (default <c>sfx</c>). Unknown cues and backend refusals report failure and never duck.
/// <see cref="PlayNext"/> draws from a shuffle bag over the discovered sfx, so a demo or ambient
/// layer never plays the same cue back-to-back.
///
/// <para>
/// Pitch is passed through untouched in engine units (MonoGame: -1..1, 0 = natural), which is what
/// makes "the same sound, differently pitched" a one-line call (see the sample's marble bag of
/// hit pitches).
/// </para>
/// </summary>
public sealed class SfxDirector
{
    private readonly AudioLibrary _library;
    private readonly ISfxBackend _backend;
    private readonly MusicDirector _music;
    private readonly string _duckSource;
    private readonly MarbleBag<string> _bag;

    /// <param name="library">Discovered cues; only names in <see cref="AudioLibrary.Sfx"/> are playable.</param>
    /// <param name="backend">Engine playback adapter.</param>
    /// <param name="music">Director whose duck source is pulsed while a cue speaks.</param>
    /// <param name="duckSource">Duck-source name in <see cref="MusicConfig.DuckSources"/> (default <c>sfx</c>).</param>
    /// <param name="random">Randomness source for <see cref="PlayNext"/>; defaults to <see cref="Random.Shared"/>.</param>
    public SfxDirector(
        AudioLibrary library,
        ISfxBackend backend,
        MusicDirector music,
        string duckSource = "sfx",
        Random? random = null)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _music = music ?? throw new ArgumentNullException(nameof(music));
        _duckSource = duckSource ?? throw new ArgumentNullException(nameof(duckSource));
        _bag = new MarbleBag<string>(library.Sfx, random, avoidImmediateRepeat: true);
    }

    /// <summary>The discovered one-shot cue names (see <see cref="AudioLibrary.Sfx"/>).</summary>
    public IReadOnlyList<string> Names => _library.Sfx;

    /// <summary>
    /// Plays <paramref name="cueName"/> and ducks the music for the configured hold while it
    /// speaks. Returns false — without touching the backend and without ducking — when the cue is
    /// not in the library or the backend refuses to play it.
    /// </summary>
    public bool Play(string cueName, float pitch = 0f, float volume = 1f)
    {
        if (!_library.IsSfx(cueName)) return false;
        if (!_backend.Play(cueName, pitch, volume)) return false;

        _music.Pulse(_duckSource);
        return true;
    }

    /// <summary>
    /// Draws the next cue from the shuffle bag (no immediate repeats) and plays it. Returns false
    /// when the library discovered no sfx at all.
    /// </summary>
    public bool PlayNext(float pitch = 0f, float volume = 1f) =>
        _bag.TryDraw(out var cue) && Play(cue, pitch, volume);
}
