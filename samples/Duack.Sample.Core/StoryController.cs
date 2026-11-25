using System;
using System.Collections.Generic;
using Duack;

namespace Duack.Sample.Core;

/// <summary>Scenes of the scripted showcase storyline (one full loop of the demo).</summary>
public enum StoryScene
{
    /// <summary>The rectangle strolls toward the NPC with the overworld track playing.</summary>
    Walk,

    /// <summary>
    /// The NPC screen, loaded clean: the notice cue already rang out on the walk screen
    /// one beat before the switch (maintainer ruling, Nov 25: no cue survives into the next screen).
    /// </summary>
    Notice,

    /// <summary>A dialog box is open; each line speaks the dialog cue.</summary>
    Dialog,

    /// <summary>
    /// The intro screen, loaded clean: the battle-start sting already rang out on the dialog
    /// screen one beat before the switch, starting the battle-music crossfade there.
    /// </summary>
    BattleIntro,

    /// <summary>Monster vs monster: pitched hit cues over the battle track until someone faints.</summary>
    Battle,
}

/// <summary>
/// What the frame should do with the audio system: play <see cref="Sfx"/> at <see cref="Pitch"/>
/// (through the <see cref="SfxManager"/>, which ducks the music automatically), switch to the
/// <see cref="Music"/> playlist when it differs from the current one, and treat <see cref="SceneChanged"/>
/// as "redraw scene text". A cue never shares its frame with the SWITCH to the next screen
/// (maintainer ruling, Nov 25, extended to every entry that round): each cue - the notice
/// cue, a dialog line, the battle-start sting, the KO hit - fires on the screen it belongs
/// to, and only a later frame returns the clean switch, so every screen loads clean.
/// </summary>
public readonly record struct StoryFrame(
    string? Sfx = null,
    float Pitch = 0f,
    string? Music = null,
    bool SceneChanged = false,
    string? Bus = null);

/// <summary>
/// The sample's scripted storyline as a pure state machine — no MonoGame types, fully headless and
/// pinned by <c>Duack.Sample.Tests</c>. Timed scenes emit audio cues; the rectangle walks, gets
/// noticed, chats, and then trades pitched hit cues (a marble bag of pitches over ONE sound, so the
/// pitch variation is obvious) in a battle that loops back to the walk when a side faints.
///
/// <para>
/// Cue and playlist names are constants because they must match the shipped audio files exactly —
/// a missing file simply does not play (the <see cref="SfxManager"/> reports false), which keeps
/// the script decoupled from what is on disk.
/// </para>
/// </summary>
public sealed class StoryController
{
    public const string NoticeCue = "notice-video-game-sfx";
    public const string DialogCue = "dialog";
    public const string BattleStartCue = "battle-start";
    public const string HitCue = "hit";
    public const string OverworldPlaylist = "overworld";
    public const string BattlePlaylist = "battle";

    /// <summary>The bus the notice cue routes to (see <see cref="SampleAudio.AmbienceBus"/>).</summary>
    public const string AmbienceBus = SampleAudio.AmbienceBus;

    /// <summary>Number of dialog lines before the battle starts.</summary>
    public const int DialogLineCount = 3;

    // Timings, in seconds. Large enough to be readable, small enough to loop in ~24.5 s.
    // Beat sizing (maintainer rulings, Nov 25): a beat's cue must FINISH on its screen - the
    // no-bleed rule means "no lingering sound reaches the next screen", never truncation.
    // ISfxBackend exposes no completion telemetry, so the beat constants are sized from the
    // SHIPPED audio assets below plus BeatTailSeconds; event-driven switching would need
    // per-playback handles (the deferred ISfxBackend experiment).
    public const float NoticeThreshold = 0.6f;   // walk progress at which the NPC notices
    public const float WalkSpeed = 0.15f;        // walk progress per second
    public const float NoticeSeconds = 1.0f;     // pause at the noticed NPC
    public const float DialogLineSeconds = 1.5f; // auto-advance per dialog line
    public const float BattleIntroSeconds = 1.2f; // the battle-music crossfade and the intro screen's pause before the fight
    public const float BeatTailSeconds = 0.10f;    // beat tail margin: covers the Update tick boundary, so the beat always outlasts its cue
    public const float BattleRoundSeconds = 1.2f;  // one hit per round
    public const float NoticeCueBeatSeconds = 1.18f; // the notice cue rings on the WALK screen (notice-video-game-sfx.wav: 1.08 s) before the notice screen loads
    public const float DialogCueBeatSeconds = 0.5f;  // pacing pause only: the dialog cue has no shipped asset (pre-existing; the cue plays nothing)
    public const float StingBeatSeconds = 0.70f;     // the battle-start sting rings on the DIALOG screen (battle-start.wav: 0.60 s) before the intro loads
    public const float BattleEndingSeconds = 1.83f;  // the KO hit finishes on the BATTLE screen before the walk loads (longest drawn variant, hit-2.wav: 1.73 s)

