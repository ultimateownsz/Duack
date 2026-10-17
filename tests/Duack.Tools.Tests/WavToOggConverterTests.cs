using NAudio.Wave;

namespace Duack.Tools.Tests;

/// <summary>
/// Round-trips a real (generated) WAV through the encoder into a temp directory: the OGG must be a
/// valid Vorbis stream, non-empty, and byte-identical across repeat encodes to the same path.
/// </summary>
public class WavToOggConverterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "duack-tools-" + Guid.NewGuid().ToString("N"));

    public WavToOggConverterTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Encoding_a_wav_produces_a_vorbis_stream()
    {
        var wav = WriteSineWav("tone.wav", seconds: 0.25);
        var ogg = Path.Combine(_dir, "tone.ogg");

        WavToOggConverter.Encode(wav, ogg, WavToOggConverter.DefaultQuality);

        Assert.True(File.Exists(ogg));
        var bytes = File.ReadAllBytes(ogg);
        Assert.True(bytes.Length > 0);
        Assert.Equal("OggS", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Encoding_is_deterministic_for_the_same_output_path()
    {
        var wav = WriteSineWav("tone.wav", seconds: 0.25);
        var ogg = Path.Combine(_dir, "tone.ogg");

        WavToOggConverter.Encode(wav, ogg, 0.5f);
        var first = File.ReadAllBytes(ogg);
        WavToOggConverter.Encode(wav, ogg, 0.5f);
        var second = File.ReadAllBytes(ogg);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(2f, 1f)]
    [InlineData(0.5f, 0.5f)]
    public void ClampQuality_clamps_to_the_unit_range(float input, float expected)
    {
        Assert.Equal(expected, WavToOggConverter.ClampQuality(input));
    }

    private string WriteSineWav(string name, double seconds)
    {
        var path = Path.Combine(_dir, name);
        const int sampleRate = 44100;
        var format = new WaveFormat(sampleRate, 16, 1);
        using var writer = new WaveFileWriter(path, format);

        var count = (int)(sampleRate * seconds);
        for (var i = 0; i < count; i++)
        {
            var value = (float)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.5);
            writer.WriteSample(value);
        }

        return path;
    }
}
