using NAudio.Wave;

namespace Duack.Tools.Tests;

/// <summary>Writes a short real WAV file for tests that need decodable audio.</summary>
internal static class TestWav
{
    public static void WriteSine(string path, double seconds = 0.1)
    {
        const int sampleRate = 44100;
        var format = new WaveFormat(sampleRate, 16, 1);
        using var writer = new WaveFileWriter(path, format);

        var count = (int)(sampleRate * seconds);
        for (var i = 0; i < count; i++)
            writer.WriteSample((float)(Math.Sin(2 * Math.PI * 440 * i / sampleRate) * 0.5));
    }
}
