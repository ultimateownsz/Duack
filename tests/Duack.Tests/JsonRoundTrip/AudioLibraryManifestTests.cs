using System.Text.Json;
using Duack;

namespace Duack.Tests.JsonRoundTrip;

/// <summary>
/// Pins the audio-library manifest wire format: the checked-in JSON that lets a browser host
/// (which cannot enumerate directories) learn which sfx and tracks a content root ships.
/// Source-generated, snake_case, tolerant of hand edits, forward-stamped by schema_version.
/// </summary>
public class AudioLibraryManifestTests
{
    private static AudioLibraryManifest Sample() => new()
    {
        SchemaVersion = AudioLibraryManifest.CurrentSchemaVersion,
        Sfx = new List<string> { "dialog", "hit" },
        Tracks = new List<string> { "battle", "overworld" },
    };

    [Fact]
    public void Serialize_then_Deserialize_round_trips_both_lists_and_the_schema_stamp()
    {
        var parsed = AudioLibraryManifestJson.Deserialize(AudioLibraryManifestJson.Serialize(Sample()));

        Assert.Equal(AudioLibraryManifest.CurrentSchemaVersion, parsed.SchemaVersion);
        Assert.Equal(new[] { "dialog", "hit" }, parsed.Sfx);
        Assert.Equal(new[] { "battle", "overworld" }, parsed.Tracks);
    }

    [Fact]
    public void Serialize_uses_snake_case_keys()
    {
        var json = AudioLibraryManifestJson.Serialize(Sample());

        Assert.Contains("\"schema_version\"", json);
        Assert.Contains("\"sfx\"", json);
        Assert.Contains("\"tracks\"", json);
        Assert.DoesNotContain("SchemaVersion", json);
        Assert.DoesNotContain("Tracks\"", json); // Pascal-case property name must never leak
    }

    [Fact]
    public void A_missing_schema_stamp_reads_as_the_current_version()
    {
        var parsed = AudioLibraryManifestJson.Deserialize("""
            { "sfx": ["hit"], "tracks": [] }
            """);

        Assert.Equal(AudioLibraryManifest.CurrentSchemaVersion, parsed.SchemaVersion);
        Assert.Equal(new[] { "hit" }, parsed.Sfx);
    }

    [Fact]
    public void A_future_schema_stamp_is_preserved_on_load_and_save()
    {
        var parsed = AudioLibraryManifestJson.Deserialize("""
            { "schema_version": 99, "sfx": [], "tracks": [] }
            """);

        Assert.Equal(99, parsed.SchemaVersion);
        Assert.Contains("\"schema_version\": 99", AudioLibraryManifestJson.Serialize(parsed));
    }

    [Fact]
    public void Unknown_fields_survive_a_load_save_cycle()
    {
        var parsed = AudioLibraryManifestJson.Deserialize("""
            { "schema_version": 1, "sfx": [], "tracks": [], "future_section": { "x": 1 } }
            """);

        var json = AudioLibraryManifestJson.Serialize(parsed);

        Assert.Contains("\"future_section\"", json);
        Assert.Contains("\"x\"", json);
    }

    [Fact]
    public void Missing_or_null_lists_load_as_empty_and_serialize_as_empty_arrays()
    {
        var missing = AudioLibraryManifestJson.Deserialize("{}");
        Assert.Empty(missing.Sfx);
        Assert.Empty(missing.Tracks);

        var nulled = AudioLibraryManifestJson.Deserialize("""
            { "sfx": null, "tracks": null }
            """);
        Assert.Empty(nulled.Sfx);
        Assert.Empty(nulled.Tracks);

        var json = AudioLibraryManifestJson.Serialize(missing);
        Assert.Contains("\"sfx\": []", json);
        Assert.Contains("\"tracks\": []", json);
    }

    [Fact]
    public void Malformed_json_throws_a_json_exception()
    {
        Assert.ThrowsAny<JsonException>(() => AudioLibraryManifestJson.Deserialize("{ nope"));
    }

    [Fact]
    public void An_audio_library_round_trips_through_its_manifest()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav", "Tracks/battle.ogg" });

        var restored = AudioLibrary.FromManifest(AudioLibraryManifestJson.Deserialize(
            AudioLibraryManifestJson.Serialize(library.ToManifest())));

        Assert.Equal(library.Sfx, restored.Sfx);
        Assert.Equal(library.Tracks, restored.Tracks);
    }

    [Fact]
    public void FromManifest_normalizes_hand_edited_lists()
    {
        var restored = AudioLibrary.FromManifest(new AudioLibraryManifest
        {
            Sfx = new List<string> { "zap", "hit", "hit", "  " },
            Tracks = new List<string> { "battle" },
        });

        Assert.Equal(new[] { "hit", "zap" }, restored.Sfx);
        Assert.Equal(new[] { "battle" }, restored.Tracks);
    }
}