    private float _sceneSeconds;

    /// <summary>
    /// The battle-ending beat: the KO hit has landed on the battle screen and FINISHES there
    /// (<see cref="BattleEndingSeconds"/>, sized to the longest hit the bag can draw); the
    /// tick after switches to the walk clean. Only true inside the Battle scene, reset by
    /// <see cref="Restart"/> and <see cref="EnterBattle"/>.
    /// </summary>
    private bool _battleEnding;

    // Entry beats (maintainer ruling, Nov 25, round 2): a screen's exit/entry cue fires on the
    // OLD screen, and the switch frame - one beat later - carries no sfx. Each flag is solo:
    // while one runs, Update returns the beat (no scene logic), Advance does nothing (beats
    // cannot be skipped, the same rule as the battle ending), and Restart clears everything.
    private bool _noticeCueBeat; // the notice cue rang on the walk screen; the notice screen loads next
    private bool _dialogCueBeat; // the first line's cue rang on the notice screen; the dialog loads next
    private bool _stingBeat;     // the battle-start sting rang on the dialog screen; the intro loads next

    public StoryController()
    {
    }

    public StoryScene Scene { get; private set; } = StoryScene.Walk;

    /// <summary>Walk progress 0..1 (the NPC stands at <see cref="NoticeThreshold"/>).</summary>
    public float WalkProgress { get; private set; }

    /// <summary>Zero-based dialog line currently shown (see <see cref="DialogLineCount"/>).</summary>
    public int DialogLine { get; private set; }

    /// <summary>Hit rounds executed in the current battle.</summary>
    public int BattleRound { get; private set; }

    /// <summary>Player monster health 0..1 (drawn as a bar in the sample).</summary>
    public float PlayerHp { get; private set; } = 1f;

    /// <summary>Foe monster health 0..1.</summary>
    public float FoeHp { get; private set; } = 1f;

    /// <summary>Engine pitch units of the most recent hit (mirrors the last hit frame's <see cref="StoryFrame.Pitch"/>).</summary>
    public float LastHitPitch { get; private set; }

    /// <summary>Advances the story by <paramref name="deltaSeconds"/>; the returned frame lists the cues to play.</summary>
    public StoryFrame Update(float deltaSeconds)
    {
        _sceneSeconds += deltaSeconds;

        // An entry or ending beat in progress: nothing else runs until the clean switch fires.
        if (_noticeCueBeat)
            return _sceneSeconds >= NoticeCueBeatSeconds ? SwitchToNotice() : default;
        if (_dialogCueBeat)
            return _sceneSeconds >= DialogCueBeatSeconds ? SwitchToDialog() : default;
        if (_stingBeat)
            return _sceneSeconds >= StingBeatSeconds ? SwitchToBattleIntro() : default;

        switch (Scene)
        {
            case StoryScene.Walk:
                if (WalkProgress + WalkSpeed * deltaSeconds >= NoticeThreshold)
                    return BeginNoticeCueBeat();
                WalkProgress += WalkSpeed * deltaSeconds;
                return default;

            case StoryScene.Notice:
                return _sceneSeconds >= NoticeSeconds ? BeginDialogCueBeat() : default;

            case StoryScene.Dialog:
                return _sceneSeconds >= DialogLineSeconds ? AdvanceDialog() : default;

            case StoryScene.BattleIntro:
                return _sceneSeconds >= BattleIntroSeconds ? EnterBattle() : default;

            case StoryScene.Battle:
                if (!_battleEnding)
                    return _sceneSeconds >= BattleRoundSeconds ? BattleRoundTick() : default;
                // The ending beat: the KO hit finishes naturally on the battle screen, and only
                // then does the switch carry the music change and NO cue - like every beat,
                // it cannot be skipped.
                if (_sceneSeconds >= BattleEndingSeconds)
                    return EnterWalkFromBattle();
                return default;

            default:
                return default;
        }
    }

    /// <summary>
    /// Space: kicks off the current pace break's exit cue beat (the cue fires on the CURRENT
    /// screen; the clean switch lands one beat later). A beat already in progress - entry,
    /// ending, or the battle itself - is never skipped.
    /// </summary>
    public StoryFrame Advance()
    {
        // One rule for every beat: Space during a beat does nothing.
        if (_noticeCueBeat || _dialogCueBeat || _stingBeat || _battleEnding)
            return default;

        switch (Scene)
        {
            case StoryScene.Walk:
                WalkProgress = NoticeThreshold; // jump to the NPC
                return BeginNoticeCueBeat();

            case StoryScene.Notice:
                return BeginDialogCueBeat();

            case StoryScene.Dialog:
                return AdvanceDialog();

            case StoryScene.BattleIntro:
                return EnterBattle();

            default:
                return default; // the battle itself cannot be skipped
        }
    }

