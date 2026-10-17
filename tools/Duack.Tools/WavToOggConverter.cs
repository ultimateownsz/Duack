using System;
using System.IO;
using NAudio.Wave;
using OggVorbisEncoder;

namespace Duack.Tools;

/// <summary>
/// Managed WAV → OGG Vorbis encoder (NAudio decode, OggVorbisEncoder encode; no ffmpeg). Ported
/// from the game's build-time converter so the tool owns the NAudio/OggVorbisEncoder dependency.
/// The Ogg stream serial is derived from the output path, so repeat encodes stay byte-comparable.
/// </summary>
public static class WavToOggConverter
{
    /// <summary>0..1 variable-bitrate setting; 0.5 ~= libvorbis q5 (~160 kbps stereo).</summary>
    public const float DefaultQuality = 0.5f;

    /// <summary>Clamps a raw quality argument into the valid 0..1 range.</summary>
    public static float ClampQuality(float quality) => Math.Clamp(quality, 0f, 1f);

    /// <summary>Encodes one WAV file to OGG at <paramref name="oggPath"/> (always overwrites).</summary>
    public static void Encode(string wavPath, string oggPath, float quality)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(oggPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var wav = new WaveFileReader(wavPath);
        var channels = wav.WaveFormat.Channels;
        var sampleRate = wav.WaveFormat.SampleRate;
        var samples = ReadSamples(wav.ToSampleProvider(), channels, (int)wav.SampleCount);

        using var output = File.Create(oggPath);
        EncodeStream(samples, sampleRate, channels, ClampQuality(quality), output, Fnv1a(Path.GetFullPath(oggPath)));
    }

    /// <summary>De-interleaves the sample provider into per-channel float arrays (-1..1).</summary>
    private static float[][] ReadSamples(ISampleProvider provider, int channels, int frameCount)
    {
        var interleaved = new float[Math.Max(frameCount, 0) * channels];
        var read = provider.Read(interleaved, 0, interleaved.Length);

        var frames = read / channels;
        var result = new float[channels][];
        for (var ch = 0; ch < channels; ch++)
        {
            result[ch] = new float[frames];
            for (var i = 0; i < frames; i++)
                result[ch][i] = interleaved[i * channels + ch];
        }

        return result;
    }

    private static void EncodeStream(float[][] samples, int sampleRate, int channels, float quality, Stream output, int serial)
    {
        const int writeBufferSize = 8192;
        var frames = samples[0].Length;

        var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, quality);
        var oggStream = new OggStream(serial);

        // Vorbis streams begin with three headers (identity, comments, codebooks).
        oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(new Comments()));
        oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        FlushPages(oggStream, output, force: true);

        var processingState = ProcessingState.Create(info);
        for (var readIndex = 0; readIndex < frames; readIndex += writeBufferSize)
        {
            var length = Math.Min(writeBufferSize, frames - readIndex);
            processingState.WriteData(samples, length, readIndex);
            Drain(oggStream, processingState, output);
        }

        processingState.WriteEndOfStream();
        Drain(oggStream, processingState, output);
        FlushPages(oggStream, output, force: true);
    }

    private static void Drain(OggStream oggStream, ProcessingState processingState, Stream output)
    {
        while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
        {
            oggStream.PacketIn(packet);
            FlushPages(oggStream, output, force: false);
        }
    }

    private static void FlushPages(OggStream oggStream, Stream output, bool force)
    {
        while (oggStream.PageOut(out OggPage page, force))
        {
            output.Write(page.Header, 0, page.Header.Length);
            output.Write(page.Body, 0, page.Body.Length);
        }
    }

    private static int Fnv1a(string text)
    {
        var hash = unchecked((int)2166136261);
        foreach (var c in text)
            hash = unchecked((hash ^ c) * 16777619);
        return hash;
    }
}
