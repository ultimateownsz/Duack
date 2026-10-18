using System;
using System.Collections.Generic;
using System.IO;

namespace Duack;

/// <summary>
/// The two-directory audio convention: an audio root holds one directory of small one-shot sounds
/// (<c>SFX/</c>, wav preferred) and one of loops and music tracks (<c>Tracks/</c>, ogg preferred).
/// <see cref="FromFiles"/> classifies relative paths into cue names and <see cref="ScanDirectory"/>
/// reads the layout straight off disk — nothing is registered by hand, dropping a file in is enough.
/// The legacy flat layout (<c>{name}.ogg</c> directly in the audio root) stays supported as a final
/// candidate fallback, so existing content keeps loading unchanged.
///
/// <para>
/// All comparisons are case-SENSITIVE (Ordinal) on purpose: case-sensitive web hosts 404 on any
/// mismatch, so a lower-cased directory or an upper-cased extension must never classify as a cue.
/// </para>
/// </summary>
public sealed class AudioLibrary
{
    /// <summary>The default small one-shot directory, relative to the audio root.</summary>
    public const string SfxDirectoryName = "SFX";

    /// <summary>The default loops/tracks directory, relative to the audio root.</summary>
    public const string TracksDirectoryName = "Tracks";

    /// <summary>Loadable types inside <see cref="SfxDirectoryName"/>, in preference order (wav first).</summary>
    public static readonly IReadOnlyList<string> SfxExtensions = new[] { ".wav", ".ogg" };

    /// <summary>Loadable types inside <see cref="TracksDirectoryName"/>, in preference order (ogg first).</summary>
    public static readonly IReadOnlyList<string> TrackExtensions = new[] { ".ogg", ".wav" };

    /// <summary>Legacy flat layout probes (ogg first), always tried after the convention directories.</summary>
    private static readonly IReadOnlyList<string> FlatExtensions = new[] { ".ogg", ".wav" };

    private readonly HashSet<string> _sfxSet;
    private readonly HashSet<string> _tracksSet;

    private AudioLibrary(IReadOnlyList<string> sfx, IReadOnlyList<string> tracks)
    {
        Sfx = sfx;
        Tracks = tracks;
        _sfxSet = new HashSet<string>(sfx, StringComparer.Ordinal);
        _tracksSet = new HashSet<string>(tracks, StringComparer.Ordinal);
    }

    /// <summary>Discovered one-shot cue names (sorted ordinal, deduped across extensions).</summary>
    public IReadOnlyList<string> Sfx { get; }

    /// <summary>Discovered track cue names (sorted ordinal, deduped across extensions).</summary>
    public IReadOnlyList<string> Tracks { get; }

    /// <summary>
    /// Classifies relative paths (<c>SFX/hit.wav</c>, <c>Tracks/overworld.ogg</c>, either separator)
    /// into the two cue sets. Only direct children of the convention directories count: root files,
    /// unknown directories, nested paths, unsupported types and dotfiles are ignored.
    /// </summary>
    public static AudioLibrary FromFiles(IEnumerable<string> relativePaths)
    {
        if (relativePaths is null) throw new ArgumentNullException(nameof(relativePaths));

        var sfx = new SortedSet<string>(StringComparer.Ordinal);
        var tracks = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var path in relativePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;

            var segments = path.Split('/', '\\');
            if (segments.Length != 2) continue;

            var fileName = segments[1];
            if (fileName.Length == 0 || fileName[0] == '.') continue; // dotfile or empty tail

            var cue = Path.GetFileNameWithoutExtension(fileName);
            if (cue.Length == 0) continue;

            var extension = Path.GetExtension(fileName);
            if (segments[0] == SfxDirectoryName && Contains(SfxExtensions, extension))
                sfx.Add(cue);
            else if (segments[0] == TracksDirectoryName && Contains(TrackExtensions, extension))
                tracks.Add(cue);
        }

