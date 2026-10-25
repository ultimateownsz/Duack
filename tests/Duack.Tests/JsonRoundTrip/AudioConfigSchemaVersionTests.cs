using Duack;

namespace Duack.Tests.JsonRoundTrip;

/// <summary>
/// Pins the schema-version stamp on the authored config document. A missing stamp reads as the
/// current version; a zero/legacy stamp is normalized up to 1 by Sanitize; a FUTURE stamp is
/// preserved (never silently downgraded to this build's schema), so an older build opening a
/// newer blob can still decide what to do with it. The settings document's stamp is pinned
/// alongside it in <c>PlayerAudioSettingsSchemaVersionTests</c>.
/// </summary>
public class AudioConfigSchemaVersionTests
{
    [Fact]
    public void AudioConfig_serializes_the_schema_version()
    {
        Assert.Contains("\"schema_version\"", AudioConfigJson.Serialize(new AudioConfig()));
    }

    [Fact]
    public void AudioConfig_round_trips_the_schema_version()
    {
        var parsed = AudioConfigJson.Parse(AudioConfigJson.Serialize(new AudioConfig()));

        Assert.Equal(AudioConfig.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void AudioConfig_without_a_stamp_reads_as_the_current_version()
    {
        var parsed = AudioConfigJson.Parse("""{ "master_volume": 0.5 }""");

        Assert.Equal(AudioConfig.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void AudioConfig_Sanitize_normalizes_a_zero_stamp_up_to_one()
    {
        var parsed = AudioConfigJson.Parse("""{ "schema_version": 0 }""");

        parsed.Sanitize();

        Assert.Equal(1, parsed.SchemaVersion);
    }

    [Fact]
    public void AudioConfig_Sanitize_preserves_a_future_stamp()
    {
        var parsed = AudioConfigJson.Parse("""{ "schema_version": 5 }""");

        parsed.Sanitize();

        Assert.Equal(5, parsed.SchemaVersion);
    }
}
