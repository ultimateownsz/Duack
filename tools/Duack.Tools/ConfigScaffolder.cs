using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Duack.Tools;

/// <summary>
/// Builds a starting <see cref="AudioConfig"/> from a content directory's WAV files (the pure part
/// of <c>duack init</c>). The result carries the default music/sfx sections, the sfx/dialogue duck
/// sources, and a placeholder <c>tracks</c> entry for every discovered WAV so the loudness/compress
/// values have somewhere to be filled in.
/// </summary>
public static class ConfigScaffolder
{
    /// <summary>
    /// Builds the scaffold from WAV file names (with or without a directory; the extension is
    /// stripped and the result is sorted so the JSON is deterministic). The <c>tracks</c> section is
    /// omitted entirely when no WAVs are found.
    /// </summary>
    public static AudioConfig Build(IEnumerable<string> wavFileNames)
    {
        var config = new AudioConfigBuilder()
            .WithDuckSource("sfx", new DuckSourceConfig())
            .WithDuckSource("dialogue", new DuckSourceConfig())
            .Build();

        var keys = wavFileNames
            .Select(Path.GetFileNameWithoutExtension)
            .Where(key => !string.IsNullOrEmpty(key))
            .Distinct()
            .OrderBy(key => key, System.StringComparer.Ordinal);

        var tracks = new Dictionary<string, TrackConfig>();
        foreach (var key in keys)
            tracks[key] = new TrackConfig();

        if (tracks.Count > 0)
            config.Tracks = tracks;

        return config;
    }
}
