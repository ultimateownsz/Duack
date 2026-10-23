using NAudio.Wave;

namespace Duack.Tools.Tests;

public class LoudnessCliTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "duack-loudcli-" + Guid.NewGuid().ToString("N"));

    public LoudnessCliTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string Path2(params string[] parts) => Path.Combine(new[] { _dir }.Concat(parts).ToArray());

    private void WriteSine(string name, float amplitude)
    {
        var path = Path2(name);
        const int sampleRate = 44100;
        var format = new WaveFormat(sampleRate, 16, 1);
        using var writer = new WaveFileWriter(path, format);
        for (var i = 0; i < sampleRate; i++)
            writer.WriteSample((float)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * amplitude));
    }

    private (int code, string outText, string errText) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Cli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void Normalize_reports_loudest_and_writes_output()
    {
        WriteSine("quiet.wav", 0.2f);
        WriteSine("loud.wav", 0.8f);

        var outDir = Path2("out");
        var (code, stdout, stderr) = Run("normalize", _dir, outDir);

        Assert.Equal(0, code);
        Assert.Equal("", stderr);
        Assert.Contains("loudest: loud", stdout);
        Assert.Contains("wrote normalized WAVs", stdout);
        Assert.Equal(2, Directory.GetFiles(outDir, "*.wav").Length);
    }

    [Fact]
    public void Normalize_write_config_updates_tracks()
    {
        WriteSine("tone.wav", 0.5f);

        var (code, stdout, stderr) = Run("normalize", _dir, "--write-config");

        Assert.Equal(0, code);
        Assert.Equal("", stderr);
        Assert.Contains("updated tracks", stdout);
        Assert.True(File.Exists(Path2("audio-config.json")));
        Assert.Contains("loudnorm", File.ReadAllText(Path2("audio-config.json")));
    }

    [Fact]
    public void Normalize_alias_loudness_works()
    {
        WriteSine("tone.wav", 0.5f);

        var (code, _, _) = Run("loudness", _dir);
        Assert.Equal(0, code);
    }
}