    /// <summary>R: rewinds the whole showcase to the overworld walk.</summary>
    public StoryFrame Restart()
    {
        Scene = StoryScene.Walk;
        WalkProgress = 0f;
        DialogLine = 0;
        BattleRound = 0;
        _battleEnding = false;
        _noticeCueBeat = _dialogCueBeat = _stingBeat = false;
        PlayerHp = 1f;
        FoeHp = 1f;
        _sceneSeconds = 0f;
        return new StoryFrame(Music: OverworldPlaylist, SceneChanged: true);
    }

    /// <summary>
    /// The notice cue fires ON the walk screen (frozen at the NPC) and finishes there; the
    /// clean switch to the notice screen lands <see cref="NoticeCueBeatSeconds"/> later -
    /// sized to the shipped notice cue so nothing is clipped (no truncation on switches).
    /// </summary>
    private StoryFrame BeginNoticeCueBeat()
    {
        WalkProgress = NoticeThreshold;
        _noticeCueBeat = true;
        _sceneSeconds = 0f;
        return new StoryFrame(Sfx: NoticeCue, Bus: AmbienceBus);
    }

    private StoryFrame SwitchToNotice()
    {
        Scene = StoryScene.Notice;
        _noticeCueBeat = false;
        _sceneSeconds = 0f;
        return new StoryFrame(SceneChanged: true);
    }

    /// <summary>
    /// The first dialog line's cue fires ON the notice screen; the clean switch to the dialog
    /// lands <see cref="DialogCueBeatSeconds"/> later.
    /// </summary>
    private StoryFrame BeginDialogCueBeat()
    {
        _dialogCueBeat = true;
        _sceneSeconds = 0f;
        return new StoryFrame(Sfx: DialogCue);
    }

    private StoryFrame SwitchToDialog()
    {
        Scene = StoryScene.Dialog;
        DialogLine = 0;
        _dialogCueBeat = false;
        _sceneSeconds = 0f;
        return new StoryFrame(SceneChanged: true);
    }

    private StoryFrame AdvanceDialog()
    {
        DialogLine++;
        if (DialogLine >= DialogLineCount)
            return BeginStingBeat();

        _sceneSeconds = 0f;
        return new StoryFrame(Sfx: DialogCue);
    }

    /// <summary>
    /// The battle-start sting fires ON the dialog screen (starting the battle-music crossfade
    /// in the heads, which key on the cue) and finishes there; the clean switch to the intro
    /// screen lands <see cref="StingBeatSeconds"/> later - sized to the shipped sting so its
    /// tail is never clipped at the switch.
    /// </summary>
    private StoryFrame BeginStingBeat()
    {
        _stingBeat = true;
        _sceneSeconds = 0f;
        return new StoryFrame(Sfx: BattleStartCue);
    }

    private StoryFrame SwitchToBattleIntro()
    {
        Scene = StoryScene.BattleIntro;
        _stingBeat = false;
        _sceneSeconds = 0f;
        return new StoryFrame(SceneChanged: true);
    }

    private StoryFrame EnterBattle()
    {
        Scene = StoryScene.Battle;
        _battleEnding = false;
        _noticeCueBeat = _dialogCueBeat = _stingBeat = false; // no beat may leak into a new fight
        BattleRound = 0;
        PlayerHp = 1f;
        FoeHp = 1f;
        _sceneSeconds = 0f;
        return new StoryFrame(Music: BattlePlaylist, SceneChanged: true);
    }

    private StoryFrame BattleRoundTick()
    {
        _sceneSeconds = 0f;

        var playerAttacks = BattleRound % 2 == 0;
        BattleRound++;
        if (playerAttacks) FoeHp = Math.Max(0f, FoeHp - 0.2f);
        else PlayerHp = Math.Max(0f, PlayerHp - 0.2f);

        LastHitPitch = 0f;
        var frame = new StoryFrame(Sfx: HitCue, Pitch: LastHitPitch);

        if (FoeHp > 0f && PlayerHp > 0f)
            return frame;

        // Somebody fainted: the KO hit lands ON the battle screen with no switch attached
        // (maintainer ruling: no cue survives into the next screen), and the ending beat holds
        // the scene for one more round tick before the clean walk-switch frame.
        _battleEnding = true;
        _sceneSeconds = 0f;
        return frame;
    }

    /// <summary>
    /// The clean switch to the walk after the battle-ending beat: overworld music, scene change,
    /// and NO sfx - the fainting blow already FINISHED on the battle screen one ending beat ago.
    /// </summary>
    private StoryFrame EnterWalkFromBattle()
    {
        Scene = StoryScene.Walk;
        WalkProgress = 0f;
        _battleEnding = false;
        _sceneSeconds = 0f;
        return new StoryFrame(Music: OverworldPlaylist, SceneChanged: true);
    }
}
