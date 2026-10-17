namespace Duack.Tools;

/// <summary>What the converter should do with one WAV file.</summary>
public enum ConversionAction
{
    /// <summary>Encode the WAV to OGG in the output directory.</summary>
    Convert,

    /// <summary>The OGG output already exists and is newer than the WAV; leave it alone.</summary>
    SkipUpToDate,

    /// <summary>The file is not a song; leave it as WAV (sound effects stay WAV).</summary>
    SkipNotSong,
}

/// <summary>
/// The pure per-file decision behind <c>duack convert</c>. Kept separate from the encoder and file
/// system so the policy ("only songs become OGG; skip up-to-date output unless forced") is testable.
/// </summary>
public static class ConversionPlanner
{
    /// <summary>Decides what to do with one WAV.</summary>
    /// <param name="isSong">Whether the file's name is in the song set.</param>
    /// <param name="force">When true, re-encode even an up-to-date output.</param>
    /// <param name="oggExists">Whether the OGG output already exists.</param>
    /// <param name="oggUpToDate">Whether the OGG output is at least as new as the source WAV.</param>
    public static ConversionAction Decide(bool isSong, bool force, bool oggExists, bool oggUpToDate)
    {
        if (!isSong)
            return ConversionAction.SkipNotSong;

        if (!force && oggExists && oggUpToDate)
            return ConversionAction.SkipUpToDate;

        return ConversionAction.Convert;
    }
}
