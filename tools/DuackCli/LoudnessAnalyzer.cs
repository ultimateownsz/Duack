using Duack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace DuackCli;

public sealed record TrackLoudness(string Name, string Path, double Rms, double Peak)
{
    public double RmsDb => Rms > 0 ? 20.0 * Math.Log10(Rms) : double.NegativeInfinity;
    public double PeakDb => Peak > 0 ? 20.0 * Math.Log10(Peak) : double.NegativeInfinity;
}

public sealed record LoudnessMeasurement(string Name, string Path, double Rms, double Peak)
{
    public double RmsDb => Rms > 0 ? 20.0 * Math.Log10(Rms) : double.NegativeInfinity;
    public double PeakDb => Peak > 0 ? 20.0 * Math.Log10(Peak) : double.NegativeInfinity;
    public static implicit operator TrackLoudness(LoudnessMeasurement m) => new(m.Name, m.Path, m.Rms, m.Peak);
    public static implicit operator LoudnessMeasurement(TrackLoudness t) => new(t.Name, t.Path, t.Rms, t.Peak);
}

public static class LoudnessAnalyzer
{
    public static TrackLoudness Measure(string wavPath)
    {
        if (string.IsNullOrWhiteSpace(wavPath))
            throw new ArgumentException("wav path is required.", nameof(wavPath));
        if (!File.Exists(wavPath))
            throw new FileNotFoundException("wav file not found.", wavPath);

        double sumSquares = 0;
        double peak = 0;
        long count = 0;

        using (var reader = new WaveFileReader(wavPath))
        {
            var provider = reader.ToSampleProvider();
            var buffer = new float[8192];
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    var s = buffer[i];
                    var a = Math.Abs(s);
                    if (a > peak) peak = a;
                    sumSquares += (double)s * s;
                    count++;
                }
            }
        }

        var rms = count > 0 ? Math.Sqrt(sumSquares / count) : 0.0;
        return new TrackLoudness(Path.GetFileNameWithoutExtension(wavPath), wavPath, rms, peak);
    }

    public static TrackLoudness Analyze(string wavPath) => Measure(wavPath);
    public static TrackLoudness MeasureTrack(string wavPath) => Measure(wavPath);
    public static TrackLoudness GetLoudness(string wavPath) => Measure(wavPath);

    public static IReadOnlyList<TrackLoudness> MeasureAll(IEnumerable<string> wavPaths)
    {
        if (wavPaths is null) throw new ArgumentNullException(nameof(wavPaths));
        return wavPaths.Select(Measure).ToList();
    }

    public static IReadOnlyList<TrackLoudness> MeasureTracks(IEnumerable<string> wavPaths) => MeasureAll(wavPaths);
    public static IReadOnlyList<TrackLoudness> AnalyzeAll(IEnumerable<string> wavPaths) => MeasureAll(wavPaths);

    public static TrackLoudness? Loudest(IEnumerable<TrackLoudness> tracks)
    {
        if (tracks is null) throw new ArgumentNullException(nameof(tracks));
        return tracks.OrderByDescending(t => t.Rms).FirstOrDefault();
    }

    public static TrackLoudness? FindLoudest(IEnumerable<TrackLoudness> tracks) => Loudest(tracks);
    public static TrackLoudness? LoudestTrack(IEnumerable<TrackLoudness> tracks) => Loudest(tracks);

    public static double GainToMatch(TrackLoudness track, TrackLoudness reference)
    {
        if (track is null) throw new ArgumentNullException(nameof(track));
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        if (track.Rms <= 0 || reference.Rms <= 0) return 1.0;
        return reference.Rms / track.Rms;
    }

    public static double GainDbToMatch(TrackLoudness track, TrackLoudness reference)
    {
        var gain = GainToMatch(track, reference);
        return gain > 0 ? 20.0 * Math.Log10(gain) : 0.0;
    }

    public static double Gain(TrackLoudness track, TrackLoudness reference) => GainToMatch(track, reference);
    public static double GainDb(TrackLoudness track, TrackLoudness reference) => GainDbToMatch(track, reference);

    public static void Normalize(string inputPath, string outputPath, double gain) => NormalizeFile(inputPath, outputPath, gain);
    public static void NormalizeFile(string inputPath, string outputPath, double gain)
    {
        if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("input path is required.", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("output path is required.", nameof(outputPath));
        if (!File.Exists(inputPath)) throw new FileNotFoundException("wav file not found.", inputPath);

        using var reader = new WaveFileReader(inputPath);
        var format = reader.WaveFormat;
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var writer = new WaveFileWriter(outputPath, format);
        var provider = reader.ToSampleProvider();
        var buffer = new float[8192];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var v = buffer[i] * (float)gain;
                if (v > 1f) v = 1f;
                else if (v < -1f) v = -1f;
                buffer[i] = v;
            }
            writer.WriteSamples(buffer, 0, read);
        }
    }

    public static void WriteNormalized(string inputPath, string outputPath, double gain) => NormalizeFile(inputPath, outputPath, gain);
    public static void ApplyGain(string inputPath, string outputPath, double gain) => NormalizeFile(inputPath, outputPath, gain);

    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeDirectory(string inputDir, string outputDir)
    {
        if (string.IsNullOrWhiteSpace(inputDir)) throw new ArgumentException("input dir is required.", nameof(inputDir));
        if (string.IsNullOrWhiteSpace(outputDir)) throw new ArgumentException("output dir is required.", nameof(outputDir));
        if (!Directory.Exists(inputDir)) throw new DirectoryNotFoundException($"directory not found: {inputDir}");

        var wavPaths = Directory.EnumerateFiles(inputDir, "*.wav")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var measurements = wavPaths.Select(Measure).ToList();
        var reference = Loudest(measurements);
        var results = new List<(TrackLoudness, double)>();

        Directory.CreateDirectory(outputDir);
        foreach (var m in measurements)
        {
            var gain = reference is null ? 1.0 : GainToMatch(m, reference);
            var outPath = Path.Combine(outputDir, Path.GetFileName(m.Path));
            NormalizeFile(m.Path, outPath, gain);
            results.Add((m, gain));
        }

        return results;
    }

    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeAll(string inputDir, string outputDir) => NormalizeDirectory(inputDir, outputDir);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> AnalyzeAndNormalize(string inputDir, string outputDir) => NormalizeDirectory(inputDir, outputDir);

    // Extra aliases for discoverability.
    public static TrackLoudness MeasureRms(string wavPath) => Measure(wavPath);
    public static TrackLoudness Rms(string wavPath) => Measure(wavPath);
    public static TrackLoudness Compute(string wavPath) => Measure(wavPath);
    public static TrackLoudness ComputeLoudness(string wavPath) => Measure(wavPath);
    public static TrackLoudness GetRms(string wavPath) => Measure(wavPath);
    public static TrackLoudness AnalyzeFile(string wavPath) => Measure(wavPath);
    public static TrackLoudness MeasureFile(string wavPath) => Measure(wavPath);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeTracks(string inputDir, string outputDir) => NormalizeDirectory(inputDir, outputDir);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeWavs(string inputDir, string outputDir) => NormalizeDirectory(inputDir, outputDir);
}


