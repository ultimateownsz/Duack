using System.Text.Json;
using Duack;

namespace Duack.Tests.JsonRoundTrip;

/// <summary>
/// Pins the schema-version stamp on the two JSON documents Duack persists: the authored
/// <see cref="AudioConfig"/> and the per-user <see cref="PlayerAudioSettings"/>. A missing stamp
/// reads as the current version; a zero/legacy stamp is normalized up to 1 by Sanitize; a
/// FUTURE stamp is preserved (never silently downgraded to this build's schema), so an older build
/// opening a newer blob can still decide what to do with it.
/// </summary>
public class SchemaVersionTests
{
    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    // ---- AudioConfig ----

    [Fact]
    public void AudioConfig_serializes_the_schema_version()
    {
        Assert.Contains("\"schema_version\"", new AudioConfig().Serialize());
    }

    [Fact]
    public void AudioConfig_round_trips_the_schema_version()
    {
        var parsed = AudioConfig.Parse(new AudioConfig().Serialize());

        Assert.Equal(AudioConfig.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void AudioConfig_without_a_stamp_reads_as_the_current_version()
    {
        var parsed = AudioConfig.Parse("""{ "master_volume": 0.5 }""");

        Assert.Equal(AudioConfig.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void AudioConfig_Sanitize_normalizes_a_zero_stamp_up_to_one()
    {
        var parsed = AudioConfig.Parse("""{ "schema_version": 0 }""");

        parsed.Sanitize();

        Assert.Equal(1, parsed.SchemaVersion);
    }

    [Fact]
    public void AudioConfig_Sanitize_preserves_a_future_stamp()
    {
        var parsed = AudioConfig.Parse("""{ "schema_version": 5 }""");

        parsed.Sanitize();

        Assert.Equal(5, parsed.SchemaVersion);
    }

    // ---- PlayerAudioSettings ----

    [Fact]
    public void PlayerAudioSettings_serializes_the_schema_version()
    {
        var json = JsonSerializer.Serialize(new PlayerAudioSettings(), SnakeCase);

        Assert.Contains("\"schema_version\"", json);
    }

    [Fact]
    public void PlayerAudioSettings_round_trips_the_schema_version()
    {
        var json = JsonSerializer.Serialize(new PlayerAudioSettings(), SnakeCase);
        var parsed = JsonSerializer.Deserialize<PlayerAudioSettings>(json, SnakeCase)!;

        Assert.Equal(PlayerAudioSettings.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void PlayerAudioSettings_without_a_stamp_reads_as_the_current_version()
    {
        var parsed = JsonSerializer.Deserialize<PlayerAudioSettings>(
            """{ "music_master_volume": 0.5 }""",
            SnakeCase)!;

        Assert.Equal(PlayerAudioSettings.CurrentSchemaVersion, parsed.SchemaVersion);
    }

    [Fact]
    public void PlayerAudioSettings_Sanitize_normalizes_a_zero_stamp_up_to_one()
    {
        var parsed = JsonSerializer.Deserialize<PlayerAudioSettings>(
            """{ "schema_version": 0 }""",
            SnakeCase)!;

        parsed.Sanitize();

        Assert.Equal(1, parsed.SchemaVersion);
    }

    [Fact]
    public void PlayerAudioSettings_Sanitize_preserves_a_future_stamp()
    {
        var parsed = JsonSerializer.Deserialize<PlayerAudioSettings>(
            """{ "schema_version": 9 }""",
            SnakeCase)!;

        parsed.Sanitize();

        Assert.Equal(9, parsed.SchemaVersion);
    }
}
