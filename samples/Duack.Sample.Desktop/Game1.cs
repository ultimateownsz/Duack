using Duack;
using Duack.MonoGame;
using Duack.Sample.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Duack.Sample.Desktop;

public static class Program
{
    public static void Main() => new Game1().Run();
}

/// <summary>
/// MonoGame DesktopGL sample for Duack as a scripted Pokémon-style storyline: the rectangle walks
/// the overworld (overworld track), gets noticed (notice sfx), chats (dialog sfx per line), then
/// battles (battle-start sting, battle track) trading pitched hit cues until someone faints and the
/// story loops. All cues come from the two-directory <c>Content/Audio</c> convention via
/// <see cref="AudioLibrary"/>, play through the <see cref="SfxDirector"/> (which ducks the music
/// automatically), and scene music switches through auto-built single-track playlists.
///
/// <para>Keys: Space advances/skips, R restarts the story, S plays a random discovered sfx,
/// 1/2 force the overworld/battle playlist, Esc quits.</para>
/// </summary>
public class Game1 : Game
{
    // Scan the app base dir (where the csproj copied Content), not the working directory —
    // `dotnet run` from the repo root must find the same files TitleContainer loads.
    private static readonly string AudioRoot = Path.Combine(AppContext.BaseDirectory, "Content", "Audio");

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Texture2D _pixel = null!;

    private AudioLibrary _library = null!;
    private SoundEffectClipLibrary _clips = null!;
    private MusicPlaylistController _controller = null!;
    private MusicDirector _director = null!;
    private SfxDirector _sfx = null!;
    private readonly StoryController _story = new();
    private MarbleBag<string> _hitBag = null!;

    private KeyboardState _previousKeyboard;
    private float _flash; // seconds of screen flash remaining (battle-start)
    private float _hitFlash; // seconds of hit-entity flash remaining
    private int _hitSide; // -1 none, 0 player, 1 foe

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        // --- Discover the two-directory convention (no cue is registered by hand) ---
        _library = AudioLibrary.ScanDirectory(AudioRoot);
        Console.WriteLine($"[Sample] discovered sfx: [{string.Join(", ", _library.Sfx)}]");
        Console.WriteLine($"[Sample] discovered tracks: [{string.Join(", ", _library.Tracks)}]");

        // Bag of hit cues (any discovered cue whose name starts with "hit") so a battle never
        // plays the same hit twice in a row; with a single "hit" cue this is just "hit" every time.
        var hitCues = _library.Sfx.Where(n => n.StartsWith("hit", StringComparison.Ordinal)).ToList();
        _hitBag = new MarbleBag<string>(hitCues.Count > 0 ? hitCues : new[] { StoryController.HitCue }, random: null, avoidImmediateRepeat: true);

        // --- Auto-build the playlists: one looping playlist per track + the "music" bag ---
        // The context mapping and the mix itself live in the shared sample core (SampleAudio),
        // so the web head plays exactly the same story and configuration.
        var playlists = SampleAudio.BuildPlaylists(_library);
        Console.WriteLine("[Sample] playlists:");
        Console.WriteLine(PlaylistJson.Serialize(playlists));

        var config = SampleAudio.BuildConfig(playlists);

        // --- Wire the desktop adapters: music + one-shot sfx over one clip library ---
        _clips = new SoundEffectClipLibrary(_library);
        _controller = new MusicPlaylistController(playlists);
        _director = new MusicDirector(new SoundEffectMusicBackend(_clips), _controller, config.Music);
        _sfx = new SfxDirector(_library, new SoundEffectSfxBackend(_clips), _director);

        _director.TrackCompleted += finished =>
            Console.WriteLine($"[Sample] track finished: {finished.Name} ({finished.Context})");

        _director.Play(StoryController.OverworldPlaylist);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        _director.Update(dt);

        if (_flash > 0f)
            _flash = Math.Max(0f, _flash - dt);
        if (_hitFlash > 0f)
            _hitFlash = Math.Max(0f, _hitFlash - dt);

        var keyboard = Keyboard.GetState();

        if (WasKeyPressed(keyboard, _previousKeyboard, Keys.Space))
        {
            ApplyFrame(_story.Advance());
        }
        else if (WasKeyPressed(keyboard, _previousKeyboard, Keys.R))
        {
            ApplyFrame(_story.Restart());
        }
        else if (WasKeyPressed(keyboard, _previousKeyboard, Keys.S))
        {
            // Random draw from the discovered sfx bag (still auto-ducks the music).
            _sfx.PlayNext();
        }
        else if (WasKeyPressed(keyboard, _previousKeyboard, Keys.D1))
        {
            _director.Play("overworld");
        }
        else if (WasKeyPressed(keyboard, _previousKeyboard, Keys.D2))
        {
            _director.Play("battle");
        }
        else
        {
            ApplyFrame(_story.Update(dt));
        }

