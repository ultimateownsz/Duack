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

    private KeyboardState _previousKeyboard;
    private float _flash; // seconds of screen flash remaining (battle-start, hits)

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
            if (_sfx.Play(frame.Sfx, frame.Pitch, duck: duck) &&
                (frame.Sfx == StoryController.HitCue || frame.Sfx == StoryController.BattleStartCue))
            {
                _flash = 0.12f;
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

    /// <summary>Top strip visualizing <see cref="MusicDirector.DuckRatio"/> — ducking you can see.</summary>
    private void DrawDuckBar(Viewport viewport)
    {
        var bar = new Rectangle(8, 8, viewport.Width - 16, 10);
        DrawRect(bar, Color.Black * 0.5f);
        var filled = new Rectangle(bar.X, bar.Y, (int)(bar.Width * _director.DuckRatio), bar.Height);
        DrawRect(filled, _director.DuckRatio < 0.99f ? Color.Orange : Color.LightGreen);
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
        var box = new Rectangle(40, viewport.Height - 90, viewport.Width - 80, 70);
        DrawRect(box, Color.Black * 0.75f);
        DrawRect(new Rectangle(box.X, box.Y, box.Width, 3), Color.White * 0.5f);

        for (var i = 0; i < StoryController.DialogLineCount; i++)
        {
            var pip = new Rectangle(box.X + 16 + i * 26, box.Bottom - 22, 18, 10);
            DrawRect(pip, i <= _story.DialogLine ? Color.White : Color.White * 0.25f);
        }
    }

    private void DrawBattle(Viewport viewport)
    {
        var centerX = viewport.Width / 2;
        var y = viewport.Height / 2 + 20;

        // Player (left, blue) and foe (right, red) as simple rectangles.
        DrawRect(new Rectangle(centerX - 220, y, 120, 120), Color.CornflowerBlue);
        DrawRect(new Rectangle(centerX + 100, y, 120, 120), Color.Red);

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
