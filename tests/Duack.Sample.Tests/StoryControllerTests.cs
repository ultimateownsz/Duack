using Duack.Sample.Core;

namespace Duack.Sample.Tests;

/// <summary>
/// Pins the scripted showcase storyline: the rectangle walks, the NPC notices (notice sfx), the
/// dialog speaks (dialog sfx per line), the battle opens (battle-start sfx, battle music) and trades
/// pitched hit cues (marble bag, no back-to-back repeats) until one side faints — then the story
/// loops back to the overworld. Cue names are pinned as literals because they must match the
/// shipped audio files exactly.
/// </summary>
public class StoryControllerTests
{
    /// <summary>Drives the auto-advancing story (Update ticks) until it reaches the named scene.</summary>
    private static void DriveToScene(StoryController story, StoryScene scene)
    {
        for (var i = 0; i < 200 && story.Scene != scene; i++)
            story.Update(1f);
        Assert.Equal(scene, story.Scene);
    }

    private static void ToBattle(StoryController story)
    {
        DriveToScene(story, StoryScene.Battle);
    }

    [Fact]
    public void A_fresh_story_starts_in_the_walk_scene()
    {
        var story = new StoryController();

        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Equal(0f, story.WalkProgress);
    }

    [Fact]
    public void Walking_accumulates_progress_and_fires_the_notice_cue_once_at_the_npc()
    {
        var story = new StoryController();

        var early = story.Update(0.1f);
        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Null(early.Sfx);
        Assert.False(early.SceneChanged);

        // Maintainer ruling (Nov 25, round 2): the entry cue fires on the OLD screen - the
        // notice cue rings on the walk screen, no switch attached.
        var notice = story.Update(4f); // crosses the notice threshold
        Assert.Equal("notice-video-game-sfx", notice.Sfx);
        Assert.Equal(SampleAudio.AmbienceBus, notice.Bus); // the demo's ambience routing
        Assert.False(notice.SceneChanged);                 // switch comes a beat later
        Assert.Equal(StoryScene.Walk, story.Scene);

        // One beat later (>= the full 1.08 s notice cue plus its tail), the clean switch:
        // scene change carries NO cue.
        var switchFrame = story.Update(1.5f);
        Assert.True(switchFrame.SceneChanged);
        Assert.Null(switchFrame.Sfx);
        Assert.Equal(StoryScene.Notice, story.Scene);
        Assert.Equal(StoryController.NoticeThreshold, story.WalkProgress); // frozen at the NPC

        var idle = story.Update(0.2f); // standing at the NPC: no re-trigger
        Assert.Null(idle.Sfx);
        Assert.False(idle.SceneChanged);
    }

    [Fact]
    public void Notice_times_out_into_the_dialog_with_the_dialog_cue()
    {
        var story = new StoryController();
        DriveToScene(story, StoryScene.Notice);

        var cue = story.Update(1.5f); // past the notice pause

        Assert.Equal("dialog", cue.Sfx);   // the first line's cue rings on the NOTICE screen
        Assert.False(cue.SceneChanged);
        Assert.Equal(StoryScene.Notice, story.Scene);

        var switchFrame = story.Update(1f); // one beat later
        Assert.True(switchFrame.SceneChanged);
        Assert.Null(switchFrame.Sfx);
        Assert.Equal(StoryScene.Dialog, story.Scene);
        Assert.Equal(0, story.DialogLine);
    }

    [Fact]
    public void Dialog_advances_line_by_line_with_the_dialog_cue_and_ends_with_battle_start()
    {
        var story = new StoryController();
        DriveToScene(story, StoryScene.Dialog);

        var line1 = story.Advance();
        Assert.Equal(1, story.DialogLine);
        Assert.Equal("dialog", line1.Sfx);

        var line2 = story.Advance();
        Assert.Equal(2, story.DialogLine);
        Assert.Equal("dialog", line2.Sfx);

        // The sting rings on the DIALOG screen - the battle-music crossfade starts with it
        // here, before any switch happens.
        var sting = story.Advance(); // past the last line
        Assert.Equal("battle-start", sting.Sfx);
        Assert.False(sting.SceneChanged);
        Assert.Equal(StoryScene.Dialog, story.Scene);

        // One beat later, the intro screen loads clean.
        var switchFrame = story.Update(1f);
        Assert.True(switchFrame.SceneChanged);
        Assert.Null(switchFrame.Sfx);
        Assert.Equal(StoryScene.BattleIntro, story.Scene);
    }

