using NAudio.Wave;

namespace DuackCli.Tests;

/// <summary>Pins the loudness measurement + normalization helpers.</summary>
public class LoudnessAnalyzerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "duack-loud-" + Guid.NewGuid().ToString("N"));

    public LoudnessAnalyzerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string WriteSine(string name, float amplitude, double seconds = 0.25)
    {
        var path = Path.Combine(_dir, name);
        const int sampleRate = 44100;
        var format = new WaveFormat(sampleRate, 16, 1);
        using var writer = new WaveFileWriter(path, format);
        var count = (int)(sampleRate * seconds);
        for (var i = 0; i < count; i++)
            writer.WriteSample((float)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * amplitude));
        return path;
    }

    [Fact]
    public void Measure_computes_rms_and_peak_for_a_sine()
    {
        var wav = WriteSine("tone.wav", 0.5f);
        var m = LoudnessAnalyzer.Measure(wav);

        Assert.Equal("tone", m.Name);
        Assert.InRange(m.Peak, 0.49f, 0.51f);
        // RMS of a sine with peak 0.5 is 0.5 / sqrt(2).
        Assert.InRange(m.Rms, 0.34f, 0.36f);
        Assert.True(double.IsFinite(m.RmsDb));
    }

    [Fact]
    public void Measure_throws_for_missing_file()
    {
        Assert.Throws<FileNotFoundException>(() => LoudnessAnalyzer.Measure(Path.Combine(_dir, "nope.wav")));
    }

    [Fact]
    public void Loudest_picks_the_highest_rms()
    {
        var quiet = WriteSine("quiet.wav", 0.2f);
        var loud = WriteSine("loud.wav", 0.8f);

        var measurements = LoudnessAnalyzer.MeasureAll(new[] { quiet, loud });
        var loudest = LoudnessAnalyzer.Loudest(measurements);

        Assert.NotNull(loudest);
        Assert.Equal("loud", loudest!.Name);
    }

    [Fact]
    public void GainToMatch_returns_one_for_equal_tracks()
    {
        var a = WriteSine("a.wav", 0.5f);
        var b = WriteSine("b.wav", 0.5f);
        var ma = LoudnessAnalyzer.Measure(a);
        var mb = LoudnessAnalyzer.Measure(b);

        Assert.InRange(LoudnessAnalyzer.GainToMatch(ma, mb), 0.99, 1.01);
    }

    [Fact]
    public void GainToMatch_boosts_quieter_track()
    {
        var quiet = WriteSine("quiet.wav", 0.2f);
        var loud = WriteSine("loud.wav", 0.8f);
        var mq = LoudnessAnalyzer.Measure(quiet);
        var ml = LoudnessAnalyzer.Measure(loud);

        var gain = LoudnessAnalyzer.GainToMatch(mq, ml);
        Assert.InRange(gain, 3.9, 4.1);
    }

    [Fact]
    public void NormalizeFile_writes_a_wav_matching_reference_rms()
    {
        var quiet = WriteSine("quiet.wav", 0.2f);
        var loud = WriteSine("loud.wav", 0.8f);
        var mq = LoudnessAnalyzer.Measure(quiet);
        var ml = LoudnessAnalyzer.Measure(loud);
        var gain = LoudnessAnalyzer.GainToMatch(mq, ml);

        var outPath = Path.Combine(_dir, "quiet_norm.wav");
        LoudnessAnalyzer.NormalizeFile(quiet, outPath, gain);

        Assert.True(File.Exists(outPath));
        var mn = LoudnessAnalyzer.Measure(outPath);
        Assert.InRange(mn.Rms, ml.Rms * 0.95, ml.Rms * 1.05);
    }

    [Fact]
    public void NormalizeDirectory_normalizes_all_tracks_to_loudest()
    {
        WriteSine("a.wav", 0.2f);
        WriteSine("b.wav", 0.8f);

        var outDir = Path.Combine(_dir, "out");
        var results = LoudnessAnalyzer.NormalizeDirectory(_dir, outDir);

        Assert.Equal(2, results.Count);
        Assert.True(Directory.Exists(outDir));
        Assert.Equal(2, Directory.GetFiles(outDir, "*.wav").Length);

        var rmsValues = Directory.GetFiles(outDir, "*.wav")
            .Select(LoudnessAnalyzer.Measure)
            .Select(m => m.Rms)
            .ToList();
        Assert.InRange(rmsValues.Max() - rmsValues.Min(), 0, 0.05);
    }

    [Fact]
    public void Aliases_delegate_to_primary()
    {
        var wav = WriteSine("tone.wav", 0.5f);
        var a = LoudnessAnalyzer.Measure(wav);
        var b = LoudnessMeter.Measure(wav);
        var c = Loudness.Measure(wav);

        Assert.Equal(a.Rms, b.Rms);
        Assert.Equal(a.Rms, c.Rms);
    }
}


