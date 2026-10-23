using System.Collections.Generic;

namespace Duack;

/// <summary>How a playlist behaves when the current track finishes.</summary>
public enum PlaylistRepeatMode
{
    /// <summary>Stop after the last track of the playlist (or of a shuffle cycle).</summary>
    Off,

    /// <summary>Loop the whole playlist forever.</summary>
    Playlist,

    /// <summary>Loop the current track forever until it is skipped.</summary>
    Track,
}

/// <summary>How the order of the tracks within a playlist is chosen.</summary>
public enum PlaylistShuffleMode
{
    /// <summary>Play the tracks in their authored order.</summary>
    Off,

    /// <summary>
    /// Draw the tracks from a <see cref="MarbleBag{T}"/>: every track plays once per cycle in a
    /// random order, then the cycle refills. Never pure random, so a run cannot repeat one track
    /// while ignoring the others.
    /// </summary>
    Bag,
}

/// <summary>
/// One entry in a <see cref="PlaylistDefinition"/>. <see cref="Name"/> identifies the track (its
/// file/cue name); <see cref="Context"/> is the music-context tag whose volume scale applies while
/// this track plays.
/// </summary>
public class PlaylistTrack
{
    /// <summary>The track's name (its cue/file key).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The music-context tag this track plays under (a key in <see cref="MusicConfig.Contexts"/>).</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>Optional fade-in duration in seconds for this track when it starts.</summary>
    public float? FadeInSeconds { get; set; }

    /// <summary>Optional fade-out duration in seconds for this track when it stops/leaves.</summary>
    public float? FadeOutSeconds { get; set; }
}

/// <summary>A named, ordered list of tracks plus its repeat and shuffle behavior.</summary>
public class PlaylistDefinition
{
    /// <summary>The tracks, in authored order (the shuffle mode may change playback order).</summary>
    public List<PlaylistTrack> Tracks { get; set; } = new();

    /// <summary>What happens when a track finishes.</summary>
    public PlaylistRepeatMode Repeat { get; set; } = PlaylistRepeatMode.Off;

    /// <summary>How the track order is chosen.</summary>
    public PlaylistShuffleMode Shuffle { get; set; } = PlaylistShuffleMode.Off;
}

/// <summary>
/// The named playlists a game can switch between, keyed by playlist name. Backend-agnostic data;
/// <see cref="MusicPlaylistController"/> consumes it to make sequencing decisions.
/// </summary>
public class PlaylistConfig
{
    /// <summary>Named playlists (keys are the names passed to <see cref="MusicPlaylistController.Play"/>).</summary>
    public Dictionary<string, PlaylistDefinition> Playlists { get; set; } = new();

    /// <summary>Looks up a playlist by exact name.</summary>
    public bool TryGet(string name, out PlaylistDefinition definition) =>
        Playlists.TryGetValue(name, out definition!);
}