    [Fact]
    public void Dialog_lines_auto_advance_on_their_timer()
    {
        var story = new StoryController();
        DriveToScene(story, StoryScene.Dialog);

        var frame = story.Update(2f); // past the per-line timer

        Assert.Equal(1, story.DialogLine);
        Assert.Equal("dialog", frame.Sfx);
        Assert.Equal(StoryScene.Dialog, story.Scene);
    }

    [Fact]
    public void The_battle_intro_starts_the_battle_playlist()
    {
        var story = new StoryController();
        ToBattle_story_helper_to_intro(story);

        var frame = story.Update(1.5f); // past the intro beat

        Assert.Equal("battle", frame.Music);
        Assert.True(frame.SceneChanged);
        Assert.Equal(StoryScene.Battle, story.Scene);
        Assert.Equal(1f, story.PlayerHp);
        Assert.Equal(1f, story.FoeHp);
    }

    /// <summary>Advances a fresh story up to (but not into) the Battle scene.</summary>
    private static void ToBattle_story_helper_to_intro(StoryController story)
    {
        DriveToScene(story, StoryScene.BattleIntro);
    }

    [Fact]
    public void Battle_trades_hits_player_first_at_natural_pitch()
    {
        var story = new StoryController();

        ToBattle(story);

        var first = story.Update(1.5f);
        Assert.Equal("hit", first.Sfx);
        Assert.Equal(story.LastHitPitch, first.Pitch);
        Assert.Equal(0f, first.Pitch); // no marble-bag pitch variation on hits
        Assert.Equal(0.8f, story.FoeHp, 3);   // the player's monster attacks first
        Assert.Equal(1f, story.PlayerHp, 3);

        var second = story.Update(1.5f);
        Assert.Equal("hit", second.Sfx);
        Assert.Equal(0f, second.Pitch);
        Assert.Equal(0.8f, story.PlayerHp, 3); // the foe strikes back
        Assert.Equal(0.8f, story.FoeHp, 3);
    }

    [Fact]
    public void Hit_pitches_are_natural_now_that_the_marble_bag_is_gone()
    {
        var story = new StoryController();
        ToBattle(story);

        var pitches = new List<float>();
        for (var i = 0; i < 20 && story.Scene == StoryScene.Battle; i++)
        {
            var frame = story.Update(1.5f);
            if (frame.Sfx == "hit") pitches.Add(frame.Pitch);
        }

        Assert.All(pitches, pitch => Assert.Equal(0f, pitch));
    }

    [Fact]
    public void The_ko_hit_lands_on_the_battle_screen_and_the_next_tick_switches_to_a_clean_walk()
    {
        // Maintainer ruling (Nov 25): no cue from a finished gameplay screen survives into the
        // next screen. The KO hit sounds ON the battle screen; only the tick AFTER it switches
        // to the walk, carrying the overworld music switch and NO sfx.
        var story = new StoryController();
        ToBattle(story);

        // Fight to the KO and grab the fainting blow's frame.
        var ko = default(StoryFrame);
        for (var i = 0; i < 20 && story.PlayerHp > 0f && story.FoeHp > 0f; i++)
            ko = story.Update(1.5f);

        Assert.Equal("hit", ko.Sfx);              // the KO hit still plays...
        Assert.Null(ko.Music);                    // ...ON the battle screen: no switch yet,
        Assert.False(ko.SceneChanged);            // still the battle scene when it lands.
        Assert.Equal(StoryScene.Battle, story.Scene);

        // The next tick - the ending beat, sized so even a drawn hit-2 finishes (>= 1.73 s)
        // before the walk loads - switches to the walk, clean.
        var walk = story.Update(2f);

        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Equal("overworld", walk.Music);
        Assert.True(walk.SceneChanged);
        Assert.Null(walk.Sfx);                    // no cue survives into the walk screen
        Assert.Equal(0f, story.WalkProgress);
    }