        return new AudioLibrary(sfx.ToList(), tracks.ToList());
    }

    /// <summary>
    /// Scans <paramref name="audioRoot"/> for the convention directories and classifies every file
    /// in them. A missing subdirectory is an empty side; a missing root throws.
    /// </summary>
    public static AudioLibrary ScanDirectory(string audioRoot)
    {
        if (audioRoot is null) throw new ArgumentNullException(nameof(audioRoot));
        if (!Directory.Exists(audioRoot))
            throw new DirectoryNotFoundException($"Audio root not found: {audioRoot}");

        // Match on-disk names with exact casing: a lower-cased `sfx` must not satisfy the
        // convention even where the filesystem itself is case-insensitive.
        var onDisk = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var directory in Directory.EnumerateDirectories(audioRoot))
        {
            var name = Path.GetFileName(directory);
            if (!onDisk.ContainsKey(name)) onDisk[name] = directory;
        }

        var files = new List<string>();
        foreach (var conventionDirectory in new[] { SfxDirectoryName, TracksDirectoryName })
        {
            if (!onDisk.TryGetValue(conventionDirectory, out var full)) continue;
            foreach (var file in Directory.EnumerateFiles(full))
                files.Add(conventionDirectory + "/" + Path.GetFileName(file));
        }

        return FromFiles(files);
    }

    /// <summary>Whether <paramref name="cueName"/> was discovered in <see cref="SfxDirectoryName"/>.</summary>
    public bool IsSfx(string cueName) => cueName is not null && _sfxSet.Contains(cueName);

    /// <summary>Whether <paramref name="cueName"/> was discovered in <see cref="TracksDirectoryName"/>.</summary>
    public bool IsTrack(string cueName) => cueName is not null && _tracksSet.Contains(cueName);

    /// <summary>
    /// Ordered relative paths a loader should probe for <paramref name="cueName"/>: the owning
    /// convention directory first (or both, for an unknown cue), then the legacy flat layout last.
    /// Candidates are synthetic — no existence check is performed.
    /// </summary>
    public IReadOnlyList<string> CandidatePaths(string cueName)
    {
        if (cueName is null) throw new ArgumentNullException(nameof(cueName));
        if (string.IsNullOrWhiteSpace(cueName))
            throw new ArgumentException("Cue name must not be blank.", nameof(cueName));

        var results = new List<string>();
        if (_sfxSet.Contains(cueName))
        {
            AddProbes(results, SfxDirectoryName, cueName, SfxExtensions);
        }
        else if (_tracksSet.Contains(cueName))
        {
            AddProbes(results, TracksDirectoryName, cueName, TrackExtensions);
        }
        else
        {
            AddProbes(results, SfxDirectoryName, cueName, SfxExtensions);
            AddProbes(results, TracksDirectoryName, cueName, TrackExtensions);
        }

        AddProbes(results, string.Empty, cueName, FlatExtensions);
        return results;
    }

    /// <summary>
    /// One looping playlist wrapping every discovered track. Pair it with
    /// <see cref="BuildPlaylists"/> when both the per-scene single-track playlists and the
    /// shuffle-everything playlist are wanted.
    /// </summary>
    public PlaylistDefinition ToPlaylist(
        string context = "",
        PlaylistRepeatMode repeat = PlaylistRepeatMode.Playlist,
        PlaylistShuffleMode shuffle = PlaylistShuffleMode.Bag)
    {
        var definition = new PlaylistDefinition { Repeat = repeat, Shuffle = shuffle };
        foreach (var name in Tracks)
            definition.Tracks.Add(new PlaylistTrack { Name = name, Context = context ?? string.Empty });
        return definition;
    }

    /// <summary>
    /// Auto-builds the playlists a game switches between: one single-track looping playlist per
    /// discovered track (e.g. scene music addressed by track name) plus — unless disabled — one
    /// all-tracks bag-shuffled playlist (see <see cref="AudioLibraryPlaylistOptions"/>).
    /// Every playlist it returns has at least one track.
    /// </summary>
    public PlaylistConfig BuildPlaylists(AudioLibraryPlaylistOptions? options = null)
    {
        options ??= new AudioLibraryPlaylistOptions();
        var config = new PlaylistConfig();

        foreach (var name in Tracks)
        {
            config.Playlists[name] = new PlaylistDefinition
            {
                Tracks = { new PlaylistTrack { Name = name, Context = ContextOf(options, name) } },
                Repeat = options.SingleTrackRepeat,
                Shuffle = PlaylistShuffleMode.Off,
            };
        }

        var allName = options.AllTracksPlaylistName;
        if (!string.IsNullOrWhiteSpace(allName) && Tracks.Count > 0)
        {
            if (config.Playlists.ContainsKey(allName))
                throw new ArgumentException(
                    $"The all-tracks playlist name '{allName}' collides with a track of the same name.",
                    nameof(options));

            var all = new PlaylistDefinition
            {
                Repeat = options.AllTracksRepeat,
                Shuffle = options.AllTracksShuffle,
            };
            foreach (var name in Tracks)
                all.Tracks.Add(new PlaylistTrack { Name = name, Context = ContextOf(options, name) });
            config.Playlists[allName] = all;
        }

        return config;
    }

    private static string ContextOf(AudioLibraryPlaylistOptions options, string name) =>
        options.ContextForTrack?.Invoke(name) ?? string.Empty;

    /// <summary>Projects the discovered cues into a manifest (for <c>duack scan</c>-style output).</summary>
    public AudioLibraryManifest ToManifest() => new()
    {
        SchemaVersion = AudioLibraryManifest.CurrentSchemaVersion,
        Sfx = new List<string>(Sfx),
        Tracks = new List<string>(Tracks),
    };

    /// <summary>
    /// Rebuilds a library from a (possibly hand-edited) manifest: names are trimmed of blanks,
    /// deduped and sorted ordinal, mirroring <see cref="FromFiles"/> normalization.
    /// </summary>
    public static AudioLibrary FromManifest(AudioLibraryManifest manifest)
    {
        if (manifest is null) throw new System.ArgumentNullException(nameof(manifest));
        return new AudioLibrary(Normalize(manifest.Sfx), Normalize(manifest.Tracks));
    }

    private static IReadOnlyList<string> Normalize(List<string>? names)
    {
        var result = new SortedSet<string>(System.StringComparer.Ordinal);
        if (names is not null)
        {
            foreach (var name in names)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    result.Add(name);
            }
        }
        return result.ToList();
    }

    private static void AddProbes(List<string> results, string directory, string cue, IReadOnlyList<string> extensions)
    {
        foreach (var extension in extensions)
            results.Add(directory.Length == 0 ? cue + extension : $"{directory}/{cue}{extension}");
    }

    private static bool Contains(IReadOnlyList<string> extensions, string extension)
    {
        foreach (var supported in extensions)
        {
            if (string.Equals(supported, extension, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}

/// <summary>Knobs for <see cref="AudioLibrary.BuildPlaylists"/>.</summary>
public sealed class AudioLibraryPlaylistOptions
{
    /// <summary>
    /// Name of the playlist wrapping all tracks (default <c>music</c>). Null or blank skips it;
    /// a name colliding with a track throws.
    /// </summary>
    public string? AllTracksPlaylistName { get; set; } = "music";

    /// <summary>Repeat mode of each single-track playlist (default: loop that track forever).</summary>
    public PlaylistRepeatMode SingleTrackRepeat { get; set; } = PlaylistRepeatMode.Track;

    /// <summary>Repeat mode of the all-tracks playlist (default: loop the playlist).</summary>
    public PlaylistRepeatMode AllTracksRepeat { get; set; } = PlaylistRepeatMode.Playlist;

    /// <summary>Shuffle mode of the all-tracks playlist (default: bag — every track once per cycle).</summary>
    public PlaylistShuffleMode AllTracksShuffle { get; set; } = PlaylistShuffleMode.Bag;

    /// <summary>Maps a track name to its music-context tag (default: empty context).</summary>
    public Func<string, string>? ContextForTrack { get; set; }
}
