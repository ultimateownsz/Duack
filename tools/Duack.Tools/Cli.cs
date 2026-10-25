using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Duack.Tools;

/// <summary>
/// Testable command dispatcher for the <c>duack</c> CLI. Writes user-facing text to the supplied
/// writers so an end-to-end test can capture output without touching the real console.
/// </summary>
public static class Cli
{
    /// <summary>Dispatches <paramref name="args"/> and returns the process exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            PrintUsage(error);
            return 1;
        }

        try
        {
            return args[0] switch
            {
                "init" => RunInit(args[1..], output, error),
                "validate" => RunValidate(args[1..], output, error),
                "convert" => RunConvert(args[1..], output, error),
                "scan" => RunScan(args[1..], output, error),
                "normalize" or "loudness" or "analyze" or "analyse" or "measure" or "loudnorm" => RunNormalize(args[1..], output, error),
                "-h" or "--help" or "help" => PrintUsage(output),
                _ => Unknown(args[0], error),
            };
        }
        catch (Exception ex)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Unknown(string command, TextWriter error)
    {
        error.WriteLine($"error: unknown command '{command}'.");
        PrintUsage(error);
        return 1;
    }

    private static int PrintUsage(TextWriter writer)
    {
        writer.WriteLine("usage: duack <command> [options]");
        writer.WriteLine();
        writer.WriteLine("  init     <contentDir> [--out <file>] [--force] [--skills] [--skills-dir <dir>]");
        writer.WriteLine("  validate <config.json> [--playlists <file>]");
        writer.WriteLine("  convert  <contentDir> <outDir> [--config <file>] [--playlists <file>]");
        writer.WriteLine("           [--quality <0..1>] [--force] [--all]");
        writer.WriteLine("           (a playlist source is required; --all converts every WAV)");
        writer.WriteLine("  scan     <audioRoot> [--out <file>]");
        writer.WriteLine("           (writes an audio-manifest.json of the SFX/ + Tracks/ cues)");
        writer.WriteLine("  normalize <dir> [<outDir>] [--out <dir>] [--config <file>] [--write-config]");
        writer.WriteLine("           (measures track loudness, finds the loudest, and writes gain-normalized WAVs)");
        return 0;
    }

    // ---- init ----

    private static int RunInit(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseArgs(args, valueOptions: new[] { "--out", "--skills-dir" }, flags: new[] { "--force", "--skills" }, out var parsed, out var errorMessage))
            return Fail(error, errorMessage);

        if (parsed.Positional.Count != 1)
            return Fail(error, "init requires exactly one <contentDir>.");

        var contentDir = parsed.Positional[0];
        if (!Directory.Exists(contentDir))
            return Fail(error, $"content directory not found: {contentDir}");

        var outPath = parsed.Value("--out") ?? Path.Combine(contentDir, "audio-config.json");
        if (File.Exists(outPath) && !parsed.Has("--force"))
            return Fail(error, $"{outPath} already exists; pass --force to overwrite.");

        var wavs = EnumerateWavs(contentDir);
        var config = ConfigScaffolder.Build(wavs);
        WriteAllText(outPath, AudioConfigJson.Serialize(config));
        output.WriteLine($"[duack] wrote {outPath} ({config.Tracks?.Count ?? 0} tracks, {wavs.Count} wavs).");

        if (parsed.Has("--skills"))
        {
            var src = FindSkillsDirectory();
            if (src is null)
            {
                error.WriteLine("warning: duack-skills directory not found; skipping skill install.");
            }
            else
            {
                var dest = parsed.Value("--skills-dir");
                if (string.IsNullOrWhiteSpace(dest))
                    dest = Directory.Exists(".opencode") ? Path.Combine(".opencode", "duack-skills") : "duack-skills";
                CopyDirectory(src, dest);
                output.WriteLine($"[duack] installed skills to {dest}");
            }
        }

        return 0;
    }

    private static string? FindSkillsDirectory()
    {
        var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var start in starts)
        {
            var dir = start;
            while (dir is not null)
            {
                var candidate = Path.Combine(dir, "duack-skills");
                if (Directory.Exists(candidate))
                    return candidate;
                var parent = Directory.GetParent(dir);
                dir = parent?.FullName;
            }
        }
        return null;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    // ---- validate ----

    private static int RunValidate(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseArgs(args, valueOptions: new[] { "--playlists" }, flags: Array.Empty<string>(), out var parsed, out var errorMessage))
            return Fail(error, errorMessage);

        if (parsed.Positional.Count != 1)
            return Fail(error, "validate requires exactly one <config.json>.");

        var configPath = parsed.Positional[0];
        if (!TryReadConfig(configPath, out var config, out errorMessage))
            return Fail(error, errorMessage);

        PlaylistConfig? playlists = null;
        if (parsed.Value("--playlists") is { } playlistPath &&
            !TryReadPlaylists(playlistPath, out playlists, out errorMessage))
        {
            return Fail(error, errorMessage);
        }

        var issues = ConfigValidator.Validate(config!, playlists);
        foreach (var issue in issues)
        {
            var label = issue.Severity == IssueSeverity.Error ? "error" : "warning";
            error.WriteLine($"{label}: {issue.Message}");
        }

        if (issues.Any(i => i.Severity == IssueSeverity.Error))
            return 1;

        if (issues.Count == 0)
            output.WriteLine("[duack] ok");
        return 0;
    }

    // ---- convert ----

    private static int RunConvert(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseArgs(
                args,
                valueOptions: new[] { "--config", "--playlists", "--quality" },
                flags: new[] { "--force", "--all" },
                out var parsed,
                out var errorMessage))
            return Fail(error, errorMessage);

        if (parsed.Positional.Count != 2)
            return Fail(error, "convert requires <contentDir> and <outDir>.");

        var contentDir = parsed.Positional[0];
        var outDir = parsed.Positional[1];
        if (!Directory.Exists(contentDir))
            return Fail(error, $"content directory not found: {contentDir}");

        // The playlist is the authoritative song list: only named songs become OGG and SFX stay WAV.
        // A playlist source (--playlists, or an embedded `playlists` section in --config) is therefore
        // required; --all is the explicit opt-in to convert EVERY WAV (SFX included) and is mutually
        // exclusive with a playlist source.
        var all = parsed.Has("--all");
        var playlistOption = parsed.Value("--playlists");
        var configOption = parsed.Value("--config");
        if (all && (playlistOption is not null || configOption is not null))
            return Fail(error, "choose either --all or a playlist source (--playlists/--config), not both.");

        SongSet? songs = null;
        if (playlistOption is { } playlistPath)
        {
            if (!TryReadPlaylists(playlistPath, out var playlists, out errorMessage))
                return Fail(error, errorMessage);
            songs = SongSet.FromPlaylists(playlists!);
        }
        else if (configOption is { } configPath)
        {
            if (!TryReadConfig(configPath, out var config, out errorMessage))
                return Fail(error, errorMessage);
            if (PlaylistFile.FromConfig(config!) is { } embedded)
                songs = SongSet.FromPlaylists(embedded);
            else
                return Fail(error, $"{configPath} has no playlists section; pass --playlists or --all.");
        }
        else if (!all)
        {
            return Fail(error, "no playlist source: pass --playlists <file>, --config <file> (with a playlists section), or --all to convert every WAV.");
        }

        var quality = WavToOggConverter.DefaultQuality;
        if (parsed.Value("--quality") is { } qualityText)
        {
            if (!float.TryParse(qualityText, NumberStyles.Float, CultureInfo.InvariantCulture, out quality))
                return Fail(error, $"invalid --quality '{qualityText}' (expected a number in 0..1).");
            quality = WavToOggConverter.ClampQuality(quality);
        }

        var force = parsed.Has("--force");
        Directory.CreateDirectory(outDir);

        var converted = 0;
        var upToDate = 0;
        var notSong = 0;
        foreach (var wavPath in EnumerateWavs(contentDir))
        {
            var name = Path.GetFileNameWithoutExtension(wavPath);
            var oggPath = Path.Combine(outDir, name + ".ogg");

            var isSong = songs?.Contains(name) ?? true;
            var oggExists = File.Exists(oggPath);
            var oggUpToDate = oggExists &&
                              File.GetLastWriteTimeUtc(oggPath) >= File.GetLastWriteTimeUtc(wavPath);

            switch (ConversionPlanner.Decide(isSong, force, oggExists, oggUpToDate))
            {
                case ConversionAction.Convert:
                    WavToOggConverter.Encode(wavPath, oggPath, quality);
                    converted++;
                    break;
                case ConversionAction.SkipUpToDate:
                    upToDate++;
                    break;
                default:
                    notSong++;
                    break;
            }
        }

        // A content directory may hold an ALREADY-encoded .ogg (a cue that ships no WAV master).
        // There is nothing to encode, and dropping it would lose the cue, so it is copied through
        // verbatim - unless the output already holds a newer copy of the same file.
        var copied = 0;
        foreach (var oggPath in EnumerateOggs(contentDir))
        {
            var target = Path.Combine(outDir, Path.GetFileName(oggPath));
            if (File.Exists(target) &&
                File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(oggPath))
            {
                continue;
            }

            File.Copy(oggPath, target, overwrite: true);
            copied++;
        }

        output.WriteLine($"[duack] converted={converted} copied={copied} up-to-date={upToDate} not-song={notSong} ({outDir})");
        return 0;
    }

    // ---- scan ----

    private static int RunScan(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseArgs(args, valueOptions: new[] { "--out" }, flags: Array.Empty<string>(), out var parsed, out var errorMessage))
            return Fail(error, errorMessage);

        if (parsed.Positional.Count != 1)
            return Fail(error, "scan requires exactly one <audioRoot>.");

        var audioRoot = parsed.Positional[0];
        if (!Directory.Exists(audioRoot))
            return Fail(error, $"audio root not found: {audioRoot}");

        var library = AudioLibrary.ScanDirectory(audioRoot);
        WarnAboutUnloadableFiles(audioRoot, error);

        var outPath = parsed.Value("--out") ?? Path.Combine(audioRoot, "audio-manifest.json");
        WriteAllText(outPath, AudioLibraryManifestJson.Serialize(library.ToManifest()));

        output.WriteLine($"[duack] sfx={library.Sfx.Count} tracks={library.Tracks.Count} -> {outPath}");
        return 0;
    }

    /// <summary>
    /// Warns about files in the convention directories the loader can never open (wrong type, or an
    /// upper-cased extension — the case mismatch a case-sensitive host would 404 on). Warnings only:
    /// the scan still writes the manifest for everything it could classify.
    /// </summary>
    private static void WarnAboutUnloadableFiles(string audioRoot, TextWriter error)
    {
        foreach (var (directory, supported) in new[]
                 {
                     (AudioLibrary.SfxManageryName, AudioLibrary.SfxExtensions),
                     (AudioLibrary.TracksDirectoryName, AudioLibrary.TrackExtensions),
                 })
        {
            var full = Path.Combine(audioRoot, directory);
            if (!Directory.Exists(full)) continue;

            foreach (var file in Directory.EnumerateFiles(full))
            {
                var extension = Path.GetExtension(file);
                if (supported.Contains(extension, StringComparer.Ordinal)) continue;

                error.WriteLine(
                    $"warning: {directory}/{Path.GetFileName(file)} has unsupported type '{extension}' " +
                    $"(loadable: {string.Join(", ", supported)}); it was not listed in the manifest.");
            }
        }
    }

    // ---- normalize / loudness ----

    private static int RunNormalize(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParseArgs(
                args,
                valueOptions: new[] { "--out", "--config" },
                flags: new[] { "--write-config" },
                out var parsed,
                out var errorMessage))
            return Fail(error, errorMessage);

        if (parsed.Positional.Count < 1 || parsed.Positional.Count > 2)
            return Fail(error, "normalize requires <dir> and an optional <outDir>.");

        var inputDir = parsed.Positional[0];
        if (!Directory.Exists(inputDir))
            return Fail(error, $"directory not found: {inputDir}");

        var outDir = parsed.Positional.Count == 2 ? parsed.Positional[1] : parsed.Value("--out");

        var wavPaths = Directory.EnumerateFiles(inputDir, "*.wav")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wavPaths.Count == 0)
            return Fail(error, $"no WAV files found in: {inputDir}");

        var measurements = wavPaths.Select(LoudnessAnalyzer.Measure).ToList();
        var reference = LoudnessAnalyzer.Loudest(measurements);

        output.WriteLine($"[duack] measured {measurements.Count} track(s); loudest: {reference?.Name} " +
                        $"(RMS {FormatDb(reference?.RmsDb ?? double.NegativeInfinity)} dBFS)");

        foreach (var m in measurements)
        {
            var gain = reference is null ? 1.0 : LoudnessAnalyzer.GainToMatch(m, reference);
            var gainDb = LoudnessAnalyzer.GainDbToMatch(m, reference!);
            output.WriteLine($"  {m.Name}: RMS {FormatDb(m.RmsDb)} dBFS, peak {FormatDb(m.PeakDb)} dBFS, " +
                            $"gain {gainDb:+0.00;-0.00} dB (x{gain:0.000})");
        }

        if (!string.IsNullOrWhiteSpace(outDir))
        {
            LoudnessAnalyzer.NormalizeDirectory(inputDir, outDir!);
            output.WriteLine($"[duack] wrote normalized WAVs to {outDir}");
        }

        if (parsed.Has("--write-config"))
        {
            var configPath = parsed.Value("--config");
            if (string.IsNullOrWhiteSpace(configPath))
                configPath = Path.Combine(inputDir, "audio-config.json");

            AudioConfig? config = null;
            if (File.Exists(configPath))
            {
                if (!TryReadConfig(configPath, out config, out errorMessage))
                    return Fail(error, errorMessage);
            }
            else
            {
                config = ConfigScaffolder.Build(wavPaths.Select(Path.GetFileName).Where(n => n is not null).Select(n => n!));
            }

            config!.Tracks ??= new Dictionary<string, TrackConfig>();
            foreach (var m in measurements)
            {
                if (!config.Tracks.TryGetValue(m.Name, out var track))
                {
                    track = new TrackConfig();
                    config.Tracks[m.Name] = track;
                }
                track.Loudnorm.I = double.IsFinite(m.RmsDb) ? Math.Round(m.RmsDb, 2) : track.Loudnorm.I;
            }

            WriteAllText(configPath!, AudioConfigJson.Serialize(config));
            output.WriteLine($"[duack] updated tracks in {configPath}");
        }
        else if (parsed.Value("--config") is { } explicitConfigPath)
        {
            if (!TryReadConfig(explicitConfigPath, out var config, out errorMessage))
                return Fail(error, errorMessage);

            config!.Tracks ??= new Dictionary<string, TrackConfig>();
            foreach (var m in measurements)
            {
                if (!config.Tracks.TryGetValue(m.Name, out var track))
                {
                    track = new TrackConfig();
                    config.Tracks[m.Name] = track;
                }
                track.Loudnorm.I = double.IsFinite(m.RmsDb) ? Math.Round(m.RmsDb, 2) : track.Loudnorm.I;
            }

            WriteAllText(explicitConfigPath, AudioConfigJson.Serialize(config));
            output.WriteLine($"[duack] updated tracks in {explicitConfigPath}");
        }

        return 0;
    }

    private static string FormatDb(double db) =>
        double.IsNegativeInfinity(db) ? "-inf" : db.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    // ---- helpers ----

    private static List<string> EnumerateWavs(string directory) =>
        Directory.EnumerateFiles(directory, "*.wav")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Pre-encoded OGG files in <paramref name="directory"/> (copied through, not re-encoded).</summary>
    private static List<string> EnumerateOggs(string directory) =>
        Directory.EnumerateFiles(directory, "*.ogg")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool TryReadConfig(string path, out AudioConfig? config, out string errorMessage)
    {
        config = null;
        if (!File.Exists(path))
        {
            errorMessage = $"config file not found: {path}";
            return false;
        }

        if (!AudioConfigJson.TryParse(File.ReadAllText(path), out config) || config is null)
        {
            errorMessage = $"could not parse config: {path}";
            return false;
        }

        errorMessage = "";
        return true;
    }

    private static bool TryReadPlaylists(string path, out PlaylistConfig? playlists, out string errorMessage)
    {
        playlists = null;
        if (!File.Exists(path))
        {
            errorMessage = $"playlist file not found: {path}";
            return false;
        }

        if (!PlaylistFile.TryParse(File.ReadAllText(path), out playlists) || playlists is null)
        {
            errorMessage = $"could not parse playlists: {path}";
            return false;
        }

        errorMessage = "";
        return true;
    }

    private static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, contents);
    }

    private static int Fail(TextWriter error, string message)
    {
        error.WriteLine($"error: {message}");
        return 1;
    }

    private static bool TryParseArgs(
        string[] args,
        string[] valueOptions,
        string[] flags,
        out ParsedArgs parsed,
        out string errorMessage)
    {
        var values = new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase);
        var flagSet = new HashSet<string>(flags, StringComparer.OrdinalIgnoreCase);
        parsed = new ParsedArgs();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                parsed.Positional.Add(arg);
                continue;
            }

            if (flagSet.Contains(arg))
            {
                parsed.Options[arg] = "true";
            }
            else if (values.Contains(arg))
            {
                if (i + 1 >= args.Length)
                {
                    errorMessage = $"option {arg} requires a value.";
                    return false;
                }

                parsed.Options[arg] = args[++i];
            }
            else
            {
                errorMessage = $"unknown option '{arg}'.";
                return false;
            }
        }

        errorMessage = "";
        return true;
    }

    private sealed class ParsedArgs
    {
        public List<string> Positional { get; } = new();
        public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Has(string name) => Options.ContainsKey(name);
        public string? Value(string name) => Options.TryGetValue(name, out var value) ? value : null;
    }
}