    [Fact]
    public void The_battle_ending_beat_cannot_be_skipped_by_space()
    {
        var story = new StoryController();
        ToBattle(story);
        for (var i = 0; i < 20 && story.PlayerHp > 0f && story.FoeHp > 0f; i++)
            story.Update(1.5f); // the KO has landed; the story is in the ending beat

        var skipped = story.Advance();

        Assert.Equal(default(StoryFrame), skipped); // same rule as the battle itself: no skip
        Assert.Equal(StoryScene.Battle, story.Scene);
    }

    [Fact]
    public void Restart_during_the_battle_ending_returns_to_the_walk_clean()
    {
        var story = new StoryController();
        ToBattle(story);
        for (var i = 0; i < 20 && story.PlayerHp > 0f && story.FoeHp > 0f; i++)
            story.Update(1.5f); // mid-ending restart

        var frame = story.Restart();

        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Equal("overworld", frame.Music);
        Assert.True(frame.SceneChanged);
        Assert.Null(frame.Sfx);
        Assert.Equal(0f, story.WalkProgress);
        Assert.Equal(0, story.BattleRound);
        Assert.Equal(1f, story.PlayerHp);
        Assert.Equal(1f, story.FoeHp);

        // The ending state is fully consumed: the restarted walk progresses into the notice.
        var notice = story.Update(10f);
        Assert.Equal("notice-video-game-sfx", notice.Sfx);
    }

    [Fact]
    public void Restart_returns_to_the_walk_and_requests_overworld_music()
    {
        var story = new StoryController();
        ToBattle(story);
        story.Update(1.5f); // one hit in

        var frame = story.Restart();

        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Equal("overworld", frame.Music);
        Assert.True(frame.SceneChanged);
        Assert.Null(frame.Sfx);
        Assert.Equal(0f, story.WalkProgress);
        Assert.Equal(0, story.DialogLine);
        Assert.Equal(0, story.BattleRound);
        Assert.Equal(1f, story.PlayerHp);
        Assert.Equal(1f, story.FoeHp);
    }

    [Fact]
    public void Space_kicks_off_the_exit_cue_beat_and_never_skips_a_beat_or_the_battle()
    {
        var story = new StoryController();

        // Space on the walk: jump to the NPC and fire the notice cue ON the walk screen.
        var notice = story.Advance();
        Assert.Equal("notice-video-game-sfx", notice.Sfx);
        Assert.False(notice.SceneChanged);
        Assert.Equal(StoryScene.Walk, story.Scene);

        // The beat itself is NOT skippable: Space during it does nothing (one rule for every
        // beat, entry or ending, mirroring the battle-ending rule).
        Assert.Equal(default(StoryFrame), story.Advance());
        Assert.Equal(StoryScene.Walk, story.Scene);

        // The intro still skips straight into the battle - a music/scene switch, no cue.
        DriveToScene(story, StoryScene.BattleIntro);
        var battle = story.Advance();
        Assert.Equal("battle", battle.Music);
        Assert.Null(battle.Sfx);
        Assert.True(battle.SceneChanged);
        Assert.Equal(StoryScene.Battle, story.Scene);

        var idle = story.Advance(); // the spectacle itself cannot be skipped
        Assert.Equal(default, idle);
        Assert.Equal(StoryScene.Battle, story.Scene);
    }

