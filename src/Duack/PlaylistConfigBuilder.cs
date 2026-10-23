using System;
using System.Collections.Generic;

namespace Duack;

/// <summary>
/// Fluent helper for assembling a <see cref="PlaylistConfig"/> in code, mirroring
/// <see cref="AudioConfigBuilder"/>: a game can define its playlists in C# and either hand them to
/// <see cref="MusicPlaylistController"/> directly or serialize them to JSON with the provisional
/// JSON layer (<c>PlaylistJson</c> in the <c>Duack.Config</c> package) for the tooling path.
/// </summary>
public sealed class PlaylistConfigBuilder
{
    private readonly PlaylistConfig _config = new();

    /// <summary>
    /// Adds (or replaces) a named playlist. <paramref name="configure"/> fills in the tracks and the
    /// repeat/shuffle modes; omit it to add an empty playlist.
    /// </summary>
    public PlaylistConfigBuilder WithPlaylist(string name, Action<PlaylistDefinitionBuilder>? configure = null)
    {
        var definition = new PlaylistDefinitionBuilder();
        configure?.Invoke(definition);
        _config.Playlists[name] = definition.Build();
        return this;
    }

    /// <summary>Returns the assembled playlist config.</summary>
    public PlaylistConfig Build() => _config;
}

/// <summary>Fluent builder for a single <see cref="PlaylistDefinition"/> (see <see cref="PlaylistConfigBuilder.WithPlaylist"/>).</summary>
public sealed class PlaylistDefinitionBuilder
{
    private readonly PlaylistDefinition _definition = new();

    /// <summary>Appends a track (its cue/file name and the music-context tag it plays under).</summary>
    public PlaylistDefinitionBuilder Track(string name, string context, float? fadeInSeconds = null, float? fadeOutSeconds = null)
    {
        _definition.Tracks.Add(new PlaylistTrack { Name = name, Context = context, FadeInSeconds = fadeInSeconds, FadeOutSeconds = fadeOutSeconds });
        return this;
    }

    /// <summary>Sets what happens when a track finishes.</summary>
    public PlaylistDefinitionBuilder Repeat(PlaylistRepeatMode mode)
    {
        _definition.Repeat = mode;
        return this;
    }

    /// <summary>Sets how the track order is chosen.</summary>
    public PlaylistDefinitionBuilder Shuffle(PlaylistShuffleMode mode)
    {
        _definition.Shuffle = mode;
        return this;
    }

    /// <summary>Returns the assembled definition.</summary>
    public PlaylistDefinition Build() => _definition;
}
