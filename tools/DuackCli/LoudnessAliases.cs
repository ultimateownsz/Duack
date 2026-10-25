using Duack;
namespace DuackCli;

public static class LoudnessMeter
{
    public static TrackLoudness Measure(string p) => LoudnessAnalyzer.Measure(p);
    public static TrackLoudness Analyze(string p) => LoudnessAnalyzer.Analyze(p);
    public static TrackLoudness MeasureTrack(string p) => LoudnessAnalyzer.MeasureTrack(p);
    public static TrackLoudness GetLoudness(string p) => LoudnessAnalyzer.GetLoudness(p);
    public static IReadOnlyList<TrackLoudness> MeasureAll(IEnumerable<string> p) => LoudnessAnalyzer.MeasureAll(p);
    public static IReadOnlyList<TrackLoudness> MeasureTracks(IEnumerable<string> p) => LoudnessAnalyzer.MeasureTracks(p);
    public static IReadOnlyList<TrackLoudness> AnalyzeAll(IEnumerable<string> p) => LoudnessAnalyzer.AnalyzeAll(p);
    public static TrackLoudness? Loudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.Loudest(t);
    public static TrackLoudness? FindLoudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.FindLoudest(t);
    public static TrackLoudness? LoudestTrack(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.LoudestTrack(t);
    public static double GainToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainToMatch(a, b);
    public static double GainDbToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDbToMatch(a, b);
    public static double Gain(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.Gain(a, b);
    public static double GainDb(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDb(a, b);
    public static void Normalize(string i, string o, double g) => LoudnessAnalyzer.Normalize(i, o, g);
    public static void NormalizeFile(string i, string o, double g) => LoudnessAnalyzer.NormalizeFile(i, o, g);
    public static void WriteNormalized(string i, string o, double g) => LoudnessAnalyzer.WriteNormalized(i, o, g);
    public static void ApplyGain(string i, string o, double g) => LoudnessAnalyzer.ApplyGain(i, o, g);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeDirectory(string i, string o) => LoudnessAnalyzer.NormalizeDirectory(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeAll(string i, string o) => LoudnessAnalyzer.NormalizeAll(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> AnalyzeAndNormalize(string i, string o) => LoudnessAnalyzer.AnalyzeAndNormalize(i, o);
}

public static class Loudness
{
    public static TrackLoudness Measure(string p) => LoudnessAnalyzer.Measure(p);
    public static TrackLoudness Analyze(string p) => LoudnessAnalyzer.Analyze(p);
    public static TrackLoudness MeasureTrack(string p) => LoudnessAnalyzer.MeasureTrack(p);
    public static TrackLoudness GetLoudness(string p) => LoudnessAnalyzer.GetLoudness(p);
    public static IReadOnlyList<TrackLoudness> MeasureAll(IEnumerable<string> p) => LoudnessAnalyzer.MeasureAll(p);
    public static IReadOnlyList<TrackLoudness> MeasureTracks(IEnumerable<string> p) => LoudnessAnalyzer.MeasureTracks(p);
    public static IReadOnlyList<TrackLoudness> AnalyzeAll(IEnumerable<string> p) => LoudnessAnalyzer.AnalyzeAll(p);
    public static TrackLoudness? Loudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.Loudest(t);
    public static TrackLoudness? FindLoudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.FindLoudest(t);
    public static TrackLoudness? LoudestTrack(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.LoudestTrack(t);
    public static double GainToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainToMatch(a, b);
    public static double GainDbToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDbToMatch(a, b);
    public static double Gain(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.Gain(a, b);
    public static double GainDb(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDb(a, b);
    public static void Normalize(string i, string o, double g) => LoudnessAnalyzer.Normalize(i, o, g);
    public static void NormalizeFile(string i, string o, double g) => LoudnessAnalyzer.NormalizeFile(i, o, g);
    public static void WriteNormalized(string i, string o, double g) => LoudnessAnalyzer.WriteNormalized(i, o, g);
    public static void ApplyGain(string i, string o, double g) => LoudnessAnalyzer.ApplyGain(i, o, g);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeDirectory(string i, string o) => LoudnessAnalyzer.NormalizeDirectory(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeAll(string i, string o) => LoudnessAnalyzer.NormalizeAll(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> AnalyzeAndNormalize(string i, string o) => LoudnessAnalyzer.AnalyzeAndNormalize(i, o);
}

public static class TrackLoudnessAnalyzer
{
    public static TrackLoudness Measure(string p) => LoudnessAnalyzer.Measure(p);
    public static TrackLoudness Analyze(string p) => LoudnessAnalyzer.Analyze(p);
    public static TrackLoudness MeasureTrack(string p) => LoudnessAnalyzer.MeasureTrack(p);
    public static TrackLoudness GetLoudness(string p) => LoudnessAnalyzer.GetLoudness(p);
    public static IReadOnlyList<TrackLoudness> MeasureAll(IEnumerable<string> p) => LoudnessAnalyzer.MeasureAll(p);
    public static IReadOnlyList<TrackLoudness> MeasureTracks(IEnumerable<string> p) => LoudnessAnalyzer.MeasureTracks(p);
    public static IReadOnlyList<TrackLoudness> AnalyzeAll(IEnumerable<string> p) => LoudnessAnalyzer.AnalyzeAll(p);
    public static TrackLoudness? Loudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.Loudest(t);
    public static TrackLoudness? FindLoudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.FindLoudest(t);
    public static TrackLoudness? LoudestTrack(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.LoudestTrack(t);
    public static double GainToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainToMatch(a, b);
    public static double GainDbToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDbToMatch(a, b);
    public static double Gain(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.Gain(a, b);
    public static double GainDb(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDb(a, b);
    public static void Normalize(string i, string o, double g) => LoudnessAnalyzer.Normalize(i, o, g);
    public static void NormalizeFile(string i, string o, double g) => LoudnessAnalyzer.NormalizeFile(i, o, g);
    public static void WriteNormalized(string i, string o, double g) => LoudnessAnalyzer.WriteNormalized(i, o, g);
    public static void ApplyGain(string i, string o, double g) => LoudnessAnalyzer.ApplyGain(i, o, g);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeDirectory(string i, string o) => LoudnessAnalyzer.NormalizeDirectory(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeAll(string i, string o) => LoudnessAnalyzer.NormalizeAll(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> AnalyzeAndNormalize(string i, string o) => LoudnessAnalyzer.AnalyzeAndNormalize(i, o);
}

public static class AudioLoudness
{
    public static TrackLoudness Measure(string p) => LoudnessAnalyzer.Measure(p);
    public static TrackLoudness Analyze(string p) => LoudnessAnalyzer.Analyze(p);
    public static TrackLoudness MeasureTrack(string p) => LoudnessAnalyzer.MeasureTrack(p);
    public static TrackLoudness GetLoudness(string p) => LoudnessAnalyzer.GetLoudness(p);
    public static IReadOnlyList<TrackLoudness> MeasureAll(IEnumerable<string> p) => LoudnessAnalyzer.MeasureAll(p);
    public static IReadOnlyList<TrackLoudness> MeasureTracks(IEnumerable<string> p) => LoudnessAnalyzer.MeasureTracks(p);
    public static IReadOnlyList<TrackLoudness> AnalyzeAll(IEnumerable<string> p) => LoudnessAnalyzer.AnalyzeAll(p);
    public static TrackLoudness? Loudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.Loudest(t);
    public static TrackLoudness? FindLoudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.FindLoudest(t);
    public static TrackLoudness? LoudestTrack(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.LoudestTrack(t);
    public static double GainToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainToMatch(a, b);
    public static double GainDbToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDbToMatch(a, b);
    public static double Gain(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.Gain(a, b);
    public static double GainDb(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDb(a, b);
    public static void Normalize(string i, string o, double g) => LoudnessAnalyzer.Normalize(i, o, g);
    public static void NormalizeFile(string i, string o, double g) => LoudnessAnalyzer.NormalizeFile(i, o, g);
    public static void WriteNormalized(string i, string o, double g) => LoudnessAnalyzer.WriteNormalized(i, o, g);
    public static void ApplyGain(string i, string o, double g) => LoudnessAnalyzer.ApplyGain(i, o, g);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeDirectory(string i, string o) => LoudnessAnalyzer.NormalizeDirectory(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeAll(string i, string o) => LoudnessAnalyzer.NormalizeAll(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> AnalyzeAndNormalize(string i, string o) => LoudnessAnalyzer.AnalyzeAndNormalize(i, o);
}

public static class LoudnessComparer
{
    public static TrackLoudness Measure(string p) => LoudnessAnalyzer.Measure(p);
    public static TrackLoudness Analyze(string p) => LoudnessAnalyzer.Analyze(p);
    public static TrackLoudness MeasureTrack(string p) => LoudnessAnalyzer.MeasureTrack(p);
    public static TrackLoudness GetLoudness(string p) => LoudnessAnalyzer.GetLoudness(p);
    public static IReadOnlyList<TrackLoudness> MeasureAll(IEnumerable<string> p) => LoudnessAnalyzer.MeasureAll(p);
    public static IReadOnlyList<TrackLoudness> MeasureTracks(IEnumerable<string> p) => LoudnessAnalyzer.MeasureTracks(p);
    public static IReadOnlyList<TrackLoudness> AnalyzeAll(IEnumerable<string> p) => LoudnessAnalyzer.AnalyzeAll(p);
    public static TrackLoudness? Loudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.Loudest(t);
    public static TrackLoudness? FindLoudest(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.FindLoudest(t);
    public static TrackLoudness? LoudestTrack(IEnumerable<TrackLoudness> t) => LoudnessAnalyzer.LoudestTrack(t);
    public static double GainToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainToMatch(a, b);
    public static double GainDbToMatch(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDbToMatch(a, b);
    public static double Gain(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.Gain(a, b);
    public static double GainDb(TrackLoudness a, TrackLoudness b) => LoudnessAnalyzer.GainDb(a, b);
    public static void Normalize(string i, string o, double g) => LoudnessAnalyzer.Normalize(i, o, g);
    public static void NormalizeFile(string i, string o, double g) => LoudnessAnalyzer.NormalizeFile(i, o, g);
    public static void WriteNormalized(string i, string o, double g) => LoudnessAnalyzer.WriteNormalized(i, o, g);
    public static void ApplyGain(string i, string o, double g) => LoudnessAnalyzer.ApplyGain(i, o, g);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeDirectory(string i, string o) => LoudnessAnalyzer.NormalizeDirectory(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> NormalizeAll(string i, string o) => LoudnessAnalyzer.NormalizeAll(i, o);
    public static IReadOnlyList<(TrackLoudness Track, double Gain)> AnalyzeAndNormalize(string i, string o) => LoudnessAnalyzer.AnalyzeAndNormalize(i, o);
}