    [Fact]
    public void Across_the_full_loop_no_frame_ever_carries_both_a_cue_and_a_scene_switch()
    {
        // Maintainer ruling (Nov 25): every cue lands on the screen it belongs to; every
        // SceneChanged frame carries NO sfx. Pin the whole loop, walk -> ... -> battle -> walk.
        var story = new StoryController();
        var backToWalk = false;

        for (var i = 0; i < 400 && !backToWalk; i++)
        {
            var frame = story.Update(1f);
            Assert.False(frame.Sfx is not null && frame.SceneChanged,
                $"frame {i} mixes cue '{frame.Sfx}' with a scene switch");
            Assert.False(frame.Sfx is not null && frame.Music is not null,
                $"frame {i} mixes cue '{frame.Sfx}' with a music switch");
            backToWalk = frame.SceneChanged && story.Scene == StoryScene.Walk && story.BattleRound > 0;
        }

        Assert.True(backToWalk, "the loop never returned to the walk after the battle");
    }
    [Fact]
    public void Every_beat_outlasts_its_cue_so_no_sound_is_clipped_at_the_switch()
    {
        // Maintainer clarification (Nov 25, round 4): D27/D28 mean "no lingering sound reaches
        // the next screen", NOT truncation - each beat's cue must finish naturally on its
        // screen before the switch loads. ISfxBackend exposes no completion telemetry, so the
        // beat constants are sized from the shipped WAV assets plus the tail margin below.
        // Reading sibling-sample content is a filesystem read, not a project reference.
        var sfx = FindShippedSfxDirectory();
        const float tail = StoryController.BeatTailSeconds;
        const float floatEpsilon = 0.005f; // float representation headroom; the invariant allows nothing else

        Assert.True(
            StoryController.NoticeCueBeatSeconds + floatEpsilon >=
            WavDurationSeconds(Path.Combine(sfx, "notice-video-game-sfx.wav")) + tail,
            $"notice beat {StoryController.NoticeCueBeatSeconds}s must outlast the notice cue");
        Assert.True(
            StoryController.StingBeatSeconds + floatEpsilon >=
            WavDurationSeconds(Path.Combine(sfx, "battle-start.wav")) + tail,
            $"sting beat {StoryController.StingBeatSeconds}s must outlast the battle-start cue");
        Assert.True(
            StoryController.BattleEndingSeconds + floatEpsilon >= LongestHitSeconds(sfx) + tail,
            $"battle-ending beat {StoryController.BattleEndingSeconds}s must outlast the longest hit draw");
    }

    /// <summary>
    /// Walks up from the test assembly to the repository root and into the desktop sample's
    /// shipped SFX directory (tolerates running from bin).
    /// </summary>
    private static string FindShippedSfxDirectory()
    {
        var probe = Path.Combine("samples", "Duack.Sample.Desktop", "Content", "Audio", "SFX", "battle-start.wav");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, probe)))
            dir = dir.Parent!;
        Assert.True(dir is not null, "repository root (with the desktop sample's SFX content) not found");
        return Path.Combine(dir.FullName, "samples", "Duack.Sample.Desktop", "Content", "Audio", "SFX");
    }

    /// <summary>16-bit PCM WAV duration in seconds (RIFF/WAVE chunks walked for the data block).</summary>
    private static float WavDurationSeconds(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8));

        var channels = BitConverter.ToInt16(bytes, 22);
        var sampleRate = BitConverter.ToInt32(bytes, 24);
        var bitsPerSample = BitConverter.ToInt16(bytes, 34);

        var offset = 12;
        while (offset + 8 <= bytes.Length && !bytes.AsSpan(offset, 4).SequenceEqual("data"u8))
        {
            var size = BitConverter.ToInt32(bytes, offset + 4);
            offset += 8 + size + (size & 1);
        }
        Assert.True(offset + 8 <= bytes.Length, $"no data chunk in {path}");

        var dataSize = BitConverter.ToInt32(bytes, offset + 4);
        return dataSize / (float)(sampleRate * channels * (bitsPerSample / 8));
    }

    /// <summary>The longest hit variant the KO draw can pull from the sample's bag.</summary>
    private static float LongestHitSeconds(string sfxDirectory) =>
        Directory.GetFiles(sfxDirectory, "hit*.wav")
            .Select(WavDurationSeconds)
            .DefaultIfEmpty(0f)
            .Max();
}