        _previousKeyboard = keyboard;

        base.Update(gameTime);
    }

    /// <summary>Feeds one story frame into the audio system: scene music switch, then the cue.</summary>
    private void ApplyFrame(StoryFrame frame)
    {
        if (frame.SceneChanged)
            Console.WriteLine($"[Sample] scene: {_story.Scene}");

        if (frame.Music is not null)
            _director.Play(frame.Music); // single-track playlist: crossfades from whatever plays

        // A scene switch (e.g. battle start) cuts any lingering one-shot (the dialog cue is long)
        // so it cannot bleed into the new scene.
        if (frame.Sfx == StoryController.BattleStartCue)
            _sfx.Stop();

        if (frame.Sfx is not null)
        {
            // Duck only when no scene switch is happening: a cue that accompanies a switch
            // (the fainting hit) must not suppress the crossfade back to the overworld track.
            var duck = frame.Music is null;

            // Hit cues draw from the MarbleBag so a battle never plays the same hit twice in a row
            // (the XACT-successor variation feature); with a single "hit" cue this is just "hit".
            var cueName = frame.Sfx;
            if (frame.Sfx == StoryController.HitCue && _hitBag is not null)
                cueName = _hitBag.Draw();

            if (_sfx.Play(cueName, frame.Pitch, duck: duck) &&
                (frame.Sfx == StoryController.HitCue || frame.Sfx == StoryController.BattleStartCue))
            {
                _flash = 0.12f;
                if (frame.Sfx == StoryController.HitCue)
                {
                    // Who got hit: player attacks on odd BattleRound (after increment), foe on even.
                    _hitSide = (_story.BattleRound % 2 == 1) ? 1 : 0;
                    _hitFlash = 0.25f;
                }
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        var viewport = GraphicsDevice.Viewport;
        var inBattle = _story.Scene is StoryScene.Battle or StoryScene.BattleIntro;

        GraphicsDevice.Clear(inBattle ? Color.DarkRed : Color.DarkGreen);

        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp);

        DrawDuckBar(viewport);

        switch (_story.Scene)
        {
            case StoryScene.Walk:
            case StoryScene.Notice:
                DrawOverworld(viewport);
                break;
            case StoryScene.Dialog:
                DrawOverworld(viewport);
                DrawDialog(viewport);
                break;
            case StoryScene.BattleIntro:
                DrawBattle(viewport);
                break;
            case StoryScene.Battle:
                DrawBattle(viewport);
                break;
        }

        // Flash overlay for the battle-start sting and every hit.
        if (_flash > 0f)
            _spriteBatch.Draw(_pixel, viewport.Bounds, Color.White * (_flash / 0.12f));

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>
    /// Top strip visualizing <see cref="MusicDirector.DuckRatio"/> — the sidechain ducking meter.
    /// 1.0 = music at full volume; lower = the music is ducked (quieter) while a sound effect speaks.
    /// </summary>
    private void DrawDuckBar(Viewport viewport)
    {
        // Title strip so the bar reads as a named meter, not a mystery rectangle.
        var title = new Rectangle(8, 8, viewport.Width - 16, 12);
        DrawRect(title, Color.Black * 0.7f);
        // "DUCK" label as a small block on the left of the title strip.
        DrawRect(new Rectangle(title.X + 4, title.Y + 3, 34, 6), Color.White * 0.85f);
        // Ratio readout as a filled block on the right of the title strip (wider = more ducked).
        var ratioBlock = new Rectangle(title.Right - 60, title.Y + 3, (int)(56 * (1f - _director.DuckRatio)), 6);
        if (ratioBlock.Width > 0)
            DrawRect(ratioBlock, _director.DuckRatio < 0.99f ? Color.OrangeRed : Color.LightGreen);

        // The meter itself.
        var bar = new Rectangle(8, 22, viewport.Width - 16, 14);
        DrawRect(bar, Color.Black * 0.6f);

        var ratio = _director.DuckRatio;
        var filled = new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), bar.Height);
        var color = ratio > 0.99f ? Color.LightGreen
                  : ratio > 0.6f ? Color.Orange
                  : Color.OrangeRed;
        DrawRect(filled, color);

        // Tick marks at 0 / 0.25 / 0.5 / 0.75 / 1.0 so the scale is readable.
        for (var i = 0; i <= 4; i++)
        {
            var x = bar.X + (int)(bar.Width * (i / 4f));
            DrawRect(new Rectangle(x, bar.Bottom - 4, 1, 4), Color.White * 0.4f);
        }

        // Legend row: green = music full, orange = ducked while an sfx plays.
        var legend = new Rectangle(8, 38, viewport.Width - 16, 8);
        DrawRect(new Rectangle(legend.X, legend.Y, 8, legend.Height), Color.LightGreen);
        DrawRect(new Rectangle(legend.X + 10, legend.Y, 8, legend.Height), Color.Orange);
        DrawRect(new Rectangle(legend.X + 20, legend.Y, 8, legend.Height), Color.OrangeRed);

        // When actively ducking, pulse the filled portion.
        if (ratio < 0.99f)
        {
            var pulse = (float)(0.5 + 0.5 * Math.Sin(Environment.TickCount64 / 80.0));
            DrawRect(new Rectangle(bar.X, bar.Y, (int)(bar.Width * ratio), 2), Color.White * (0.5f * pulse));
        }
    }

    private void DrawOverworld(Viewport viewport)
    {
        var pathY = viewport.Height / 2;
        var startX = 60;
        var npcX = viewport.Width - 140;

        // The walking path.
        DrawRect(new Rectangle(startX, pathY + 60, npcX - startX, 6), Color.White * 0.25f);

        // The player rectangle, position driven by the story's walk progress.
        var progress = MathHelper.Clamp(_story.WalkProgress / StoryController.NoticeThreshold, 0f, 1f);
        var playerX = (int)MathHelper.Lerp(startX, npcX - 40, progress);
        DrawRect(new Rectangle(playerX, pathY, 32, 60), Color.CornflowerBlue);

        // The NPC rectangle (yellow, standing at the notice spot).
        DrawRect(new Rectangle(npcX, pathY, 32, 60), Color.Gold);

        if (_story.Scene == StoryScene.Notice)
        {
            // "!" above the NPC.
            DrawRect(new Rectangle(npcX + 12, pathY - 26, 8, 18), Color.Yellow);
            DrawRect(new Rectangle(npcX + 12, pathY - 5, 8, 6), Color.Yellow);
        }
    }

    private void DrawDialog(Viewport viewport)
    {
        // A text box with one pip per dialog line; filled pips = lines already read.
        // Dialog text is shown as a simple "LINE n" readout (no SpriteFont in the sample) plus a
        // blinking caret so the box reads as live speech rather than a static panel.
        var box = new Rectangle(40, viewport.Height - 110, viewport.Width - 80, 90);
        DrawRect(box, Color.Black * 0.8f);
        DrawRect(new Rectangle(box.X, box.Y, box.Width, 3), Color.White * 0.6f);

        // Speaker portrait (a small colored square) on the left.
        DrawRect(new Rectangle(box.X + 12, box.Y + 12, 40, 40), Color.Gold);
        DrawRect(new Rectangle(box.X + 16, box.Y + 16, 32, 32), Color.DarkGoldenrod);

        // "LINE n" as a big digit on the right of the portrait.
        var digitX = box.X + 64;
        var digitY = box.Y + 14;
        DrawDigit(digitX, digitY, 36, _story.DialogLine, Color.White);

        // Three text lines as varying-width bars (a "typing" placeholder) so the box reads as speech.
        var textX = box.X + 110;
        var textY = box.Y + 16;
        var widths = new[] { (int)(box.Width * 0.55f), (int)(box.Width * 0.45f), (int)(box.Width * 0.35f) };
        for (var i = 0; i < 3; i++)
        {
            var w = widths[i];
            var alpha = (i == 0) ? 0.9f : (i == 1) ? 0.6f : 0.35f;
            DrawRect(new Rectangle(textX, textY + i * 16, w, 6), Color.White * alpha);
        }

        // Blinking caret at the end of the first text line.
        if ((Environment.TickCount64 / 400) % 2 == 0)
            DrawRect(new Rectangle(textX + widths[0] + 4, textY, 6, 6), Color.White);

        // Pips: one per dialog line, filled = read.
        for (var i = 0; i < StoryController.DialogLineCount; i++)
        {
            var pip = new Rectangle(box.X + 16 + i * 26, box.Bottom - 16, 18, 8);
            DrawRect(pip, i <= _story.DialogLine ? Color.White : Color.White * 0.25f);
        }
    }

    /// <summary>Draws a single digit 0-9 as a 7-segment display made of rectangles.</summary>
    private void DrawDigit(int x, int y, int size, int digit, Color color)
    {
        var seg = size / 7;
        // segments: 0=top,1=upper-left,2=upper-right,3=middle,4=lower-left,5=lower-right,6=bottom
        var on = digit switch
        {
            0 => new[] { true, true, true, false, true, true, true },
            1 => new[] { false, false, true, false, false, true, false },
            2 => new[] { true, false, true, true, true, false, true },
            3 => new[] { true, false, true, true, false, true, true },
            4 => new[] { false, true, true, true, false, true, false },
            5 => new[] { true, true, false, true, false, true, true },
            6 => new[] { true, true, false, true, true, true, true },
            7 => new[] { true, false, true, false, false, true, false },
            8 => new[] { true, true, true, true, true, true, true },
            9 => new[] { true, true, true, true, false, true, true },
            _ => new[] { false, false, false, false, false, false, false },
        };
        // top
        if (on[0]) DrawRect(new Rectangle(x + seg, y, size - 2 * seg, seg), color);
        // upper-left
        if (on[1]) DrawRect(new Rectangle(x, y + seg, seg, (size / 2) - seg), color);
        // upper-right
        if (on[2]) DrawRect(new Rectangle(x + size - seg, y + seg, seg, (size / 2) - seg), color);
        // middle
        if (on[3]) DrawRect(new Rectangle(x + seg, y + size / 2, size - 2 * seg, seg), color);
        // lower-left
        if (on[4]) DrawRect(new Rectangle(x, y + size / 2 + seg, seg, (size / 2) - seg), color);
        // lower-right
        if (on[5]) DrawRect(new Rectangle(x + size - seg, y + size / 2 + seg, seg, (size / 2) - seg), color);
        // bottom
        if (on[6]) DrawRect(new Rectangle(x + seg, y + size - seg, size - 2 * seg, seg), color);
    }

    private void DrawBattle(Viewport viewport)
    {
        var centerX = viewport.Width / 2;
        var y = viewport.Height / 2 + 20;

        // Player (left, blue) and foe (right, red) as simple rectangles.
        // The entity that just got hit flashes white and scales up briefly so the blow is readable.
        var playerFlash = (_hitSide == 0 && _hitFlash > 0f) ? (_hitFlash / 0.25f) : 0f;
        var foeFlash = (_hitSide == 1 && _hitFlash > 0f) ? (_hitFlash / 0.25f) : 0f;

        var playerRect = new Rectangle(centerX - 220, y, 120, 120);
        var foeRect = new Rectangle(centerX + 100, y, 120, 120);
        if (playerFlash > 0f)
        {
            playerRect.Inflate((int)(6 * playerFlash), (int)(6 * playerFlash));
            DrawRect(playerRect, Color.White * (0.6f * playerFlash));
        }
        if (foeFlash > 0f)
        {
            foeRect.Inflate((int)(6 * foeFlash), (int)(6 * foeFlash));
            DrawRect(foeRect, Color.White * (0.6f * foeFlash));
        }

        DrawRect(new Rectangle(centerX - 220, y, 120, 120), Color.CornflowerBlue);
        DrawRect(new Rectangle(centerX + 100, y, 120, 120), Color.Red);

        // A "strike" marker on the side that just attacked, so the blow reads even without sound.
        if (_hitFlash > 0f)
        {
            var attackerX = (_hitSide == 0) ? (centerX + 100 + 60) : (centerX - 220 + 60);
            var markerColor = (_hitSide == 0) ? Color.CornflowerBlue : Color.Red;
            DrawRect(new Rectangle(attackerX - 4, y - 30, 8, 180), markerColor * (_hitFlash / 0.25f));
        }

        // HP bars driven by the story state.
        DrawHpBar(new Rectangle(centerX - 220, y - 14, 120, 8), _story.PlayerHp, Color.CornflowerBlue);
        DrawHpBar(new Rectangle(centerX + 100, y - 14, 120, 8), _story.FoeHp, Color.Red);

        // "Hit number" pips so traded blows are visible even without sound.
        for (var i = 0; i < _story.BattleRound && i < 10; i++)
        {
            DrawRect(new Rectangle(centerX - 60 + (i % 5) * 14, 40 + (i / 5) * 14, 10, 10), Color.White * 0.7f);
        }
    }

    private void DrawHpBar(Rectangle rect, float fill, Color color)
    {
        DrawRect(rect, Color.Black * 0.6f);
        DrawRect(new Rectangle(rect.X, rect.Y, (int)(rect.Width * MathHelper.Clamp(fill, 0f, 1f)), rect.Height), color);
    }

    private void DrawRect(Rectangle rect, Color color) =>
        _spriteBatch!.Draw(_pixel, rect, color);

    private static bool WasKeyPressed(KeyboardState current, KeyboardState previous, Keys key) =>
        current.IsKeyDown(key) && previous.IsKeyUp(key);
}
