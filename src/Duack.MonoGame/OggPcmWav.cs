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
    public static Stream Decode(Stream ogg) => Decode(ogg, TimeSpan.Zero);

    /// <summary>
    /// Decodes <paramref name="ogg"/> into a seekable in-memory WAV stream that starts at
    /// <paramref name="startOffset"/> - NVorbis decodes sequentially, so the skipped portion is
    /// decoded-and-discarded (the cost of a deep seek; see the class doc). Null when the offset
    /// leaves no audio (at or past the stream's end) - the caller keeps playing what it has.
    /// </summary>
    public static Stream? Decode(Stream ogg, TimeSpan startOffset)
    {
        using var reader = new VorbisReader(ogg, false);
        var channels = reader.Channels;
        var sampleRate = reader.SampleRate;

        long totalFrames = reader.TotalSamples;
        if (totalFrames < 0) totalFrames = 0;

        var startFrame = startOffset <= TimeSpan.Zero
            ? 0L
            : Math.Min((long)(startOffset.TotalSeconds * sampleRate), Math.Max(totalFrames - 1, 0));

        if (startFrame > 0)
        {
            // NVorbis seeks by scanning packets; a seekable stream gets there directly. Sequential
            // discard is the fallback (and the only path on a non-seekable stream).
            try
            {
                reader.SamplePosition = startFrame;
            }
            catch (Exception)
            {
                var discarded = new float[4096 * channels];
                var remainingFrames = startFrame;
                while (remainingFrames > 0)
                {
                    var read = reader.ReadSamples(discarded, 0, discarded.Length);
                    if (read <= 0) return null;
                    remainingFrames -= read / channels;
                }
            }

            if (reader.DecodedPosition >= totalFrames) return null;
        }

        var frames = totalFrames - startFrame;

        // Allocate from the reported total when it is trustworthy, but always be prepared to
        // grow: NVorbis's TotalSamples is an estimate for some streams and the decode can run
        // past it, which previously overflowed the fixed buffer ("Index was outside the bounds").
        var pcm = new byte[44 + Math.Max(frames, 0) * channels * 2 + 4096 * channels * 2];
        WriteWavHeader(pcm, channels, sampleRate, (int)(frames * channels * 2));

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


    /// <summary>
    /// Extracts a RIFF/WAV stream's PCM payload from <paramref name="startOffset"/> onward - the
    /// byte-slice counterpart of the OGG decode (data chunk offset + position x block align,
    /// clamped to the payload). Null when the offset is at or past the payload's end.
    /// </summary>
    public static Stream? SliceWav(Stream wav, TimeSpan startOffset)
    {
        using var source = wav;
        var bytes = new byte[source.Length];
        int total = 0, read;
        while ((read = source.Read(bytes, total, (int)(bytes.Length - total))) > 0)
            total += read;

        // Walk the chunk list: "fmt " for the sample geometry, "data" for the payload bounds.
        int channels = 0, sampleRate = 0, blockAlign = 0;
        long dataStart = -1, dataLength = 0;
        var seconds = startOffset <= TimeSpan.Zero || double.IsNaN(startOffset.TotalSeconds)
            ? 0d
            : startOffset.TotalSeconds;
        for (var pos = 12; pos + 8 <= total;)
        {
            var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
            var size = bytes[pos + 4] | bytes[pos + 5] << 8 | bytes[pos + 6] << 16 | bytes[pos + 7] << 24;
            var body = pos + 8;
            if (id == "fmt ")
            {
                channels = bytes[body + 2] | bytes[body + 3] << 8;
                sampleRate = bytes[body + 4] | bytes[body + 5] << 8 | bytes[body + 6] << 16 | bytes[body + 7] << 24;
                blockAlign = bytes[body + 12] | bytes[body + 13] << 8;
            }
            else if (id == "data")
            {
                dataStart = body;
                dataLength = size;
            }

            pos = body + size + (size & 1);  // chunks are word-aligned
        }

        if (dataStart < 0 || channels <= 0 || blockAlign <= 0) return null;
        var byteOffset = (long)Math.Round(seconds * sampleRate, MidpointRounding.ToZero) * blockAlign;
        if (byteOffset < 0) byteOffset = 0;
        if (byteOffset >= dataLength) return null;
        var slice = new byte[dataLength - byteOffset];
        Buffer.BlockCopy(bytes, (int)(dataStart + byteOffset), slice, 0, slice.Length);
        return new MemoryStream(slice, writable: false);
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