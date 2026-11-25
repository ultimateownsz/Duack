using Duack.Sample.Core;

namespace Duack.Sample.Tests;

/// <summary>
/// Pins the demo wiring both samples share (it used to be copy-pasted in the desktop `Game1` and
/// the web `Index.razor`): the battle/overworld context mapping over the discovered tracks and the
/// exact demo mix, so the desktop (MonoGame) and web (KNI) heads cannot drift apart.
/// </summary>
public class SampleAudioTests
{
    private static readonly string[] DemoFiles =
    {
        "SFX/battle-start.wav",
        "SFX/dialog.wav",
        "SFX/hit.wav",
        "SFX/notice-video-game-sfx.wav",
        "Tracks/battle.ogg",
        "Tracks/hospital.ogg",
        "Tracks/overworld.ogg",
    };

    private static AudioLibrary DemoLibrary() => AudioLibrary.FromFiles(DemoFiles);

    [Fact]
    public void BuildPlaylists_builds_one_single_track_playlist_per_track_plus_the_music_bag()
    {
        var playlists = SampleAudio.BuildPlaylists(DemoLibrary());

        Assert.Equal(
            new[] { "battle", "hospital", "music", "overworld" },
            playlists.Playlists.Keys.OrderBy(k => k, StringComparer.Ordinal));

        var battle = playlists.Playlists["battle"];
        Assert.Single(battle.Tracks);
        Assert.Equal("battle", battle.Tracks[0].Name);
        Assert.Equal(PlaylistRepeatMode.Track, battle.Repeat);
        Assert.Equal(PlaylistShuffleMode.Off, battle.Shuffle);

        var music = playlists.Playlists["music"];
        Assert.Equal(3, music.Tracks.Count);
        Assert.Equal(PlaylistRepeatMode.Playlist, music.Repeat);
        Assert.Equal(PlaylistShuffleMode.Bag, music.Shuffle);
    }

    [Fact]
    public void BuildPlaylists_maps_only_the_battle_track_to_the_Battle_context()
    {
        var playlists = SampleAudio.BuildPlaylists(DemoLibrary());

        Assert.Equal("Battle", playlists.Playlists["battle"].Tracks[0].Context);
        Assert.Equal("Overworld", playlists.Playlists["overworld"].Tracks[0].Context);
        Assert.Equal("Overworld", playlists.Playlists["hospital"].Tracks[0].Context);
        Assert.All(
            playlists.Playlists["music"].Tracks,
            track => Assert.Equal(track.Name == "battle" ? "Battle" : "Overworld", track.Context));
    }

    [Fact]
    public void BuildPlaylists_rejects_a_library_without_tracks()
    {
        // The music bag needs at least one track; an sfx-only library must not fabricate one.
        var sfxOnly = AudioLibrary.FromFiles(new[] { "SFX/hit.wav" });

        var playlists = SampleAudio.BuildPlaylists(sfxOnly);

        Assert.Empty(playlists.Playlists);
    }

    [Fact]
    public void BuildConfig_pins_the_demo_mix_both_samples_play()
    {
        var playlists = SampleAudio.BuildPlaylists(DemoLibrary());

        var config = SampleAudio.BuildConfig();

        Assert.Equal(0.8f, config.Buses[BusNames.Master].Volume);
        Assert.Equal(0.8f, config.Buses[BusNames.Music].Volume);
        Assert.Equal(1.2f, config.Music.CrossfadeSeconds);
        Assert.Equal(1f, config.Music.Contexts["Overworld"]);
        Assert.Equal(0.9f, config.Music.Contexts["Battle"]);

        var duck = config.Music.DuckSources["sfx"];
        Assert.Equal(0.3f, duck.Ratio);
        Assert.Equal(0.06f, duck.AttackSeconds);
        Assert.Equal(0.15f, duck.HoldSeconds);
        Assert.Equal(0.1f, duck.ReleaseSeconds);
    }

    [Fact]
    public void BuildConfig_wires_the_ambience_bus_without_a_bus_duck()
    {
        var config = SampleAudio.BuildConfig();

        Assert.Equal(0.55f, config.Buses[SampleAudio.AmbienceBus].Volume);
        Assert.Equal("music", config.Music.DuckSources[SampleAudio.SfxDuckSource].Target);

        // No bus-targeted duck: the scenario (e.g. dialogue ducking a bus) stays out of the
        // sample until a real project proves it. The mechanism keeps its core unit tests.
        Assert.DoesNotContain(config.Music.DuckSources.Values, d => d.Target != "music");
    }
    [Fact]
    public void DescribeBuses_lists_the_fixed_sfx_bus_and_the_user_bus_through_one_readback()
    {
        // Bus unification surfaced in the demo: one readback line for every bus of the resolved
        // table - the fixed sfx bus is the same machinery as the user ambience bus, gain and
        // live duck contribution per bus. Both heads print exactly this string.
        var mixer = new BusMixer(SampleAudio.BuildConfig().Buses);

        var line = SampleAudio.DescribeBuses(mixer);

        Assert.Contains("master: gain=", line);
        Assert.Contains("music: gain=", line);
        Assert.Contains("sfx: gain=1, duck=1", line);
        Assert.Contains("ambience: gain=0.55, duck=1", line);
    }
}
