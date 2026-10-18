using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Duack;
using Duack.KNI;
using Duack.Sample.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Duack.Sample.Web;

/// <summary>
/// KNI/BlazorGL web counterpart of the desktop sample: same scripted storyline, same rendering,
/// same duck meter — but music rides the platform <c>MediaPlayer</c> (HTML &lt;audio&gt;) and there is
/// no one-shot sfx backend (web limitation), so story cues surface as duck pulses.
/// </summary>
public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Texture2D _pixel = null!;

    private AudioLibrary _library = null!;
    private MusicPlaylistController _controller = null!;
    private MusicDirector _director = null!;
    private readonly StoryController _story = new();
    private MarbleBag<string> _hitBag = null!;

    private KeyboardState _previousKeyboard;
    private float _flash;
    private float _hitFlash;
    private int _hitSide = -1;
    private float _duckPeakHold = 1f;

    private bool _musicStarted;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        // Web cannot enumerate directories; rebuild the library from the checked-in manifest.
        AudioLibraryManifest manifest;
        try
        {
            using var stream = TitleContainer.OpenStream("Content/Audio/audio-manifest.json");
            using var reader = new StreamReader(stream);
            manifest = AudioLibraryManifestJson.Deserialize(reader.ReadToEnd());
        }
        catch
        {
            manifest = new AudioLibraryManifest { Sfx = new List<string>(), Tracks = new List<string>() };
        }
        _library = AudioLibrary.FromManifest(manifest);

        var hitCues = _library.Sfx.Where(n => n.StartsWith("hit", StringComparison.Ordinal)).ToList();
        _hitBag = new MarbleBag<string>(hitCues.Count > 0 ? hitCues : new[] { StoryController.HitCue }, avoidImmediateRepeat: true);

        var playlists = SampleAudio.BuildPlaylists(_library);
        var config = SampleAudio.BuildConfig(playlists);

        // KNI adapter: platform MediaPlayer (HTML <audio> on BlazorGL).
        var backend = new MediaPlayerMusicBackend(
            new KniMediaPlayerHost(), contentRoot: "Content/Audio/Tracks", extension: ".ogg");
        _controller = new MusicPlaylistController(playlists);
        _director = new MusicDirector(backend, _controller, config.Music);

        _director.TrackCompleted += finished =>
            System.Diagnostics.Debug.WriteLine($"[Sample] track finished: {finished.Name} ({finished.Context})");
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

        if (Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        _director.Update(dt);

        if (_flash > 0f) _flash = Math.Max(0f, _flash - dt);
        if (_hitFlash > 0f) _hitFlash = Math.Max(0f, _hitFlash - dt);

        // Start music on first update — the user already gestured (boot-screen click), so
        // browser autoplay policy is satisfied.
        if (!_musicStarted)
        {
            _musicStarted = true;
            _director.Play(StoryController.OverworldPlaylist);
        }

        var keyboard = Keyboard.GetState();

        if (WasKeyPressed(keyboard, _previousKeyboard, Keys.Space))
            ApplyFrame(_story.Advance());
        else if (WasKeyPressed(keyboard, _previousKeyboard, Keys.R))
            ApplyFrame(_story.Restart());
        else
            ApplyFrame(_story.Update(dt));

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    private void ApplyFrame(StoryFrame frame)
    {
        if (frame.Music is not null)
            _director.Play(frame.Music);

        if (frame.Sfx is not null)
        {
            var duck = frame.Music is null;
            if (duck) _director.Pulse(SampleAudio.SfxDuckSource);

            var cueName = frame.Sfx;
            if (frame.Sfx == StoryController.HitCue && _hitBag is not null)
                cueName = _hitBag.Draw();

            if (frame.Sfx == StoryController.HitCue || frame.Sfx == StoryController.BattleStartCue)
            {
                _flash = 0.12f;
                if (frame.Sfx == StoryController.HitCue)
                {
                    _hitSide = (_story.BattleRound % 2 == 1) ? 1 : 0;
                    _hitFlash = 0.25f;
                }
            }
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        var inBattle = _story.Scene is StoryScene.Battle or StoryScene.BattleIntro;
        GraphicsDevice.Clear(inBattle ? Color.DarkRed : Color.DarkGreen);

        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp);

        DrawDuckBar(GraphicsDevice.Viewport);

        switch (_story.Scene)
        {
            case StoryScene.Walk:
            case StoryScene.Notice:
                DrawOverworld(GraphicsDevice.Viewport);
                break;
            case StoryScene.Dialog:
                DrawOverworld(GraphicsDevice.Viewport);
                DrawDialog(GraphicsDevice.Viewport);
                break;
            case StoryScene.BattleIntro:
            case StoryScene.Battle:
                DrawBattle(GraphicsDevice.Viewport);
                break;
        }

        if (_flash > 0f)
            _spriteBatch.Draw(_pixel, GraphicsDevice.Viewport.Bounds, Color.White * (_flash / 0.12f));

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    // --- Rendering helpers (same as desktop) ---

    private void DrawDuckBar(Viewport viewport)
    {
        if (_duckPeakHold < _director.DuckRatio)
            _duckPeakHold = _director.DuckRatio;
        else
            _duckPeakHold = Math.Max(_director.DuckRatio, _duckPeakHold - 0.005f);

        var x = 8;
        var w = viewport.Width - 16;

        var title = new Rectangle(x, 8, w, 10);
        DrawRect(title, Color.Black * 0.7f);
        DrawRect(new Rectangle(title.X + 4, title.Y + 3, 30, 4), Color.White * 0.85f);
        var readout = new Rectangle(title.Right - 50, title.Y + 3, (int)(46 * (1f - _director.DuckRatio)), 4);
        if (readout.Width > 0)
            DrawRect(readout, _director.DuckRatio < 0.99f ? Color.OrangeRed : Color.LightGreen);

        var musicLabel = new Rectangle(x, 20, 46, 12);
        DrawRect(musicLabel, Color.Black * 0.5f);
        DrawRect(new Rectangle(musicLabel.X + 4, musicLabel.Y + 3, 8, 6), Color.CornflowerBlue);

        var musicBar = new Rectangle(x + 48, 20, w - 48, 12);
        DrawRect(musicBar, Color.Black * 0.6f);
        var ratio = _director.DuckRatio;
        var musicFill = new Rectangle(musicBar.X, musicBar.Y, (int)(musicBar.Width * ratio), musicBar.Height);
        var musicColor = ratio > 0.99f ? Color.LightGreen : ratio > 0.6f ? Color.Orange : Color.OrangeRed;
        DrawRect(musicFill, musicColor);

        for (var i = 0; i <= 4; i++)
        {
            var tx = musicBar.X + (int)(musicBar.Width * (i / 4f));
            DrawRect(new Rectangle(tx, musicBar.Bottom - 3, 1, 3), Color.White * 0.35f);
        }

        var peakX = musicBar.X + (int)(musicBar.Width * _duckPeakHold);
        DrawRect(new Rectangle(peakX, musicBar.Y, 2, musicBar.Height), Color.White * 0.9f);

        var sfxLabel = new Rectangle(x, 34, 46, 12);
        DrawRect(sfxLabel, Color.Black * 0.5f);
        DrawRect(new Rectangle(sfxLabel.X + 4, sfxLabel.Y + 3, 8, 6), Color.Orange);

        var sfxBar = new Rectangle(x + 48, 34, w - 48, 12);
        DrawRect(sfxBar, Color.Black * 0.6f);

        if (ratio < 0.99f)
            DrawRect(new Rectangle(sfxBar.X, sfxBar.Y, sfxBar.Width, sfxBar.Height), Color.Orange * 0.85f);
        else
            DrawRect(new Rectangle(sfxBar.X, sfxBar.Y + sfxBar.Height / 2, sfxBar.Width, 2), Color.White * 0.15f);

        DrawRect(new Rectangle(x + 44, 32, 2, 4), Color.White * 0.4f);
    }

    private void DrawOverworld(Viewport viewport)
    {
        var pathY = viewport.Height / 2;
        var startX = 60;
        var npcX = viewport.Width - 140;
        DrawRect(new Rectangle(startX, pathY + 60, npcX - startX, 6), Color.White * 0.25f);
        var progress = MathHelper.Clamp(_story.WalkProgress / StoryController.NoticeThreshold, 0f, 1f);
        var playerX = (int)MathHelper.Lerp(startX, npcX - 40, progress);
        DrawRect(new Rectangle(playerX, pathY, 32, 60), Color.CornflowerBlue);
        DrawRect(new Rectangle(npcX, pathY, 32, 60), Color.Gold);
        if (_story.Scene == StoryScene.Notice)
        {
            DrawRect(new Rectangle(npcX + 12, pathY - 26, 8, 18), Color.Yellow);
            DrawRect(new Rectangle(npcX + 12, pathY - 5, 8, 6), Color.Yellow);
        }
    }

    private void DrawDialog(Viewport viewport)
    {
        var box = new Rectangle(40, viewport.Height - 110, viewport.Width - 80, 90);
        DrawRect(box, Color.Black * 0.8f);
        DrawRect(new Rectangle(box.X, box.Y, box.Width, 3), Color.White * 0.6f);
        DrawRect(new Rectangle(box.X + 12, box.Y + 12, 40, 40), Color.Gold);
        DrawRect(new Rectangle(box.X + 16, box.Y + 16, 32, 32), Color.DarkGoldenrod);
        DrawDigit(box.X + 64, box.Y + 14, 36, _story.DialogLine, Color.White);
        var textX = box.X + 110;
        var textY = box.Y + 16;
        var widths = new[] { (int)(box.Width * 0.55f), (int)(box.Width * 0.45f), (int)(box.Width * 0.35f) };
        for (var i = 0; i < 3; i++)
            DrawRect(new Rectangle(textX, textY + i * 16, widths[i], 6), Color.White * (i == 0 ? 0.9f : i == 1 ? 0.6f : 0.35f));
        if ((Environment.TickCount64 / 400) % 2 == 0)
            DrawRect(new Rectangle(textX + widths[0] + 4, textY, 6, 6), Color.White);
        for (var i = 0; i < StoryController.DialogLineCount; i++)
        {
            var pip = new Rectangle(box.X + 16 + i * 26, box.Bottom - 16, 18, 8);
            DrawRect(pip, i <= _story.DialogLine ? Color.White : Color.White * 0.25f);
        }
    }

    private void DrawDigit(int x, int y, int size, int digit, Color color)
    {
        var seg = size / 7;
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
        if (on[0]) DrawRect(new Rectangle(x + seg, y, size - 2 * seg, seg), color);
        if (on[1]) DrawRect(new Rectangle(x, y + seg, seg, (size / 2) - seg), color);
        if (on[2]) DrawRect(new Rectangle(x + size - seg, y + seg, seg, (size / 2) - seg), color);
        if (on[3]) DrawRect(new Rectangle(x + seg, y + size / 2, size - 2 * seg, seg), color);
        if (on[4]) DrawRect(new Rectangle(x, y + size / 2 + seg, seg, (size / 2) - seg), color);
        if (on[5]) DrawRect(new Rectangle(x + size - seg, y + size / 2 + seg, seg, (size / 2) - seg), color);
        if (on[6]) DrawRect(new Rectangle(x + seg, y + size - seg, size - 2 * seg, seg), color);
    }

    private void DrawBattle(Viewport viewport)
    {
        var centerX = viewport.Width / 2;
        var y = viewport.Height / 2 + 40;

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

        DrawHpBar(new Rectangle(centerX - 220, y - 14, 120, 8), _story.PlayerHp, Color.CornflowerBlue);
        DrawHpBar(new Rectangle(centerX + 100, y - 14, 120, 8), _story.FoeHp, Color.Red);

        const int pipSize = 12;
        const int pipGap = 6;
        const int pipsPerRow = 5;
        var rowWidth = pipsPerRow * pipSize + (pipsPerRow - 1) * pipGap;
        var startX = centerX - rowWidth / 2;
        for (var i = 0; i < _story.BattleRound && i < 10; i++)
        {
            var col = i % pipsPerRow;
            var row = i / pipsPerRow;
            var px = startX + col * (pipSize + pipGap);
            var py = 52 + row * (pipSize + pipGap);
            DrawRect(new Rectangle(px, py, pipSize, pipSize), Color.White * 0.7f);
        }

        if (_hitFlash > 0f)
        {
            var attackerX = (_hitSide == 0) ? (centerX + 100 + 60) : (centerX - 220 + 60);
            var markerColor = (_hitSide == 0) ? Color.CornflowerBlue : Color.Red;
            DrawRect(new Rectangle(attackerX - 4, y - 30, 8, 180), markerColor * (_hitFlash / 0.25f));
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