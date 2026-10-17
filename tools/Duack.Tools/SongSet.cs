using System;
using System.Collections.Generic;
using System.IO;

namespace Duack.Tools;

/// <summary>
/// The set of names that count as "songs" for conversion, taken from the union of every playlist's
/// track names. Names are compared case- and extension-insensitively, so a playlist can declare
/// <c>"theme_a"</c> while the file on disk is <c>theme_a.wav</c> (or <c>Theme_A.ogg</c>).
/// </summary>
public sealed class SongSet
{
    private readonly HashSet<string> _names;

    private SongSet(HashSet<string> names) => _names = names;

    /// <summary>Builds a song set from the union of every track name in every playlist.</summary>
    public static SongSet FromPlaylists(PlaylistConfig playlists)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in playlists.Playlists.Values)
        {
            if (definition is null)
                continue;

            foreach (var track in definition.Tracks)
            {
                if (track is null)
                    continue;

                var normalized = Normalize(track.Name);
                if (normalized.Length > 0)
                    names.Add(normalized);
            }
        }

        return new SongSet(names);
    }

    /// <summary>True when <paramref name="fileName"/> (with or without extension) names a song.</summary>
    public bool Contains(string fileName) => _names.Contains(Normalize(fileName));

    private static string Normalize(string name)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(name);
        return string.IsNullOrEmpty(withoutExtension) ? name.Trim() : withoutExtension.Trim();
    }
}
