using System;
using System.IO;
using NVorbis;

namespace Duack.MonoGame;

/// <summary>
/// Decodes an OGG Vorbis stream into a RIFF/WAVE byte buffer (16-bit PCM) that
/// <c>SoundEffect.FromStream</c> accepts on every backend. KNI's parser understands RIFF WAV only
/// and cannot decode Vorbis, so OGG has to be repacked before it reaches the engine.
///
/// <para>
/// The decode holds the uncompressed samples briefly; peak memory is roughly twice the track's WAV
/// size. When the Vorbis stream reports no total sample count the buffer grows as it goes and the
/// header is patched at the end.
/// </para>
/// </summary>
internal static class OggPcmWav
{
    /// <summary>Decodes <paramref name="ogg"/> into a seekable in-memory WAV stream.</summary>
    public static Stream Decode(Stream ogg)
    {
        using var reader = new VorbisReader(ogg, false);
        var channels = reader.Channels;
        var sampleRate = reader.SampleRate;

        long totalFrames = reader.TotalSamples;
        if (totalFrames < 0) totalFrames = 0;

        // Allocate from the reported total when it is trustworthy, but always be prepared to
        // grow: NVorbis's TotalSamples is an estimate for some streams and the decode can run
        // past it, which previously overflowed the fixed buffer ("Index was outside the bounds").
        var pcm = new byte[44 + Math.Max(totalFrames, 0) * channels * 2 + 4096 * channels * 2];
        WriteWavHeader(pcm, channels, sampleRate, (int)(totalFrames * channels * 2));

        var sampleBuffer = new float[4096 * channels];
        long writtenFrames = 0;
        int readSamples;
        while ((readSamples = reader.ReadSamples(sampleBuffer, 0, sampleBuffer.Length)) > 0)
        {
            // Ensure there is room for this chunk before writing.
            var needed = 44 + (writtenFrames + readSamples / channels + 1) * channels * 2;
            if (needed > pcm.Length)
            {
                var grown = new byte[Math.Max(needed, pcm.Length * 2)];
                Buffer.BlockCopy(pcm, 0, grown, 0, Math.Min(pcm.Length, grown.Length));
                pcm = grown;
            }

            var baseByte = 44 + writtenFrames * channels * 2;
            for (var i = 0; i < readSamples; i++)
            {
                var sample = Math.Clamp((int)(sampleBuffer[i] * 32767f), short.MinValue, short.MaxValue);
                pcm[baseByte + i * 2] = (byte)sample;
                pcm[baseByte + i * 2 + 1] = (byte)((uint)sample >> 8);
            }

            writtenFrames += readSamples / channels;
        }

        // Patch the header with the actual payload size (covers both the estimate-was-wrong
        // case and the unknown-total case).
        WriteWavHeader(pcm, channels, sampleRate, (int)(writtenFrames * channels * 2));

        // Trim to the actual used size so SoundEffect.FromStream doesn't read trailing zeros.
        var finalLength = 44 + (int)(writtenFrames * channels * 2);
        var final = new byte[finalLength];
        Buffer.BlockCopy(pcm, 0, final, 0, finalLength);
        return new MemoryStream(final, writable: false);
    }

    /// <summary>Writes a canonical 44-byte RIFF/WAVE header for 16-bit PCM at the start of
    /// <paramref name="buffer"/>; <paramref name="dataLength"/> is the payload byte count.</summary>
    private static void WriteWavHeader(byte[] buffer, int channels, int sampleRate, int dataLength)
    {
        "RIFF"u8.CopyTo(buffer.AsSpan(0, 4));
        "WAVE"u8.CopyTo(buffer.AsSpan(8, 4));
        "fmt "u8.CopyTo(buffer.AsSpan(12, 4));
        "data"u8.CopyTo(buffer.AsSpan(36, 4));

        WriteInt32(buffer, 4, 36 + dataLength);             // riff chunk size
        WriteInt32(buffer, 16, 16);                         // fmt chunk size
        WriteInt16(buffer, 20, 1);                          // PCM format tag
        WriteInt16(buffer, 22, (short)channels);
        WriteInt32(buffer, 24, sampleRate);
        WriteInt32(buffer, 28, sampleRate * channels * 2);  // byte rate
        WriteInt16(buffer, 32, (short)(channels * 2));      // block align
        WriteInt16(buffer, 34, 16);                         // bits per sample
        WriteInt32(buffer, 40, dataLength);                 // data chunk size
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }
}