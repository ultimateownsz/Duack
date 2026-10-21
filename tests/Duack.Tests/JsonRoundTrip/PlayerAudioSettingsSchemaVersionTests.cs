using System.Text.Json;

namespace Duack.Tests.JsonRoundTrip;

/// <summary>
/// Pins the schema-version stamp on the per-user settings document. The settings wire format is
/// part of the CORE (the stores that persist it live there), so its pins stay in this suite - the
/// config document's pins moved to the JSON package's own test project.
/// </summary>
public class PlayerAudioSettingsSchemaVersionTests
{
    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

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
