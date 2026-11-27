using System;
using System.IO;
using Duack.MonoGame;
using Xunit;

namespace Duack.Adapters.Tests;

/// <summary>
/// Pins the WAV byte-slice half of the desktop seek (the OGG half rides NVorbis's
/// <c>SamplePosition</c> against a real stream): the payload is extracted from the requested
/// offset on a sample-frame boundary, the whole payload at zero, and an offset at or past the
/// payload's end yields null.
/// </summary>
public class OggPcmWavSliceTests
{
    private const int Channels = 2;
    // A low rate keeps every test offset an exact multiple of a tick-length sample frame
    // (TimeSpan quantizes to 100 ns ticks, which a 44.1 kHz offset does not survive).
    private const int SampleRate = 10;
    private const int BlockAlign = Channels * 2;

    private static byte[] Wav(int frames)
    {
        var dataLength = frames * BlockAlign;
        var wav = new byte[44 + dataLength];
        "RIFF"u8.CopyTo(wav.AsSpan(0, 4));
        "WAVE"u8.CopyTo(wav.AsSpan(8, 4));
        "fmt "u8.CopyTo(wav.AsSpan(12, 4));
        "data"u8.CopyTo(wav.AsSpan(36, 4));
        WriteInt32(wav, 4, 36 + dataLength);
        WriteInt32(wav, 16, 16);
        wav[20] = 1; // PCM
        wav[22] = Channels;
        WriteInt32(wav, 24, SampleRate);
        WriteInt32(wav, 28, SampleRate * BlockAlign);
        wav[32] = BlockAlign;
        wav[34] = 16;
        WriteInt32(wav, 40, dataLength);
        for (var i = 0; i < dataLength; i++) wav[44 + i] = (byte)(i * 7 + 3);
        return wav;
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var _ = stream;
        var bytes = new byte[stream.Length];
        var total = 0;
        int read;
        while ((read = stream.Read(bytes, total, bytes.Length - total)) > 0) total += read;
        return bytes;
    }

    [Fact]
    public void Slicing_at_zero_returns_the_whole_payload()
    {
        var wav = Wav(1000);

        var slice = ReadAll(OggPcmWav.SliceWav(new MemoryStream(wav), TimeSpan.Zero)!);

        Assert.Equal(wav.Length - 44, slice.Length);
        Assert.Equal(wav.AsSpan(44, 16).ToArray(), slice.AsSpan(0, 16).ToArray());
    }

    [Fact]
    public void Slicing_extracts_the_payload_from_the_offset_on_a_frame_boundary()
    {
        var wav = Wav(1000);

        // Half a second in: 5 sample frames x block align bytes at the 10 Hz test rate.
        var slice = ReadAll(OggPcmWav.SliceWav(new MemoryStream(wav), TimeSpan.FromSeconds(0.5))!);

        Assert.Equal(995 * BlockAlign, slice.Length);
        Assert.Equal(wav.AsSpan(44 + 5 * BlockAlign, 16).ToArray(), slice.AsSpan(0, 16).ToArray());
    }

    [Fact]
    public void Slicing_at_or_past_the_payloads_end_returns_null()
    {
        var wav = Wav(1000);

        Assert.Null(OggPcmWav.SliceWav(new MemoryStream(wav), TimeSpan.FromSeconds(200)));
    }

    [Fact]
    public void Slicing_clamps_a_negative_offset_to_the_start()
    {
        var wav = Wav(1000);

        var slice = OggPcmWav.SliceWav(new MemoryStream(wav), TimeSpan.FromSeconds(-1));

        Assert.Equal(wav.Length - 44, ReadAll(slice!).Length);
    }
}
