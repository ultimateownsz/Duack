using Duack;
using Duack.MonoGame;

namespace Duack.Adapters.Tests;

/// <summary>
/// Pins the probe order of <see cref="SoundEffectClipLibrary.CandidateUris"/>: with a discovered
/// <see cref="AudioLibrary"/> the owning convention directory is tried first, and without one the
/// loader keeps its exact legacy flat behavior (ogg, then wav).
/// </summary>
public class SoundEffectClipLibraryTests
{
    [Fact]
    public void Candidate_uris_keep_the_legacy_flat_probe_order_without_a_library()
    {
        Assert.Equal(
            new[] { "Content/Audio/hit.ogg", "Content/Audio/hit.wav" },
            SoundEffectClipLibrary.CandidateUris(null, "hit"));
    }

    [Fact]
    public void Candidate_uris_follow_the_two_directory_convention_with_a_library()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav", "Tracks/overworld.ogg" });

        Assert.Equal(
            new[]
            {
                "Content/Audio/SFX/hit.wav",
                "Content/Audio/SFX/hit.ogg",
                "Content/Audio/hit.ogg",
                "Content/Audio/hit.wav",
            },
            SoundEffectClipLibrary.CandidateUris(library, "hit"));

        Assert.Equal(
            new[]
            {
                "Content/Audio/Tracks/overworld.ogg",
                "Content/Audio/Tracks/overworld.wav",
                "Content/Audio/overworld.ogg",
                "Content/Audio/overworld.wav",
            },
            SoundEffectClipLibrary.CandidateUris(library, "overworld"));
    }

    [Fact]
    public void Candidate_uris_accept_a_custom_root()
    {
        var library = AudioLibrary.FromFiles(new[] { "SFX/hit.wav" });

        Assert.Equal(
            new[] { "audio/SFX/hit.wav", "audio/SFX/hit.ogg", "audio/hit.ogg", "audio/hit.wav" },
            SoundEffectClipLibrary.CandidateUris(library, "hit", root: "audio"));
    }
}
