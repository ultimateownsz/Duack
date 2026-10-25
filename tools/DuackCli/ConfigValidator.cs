using Duack;
using System.Collections.Generic;
using System.Globalization;

namespace DuackCli;

/// <summary>How serious a <see cref="ValidationIssue"/> is.</summary>
public enum IssueSeverity
{
    /// <summary>The config is unusable or would be silently changed; the validate command fails.</summary>
    Error,

    /// <summary>Suspicious but loadable; the validate command still succeeds.</summary>
    Warning,
}

/// <summary>A single problem found in an audio config (or its playlists).</summary>
/// <param name="Severity">Whether the issue fails validation.</param>
/// <param name="Message">A human-readable, path-qualified description of the problem.</param>
public sealed record ValidationIssue(IssueSeverity Severity, string Message);

/// <summary>
/// Reports schema and range problems in an <see cref="AudioConfig"/> WITHOUT mutating it, so a
/// hand-edited file can be checked with clear messages before the runtime's
/// <see cref="AudioConfig.Sanitize"/> silently clamps it. Pure and headless.
/// </summary>
public static class ConfigValidator
{
    /// <summary>Validates an audio config, with no playlist cross-checks.</summary>
    public static IReadOnlyList<ValidationIssue> Validate(AudioConfig config) => Validate(config, null);

    /// <summary>Validates an audio config and, when supplied, its playlists.</summary>
    public static IReadOnlyList<ValidationIssue> Validate(AudioConfig config, PlaylistConfig? playlists)
    {
        var issues = new List<ValidationIssue>();
        if (config is null)
        {
            issues.Add(Error("config is null."));
            return issues;
        }

        ValidateSchemaVersion(config, issues);
        ValidateAudioConfig(config, issues);
        if (playlists is not null)
            ValidatePlaylists(config, playlists, issues);

        return issues;
    }

    private static void ValidateSchemaVersion(AudioConfig config, List<ValidationIssue> issues)
    {
        if (config.SchemaVersion > AudioConfig.CurrentSchemaVersion)
        {
            issues.Add(Error(
                $"schema_version {config.SchemaVersion} is newer than this build supports " +
                $"(max {AudioConfig.CurrentSchemaVersion})."));
        }
        else if (config.SchemaVersion < 1)
        {
            issues.Add(Warning(
                $"schema_version is missing or legacy (was {config.SchemaVersion}); " +
                $"it will be normalized to 1 on load."));
        }
    }

    private static void ValidateAudioConfig(AudioConfig config, List<ValidationIssue> issues)
    {
        Range("master_volume", config.MasterVolume, 0f, 1f, issues);

        if (config.Music is null)
        {
            issues.Add(Error("music section is missing."));
        }
        else
        {
            Range("music.master_volume", config.Music.MasterVolume, 0f, 1f, issues);
            Range("music.crossfade_seconds", config.Music.CrossfadeSeconds, 0.1f, 10f, issues);

            foreach (var (name, volume) in config.Music.Contexts)
                Range($"music.contexts[{name}]", volume, 0f, 1f, issues);

            foreach (var (name, source) in config.Music.DuckSources)
                ValidateDuckSource(name, source, issues);
        }

        if (config.Sfx is null)
        {
            issues.Add(Error("sfx section is missing."));
        }
        else
        {
            Range("sfx.master_volume", config.Sfx.MasterVolume, 0f, 1f, issues);
        }
    }

    private static void ValidateDuckSource(string name, DuckSourceConfig source, List<ValidationIssue> issues)
    {
        if (source is null)
        {
            issues.Add(Error($"music.duck_sources[{name}] is null."));
            return;
        }

        Range($"music.duck_sources[{name}].ratio", source.Ratio, 0f, 1f, issues);
        NonNegative($"music.duck_sources[{name}].attack_seconds", source.AttackSeconds, issues);
        NonNegative($"music.duck_sources[{name}].hold_seconds", source.HoldSeconds, issues);
        NonNegative($"music.duck_sources[{name}].release_seconds", source.ReleaseSeconds, issues);
    }

    private static void ValidatePlaylists(AudioConfig config, PlaylistConfig playlists, List<ValidationIssue> issues)
    {
        foreach (var (playlistName, definition) in playlists.Playlists)
        {
            if (definition is null)
            {
                issues.Add(Error($"playlists[{playlistName}] has no definition."));
                continue;
            }

            foreach (var track in definition.Tracks)
            {
                if (track is null || string.IsNullOrWhiteSpace(track.Name))
                {
                    issues.Add(Error($"playlists[{playlistName}] has a track with a missing name."));
                    continue;
                }

                var context = track.Context;
                if (!string.IsNullOrWhiteSpace(context) &&
                    (config.Music is null || !config.Music.Contexts.ContainsKey(context)))
                {
                    issues.Add(Warning(
                        $"playlists[{playlistName}] track '{track.Name}' references unknown " +
                        $"music context '{context}'."));
                }
            }
        }
    }

    private static void Range(string path, float value, float min, float max, List<ValidationIssue> issues)
    {
        if (!float.IsFinite(value) || value < min || value > max)
            issues.Add(Error($"{path} must be a finite value in [{N(min)}, {N(max)}] (was {N(value)})."));
    }

    private static void NonNegative(string path, float value, List<ValidationIssue> issues)
    {
        if (!float.IsFinite(value) || value < 0f)
            issues.Add(Error($"{path} must be a finite value >= 0 (was {N(value)})."));
    }

    private static string N(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static ValidationIssue Error(string message) => new(IssueSeverity.Error, message);

    private static ValidationIssue Warning(string message) => new(IssueSeverity.Warning, message);
}


