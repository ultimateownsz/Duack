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
    private static void ToBattle(StoryController story)
    {
        story.Advance(); // Walk -> Notice
        story.Advance(); // Notice -> Dialog (line 0)
        story.Advance(); // line 1
        story.Advance(); // line 2
        story.Advance(); // past the last line -> BattleIntro
        story.Advance(); // BattleIntro -> Battle
        Assert.Equal(StoryScene.Battle, story.Scene);
    }

    [Fact]
    public void A_fresh_story_starts_in_the_walk_scene()
    {
        var story = new StoryController(new Random(1));

        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Equal(0f, story.WalkProgress);
    }

    [Fact]
    public void Walking_accumulates_progress_and_fires_the_notice_cue_once_at_the_npc()
    {
        var story = new StoryController(new Random(1));

        var early = story.Update(0.1f);
        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Null(early.Sfx);
        Assert.False(early.SceneChanged);

        var notice = story.Update(4f); // crosses the notice threshold
        Assert.Equal("notice-video-game-sfx", notice.Sfx);
        Assert.True(notice.SceneChanged);
        Assert.Equal(StoryScene.Notice, story.Scene);

        var idle = story.Update(0.5f); // standing at the NPC: no re-trigger
        Assert.Null(idle.Sfx);
        Assert.False(idle.SceneChanged);
    }

    [Fact]
    public void Notice_times_out_into_the_dialog_with_the_dialog_cue()
    {
        var story = new StoryController(new Random(1));
        story.Advance(); // -> Notice

        var frame = story.Update(1.5f); // past the notice pause

        Assert.Equal("dialog", frame.Sfx);
        Assert.True(frame.SceneChanged);
        Assert.Equal(StoryScene.Dialog, story.Scene);
        Assert.Equal(0, story.DialogLine);
    }

    [Fact]
    public void Dialog_advances_line_by_line_with_the_dialog_cue_and_ends_with_battle_start()
    {
        var story = new StoryController(new Random(1));
        story.Advance(); // -> Notice
        story.Advance(); // -> Dialog (line 0, cue fired on open)
        Assert.Equal(StoryScene.Dialog, story.Scene);

        var line1 = story.Advance();
        Assert.Equal(1, story.DialogLine);
        Assert.Equal("dialog", line1.Sfx);

        var line2 = story.Advance();
        Assert.Equal(2, story.DialogLine);
        Assert.Equal("dialog", line2.Sfx);

        var done = story.Advance(); // past the last line
        Assert.Equal("battle-start", done.Sfx);
        Assert.True(done.SceneChanged);
        Assert.Equal(StoryScene.BattleIntro, story.Scene);
    }

    [Fact]
    public void Dialog_lines_auto_advance_on_their_timer()
    {
        var story = new StoryController(new Random(1));
        story.Advance(); // -> Notice
        story.Advance(); // -> Dialog (line 0)

        var frame = story.Update(2f); // past the per-line timer

        Assert.Equal(1, story.DialogLine);
        Assert.Equal("dialog", frame.Sfx);
        Assert.Equal(StoryScene.Dialog, story.Scene);
    }

    [Fact]
    public void The_battle_intro_starts_the_battle_playlist()
    {
        var story = new StoryController(new Random(1));
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
        story.Advance(); // -> Notice
        story.Advance(); // -> Dialog
        story.Advance();
        story.Advance();
        story.Advance(); // -> BattleIntro
        Assert.Equal(StoryScene.BattleIntro, story.Scene);
    }

    [Fact]
    public void Battle_trades_hits_player_first_with_varying_pitch()
    {
        var story = new StoryController(new Random(1));
        ToBattle(story);

        var first = story.Update(1.5f);
        Assert.Equal("hit", first.Sfx);
        Assert.Equal(story.LastHitPitch, first.Pitch);
        Assert.Equal(0.8f, story.FoeHp, 3);   // the player's monster attacks first
        Assert.Equal(1f, story.PlayerHp, 3);

        var second = story.Update(1.5f);
        Assert.Equal("hit", second.Sfx);
        Assert.Equal(0.8f, story.PlayerHp, 3); // the foe strikes back
        Assert.Equal(0.8f, story.FoeHp, 3);

        Assert.NotEqual(first.Pitch, second.Pitch); // marble bag: no back-to-back repeats
    }

    [Fact]
    public void Hit_pitches_come_from_the_authored_marble_bag_and_stay_in_engine_range()
    {
        var story = new StoryController(new Random(1));
        ToBattle(story);

        var pitches = new List<float>();
        for (var i = 0; i < 20 && story.Scene == StoryScene.Battle; i++)
        {
            var frame = story.Update(1.5f);
            if (frame.Sfx == "hit") pitches.Add(frame.Pitch);
        }

        var authored = new[] { -0.3f, -0.15f, 0f, 0.15f, 0.3f };
        Assert.All(pitches, pitch => Assert.Contains(pitch, authored));
        Assert.True(pitches.Distinct().Count() > 1); // seeded, but more than one value over a battle
    }

    [Fact]
    public void A_fainted_side_loops_the_story_back_to_the_walk_with_overworld_music()
    {
        var story = new StoryController(new Random(1));
        ToBattle(story);

        var frame = default(StoryFrame);
        for (var i = 0; i < 20 && story.Scene == StoryScene.Battle; i++)
            frame = story.Update(1.5f);

        Assert.Equal(StoryScene.Walk, story.Scene);
        Assert.Equal("hit", frame.Sfx);          // the fainting blow still plays
        Assert.Equal("overworld", frame.Music);  // ...while the music fades back to the overworld
        Assert.True(frame.SceneChanged);
        Assert.Equal(0f, story.WalkProgress);
    }

    [Fact]
    public void Restart_returns_to_the_walk_and_requests_overworld_music()
    {
        var story = new StoryController(new Random(1));
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
    public void Advance_skips_each_pace_break_but_never_the_battle_itself()
    {
        var story = new StoryController(new Random(1));

        Assert.True(story.Advance().SceneChanged); // Walk -> Notice
        Assert.Equal(StoryScene.Notice, story.Scene);
        Assert.True(story.Advance().SceneChanged); // Notice -> Dialog
        Assert.Equal(StoryScene.Dialog, story.Scene);

        story.Advance(); // line 1
        story.Advance(); // line 2
        var intro = story.Advance(); // -> BattleIntro
        Assert.Equal("battle-start", intro.Sfx);
        Assert.True(intro.SceneChanged);

        var battle = story.Advance(); // -> Battle
        Assert.Equal("battle", battle.Music);
        Assert.Equal(StoryScene.Battle, story.Scene);

        var idle = story.Advance(); // the spectacle itself cannot be skipped
        Assert.Equal(default, idle);
        Assert.Equal(StoryScene.Battle, story.Scene);
    }
}
