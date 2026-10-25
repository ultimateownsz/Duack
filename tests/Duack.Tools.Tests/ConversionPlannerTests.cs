namespace DuackCli.Tests;

/// <summary>
/// Pins the per-file conversion decision: only songs become OGG, up-to-date output is skipped
/// unless forced, and a non-song is always left alone.
/// </summary>
public class ConversionPlannerTests
{
    [Fact]
    public void A_non_song_is_skipped()
    {
        Assert.Equal(
            ConversionAction.SkipNotSong,
            ConversionPlanner.Decide(isSong: false, force: false, oggExists: false, oggUpToDate: false));
    }

    [Fact]
    public void A_non_song_is_skipped_even_when_forced()
    {
        Assert.Equal(
            ConversionAction.SkipNotSong,
            ConversionPlanner.Decide(isSong: false, force: true, oggExists: true, oggUpToDate: true));
    }

    [Fact]
    public void A_song_with_no_output_is_converted()
    {
        Assert.Equal(
            ConversionAction.Convert,
            ConversionPlanner.Decide(isSong: true, force: false, oggExists: false, oggUpToDate: false));
    }

    [Fact]
    public void A_song_with_a_stale_output_is_converted()
    {
        Assert.Equal(
            ConversionAction.Convert,
            ConversionPlanner.Decide(isSong: true, force: false, oggExists: true, oggUpToDate: false));
    }

    [Fact]
    public void A_song_with_an_up_to_date_output_is_skipped()
    {
        Assert.Equal(
            ConversionAction.SkipUpToDate,
            ConversionPlanner.Decide(isSong: true, force: false, oggExists: true, oggUpToDate: true));
    }

    [Fact]
    public void Force_reconverts_an_up_to_date_song()
    {
        Assert.Equal(
            ConversionAction.Convert,
            ConversionPlanner.Decide(isSong: true, force: true, oggExists: true, oggUpToDate: true));
    }
}


