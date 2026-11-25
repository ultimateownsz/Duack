using System;
using Duack;
using Duack.MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Duack.Sample.Concurrency;

public static class Program
{
    public static void Main() => new Game1().Run();
}

/// <summary>
/// Dedicated voice-concurrency example: SFX voice limiting. Click ONE to fire a single cue,
/// BURST to fire 50 overlapping cues through a manager capped at 8 voices (oldest-voice stealing),
/// and STOP to silence everything mid-listen; Space/T/Escape mirror the buttons from the keyboard.
/// The two-digit readout is the limiter's ActiveVoiceCount and must never exceed 08 during the
/// burst. The limiter is pure core; the only adapter wiring here is the MonoGame clip library and
/// backend. Buttons and labels use the same no-asset pixel-glyph approach as the counter — this
/// project deliberately has no content pipeline.
/// </summary>
public class Game1 : Game
{
    private const int GlobalCap = 8;
    private const int BurstSize = 50;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private SfxManager _sfx = null!;
    private MarbleBag<string> _cues = null!;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private Rectangle _singleButton;
    private Rectangle _burstButton;
    private Rectangle _stopButton;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        // The two-directory convention: no cue is registered by hand.
        var audioRoot = Path.Combine(AppContext.BaseDirectory, "Content", "Audio");
        var library = AudioLibrary.ScanDirectory(audioRoot);
        if (library.Sfx.Count == 0)
            throw new InvalidOperationException($"No SFX discovered under {audioRoot}");

        _cues = new MarbleBag<string>(library.Sfx, random: null, avoidImmediateRepeat: true);

        // Music is untouched by this example: an empty playlist and a real backend are enough for
        // SfxManager's duck pulse, which is a no-op without a configured duck source.
        var clips = new SoundEffectClipLibrary(library);
        var music = new MusicManager(
            new SoundEffectMusicBackend(clips),
            new MusicPlaylistController(new PlaylistConfigBuilder().Build()),
            new MusicConfig());

        _sfx = new SfxManager(
            library,
            new SoundEffectSfxBackend(clips),
            music,
            voiceLimits: new VoiceLimitConfig
            {
                GlobalMaxVoices = GlobalCap,
                DefaultVoiceSeconds = 3f, // long enough that all 50 plays overlap
                DefaultStealingPolicy = VoiceStealingPolicy.Oldest,
            });

        Console.WriteLine($"[Concurrency] cues: [{string.Join(", ", library.Sfx)}]; cap {GlobalCap}");
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (keyboard.IsKeyDown(Keys.Escape)) Exit();

        // Unscaled time: a voice slot must expire on wall-clock time, not gameplay time.
        _sfx.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        LayoutButtons();

        if (WasPressed(keyboard, Keys.Space) || WasClicked(mouse, _singleButton))
        {
            PlayOne();
        }
        else if (WasPressed(keyboard, Keys.T) || WasClicked(mouse, _burstButton))
        {
            for (var i = 0; i < BurstSize; i++) PlayOne();
            Console.WriteLine($"[Concurrency] fired {BurstSize} cues; active {_sfx.ActiveVoiceCount}; last {_sfx.LastPlayResult}");
        }
        else if (WasClicked(mouse, _stopButton))
        {
            _sfx.Stop();
            Console.WriteLine("[Concurrency] stop: all voices stopped and slots cleared");
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    /// <summary>Lays the three buttons out under the counter, above the slot bars.</summary>
    private void LayoutButtons()
    {
        var viewport = GraphicsDevice.Viewport;
        const int width = 150;
        const int height = 44;
        const int gap = 16;
        var totalWidth = width * 3 + gap * 2;
        var x = viewport.Width / 2 - totalWidth / 2;
        var y = viewport.Height - 110;

        _singleButton = new Rectangle(x, y, width, height);
        _burstButton = new Rectangle(x + width + gap, y, width, height);
        _stopButton = new Rectangle(x + (width + gap) * 2, y, width, height);
    }

    private void PlayOne()
    {
        if (_cues.TryDraw(out var cue)) _sfx.Play(cue, duck: false);
    }

    private bool WasPressed(KeyboardState current, Keys key) =>
        current.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    private bool WasClicked(MouseState current, Rectangle button) =>
        current.LeftButton == ButtonState.Pressed &&
        _previousMouse.LeftButton == ButtonState.Released &&
        button.Contains(current.Position);

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        var viewport = GraphicsDevice.Viewport;
        var count = Math.Min(99, _sfx.ActiveVoiceCount);
        var digitSize = 48;
        var x = viewport.Width / 2 - digitSize;
        var y = viewport.Height / 2 - digitSize / 2;

        DrawRect(new Rectangle(x - 8, y - 8, digitSize * 2 + 16, digitSize + 16), Color.Black * 0.7f);
        DrawDigit(x, y, digitSize, count / 10, Color.White);
        DrawDigit(x + digitSize + 8, y, digitSize, count % 10, Color.White);

        LayoutButtons();
        DrawButton(_singleButton, "ONE", Color.White);
        DrawButton(_burstButton, "BURST", Color.White);
        DrawButton(_stopButton, "STOP", Color.OrangeRed);

        // A bar per reserved slot makes the cap visible even without listening.
        var barWidth = Math.Max(4, (viewport.Width - 40) / GlobalCap - 4);
        for (var i = 0; i < GlobalCap; i++)
        {
            var filled = i < _sfx.ActiveVoiceCount;
            DrawRect(new Rectangle(20 + i * (barWidth + 4), viewport.Height - 32, barWidth, 12),
                filled ? Color.OrangeRed : Color.White * 0.2f);
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawRect(Rectangle rect, Color color) =>
        _spriteBatch.Draw(_pixel, rect, color);

    /// <summary>A filled button with a centered pixel-glyph label (no font asset needed).</summary>
    private void DrawButton(Rectangle bounds, string label, Color color)
    {
        DrawRect(bounds, Color.Black * 0.6f);
        const int glyphHeight = 5;
        const int glyphWidth = 3;
        const int scale = 3;
        const int spacing = 1;
        var textWidth = label.Length * (glyphWidth + spacing) * scale - spacing * scale;
        var textHeight = glyphHeight * scale;
        var x = bounds.Center.X - textWidth / 2;
        var y = bounds.Center.Y - textHeight / 2;
        foreach (var c in label) x = DrawGlyph(x, y, scale, c, color) + spacing * scale;
    }

    /// <summary>Draws one 3x5 pixel glyph scaled by <paramref name="scale"/>; returns the next x.</summary>
    private int DrawGlyph(int x, int y, int scale, char c, Color color)
    {
        if (!Glyphs.TryGetValue(c, out var rows)) return x;
        for (var row = 0; row < rows.Length; row++)
        for (var col = 0; col < 3; col++)
        {
            if ((rows[row] & (1 << (2 - col))) == 0) continue;
            DrawRect(new Rectangle(x + col * scale, y + row * scale, scale, scale), color);
        }
        return x + 3 * scale;
    }

    /// <summary>3x5 pixel glyphs (bit 2 = left column), enough for the button labels.</summary>
    private static readonly IReadOnlyDictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
    {
        // 5 rows, bit 2 = leftmost column of the 3-wide glyph.
        ['O'] = new byte[] { 0b111, 0b101, 0b101, 0b101, 0b111 },
        ['N'] = new byte[] { 0b101, 0b111, 0b111, 0b111, 0b101 },
        ['E'] = new byte[] { 0b111, 0b100, 0b111, 0b100, 0b111 },
        ['B'] = new byte[] { 0b111, 0b101, 0b110, 0b101, 0b111 },
        ['U'] = new byte[] { 0b101, 0b101, 0b101, 0b101, 0b111 },
        ['R'] = new byte[] { 0b111, 0b101, 0b110, 0b101, 0b101 },
        ['S'] = new byte[] { 0b111, 0b100, 0b111, 0b001, 0b111 },
        ['T'] = new byte[] { 0b111, 0b010, 0b010, 0b010, 0b010 },
        ['P'] = new byte[] { 0b111, 0b101, 0b111, 0b100, 0b100 },
    };

    /// <summary>Two-digit seven-segment readout, so no Content font asset is needed.</summary>
    private void DrawDigit(int x, int y, int size, int digit, Color color)
    {
        var seg = Math.Max(2, size / 7);
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
        if (on[1]) DrawRect(new Rectangle(x, y + seg, seg, size / 2 - seg), color);
        if (on[2]) DrawRect(new Rectangle(x + size - seg, y + seg, seg, size / 2 - seg), color);
        if (on[3]) DrawRect(new Rectangle(x + seg, y + size / 2, size - 2 * seg, seg), color);
        if (on[4]) DrawRect(new Rectangle(x, y + size / 2 + seg, seg, size / 2 - seg), color);
        if (on[5]) DrawRect(new Rectangle(x + size - seg, y + size / 2 + seg, seg, size / 2 - seg), color);
        if (on[6]) DrawRect(new Rectangle(x + seg, y + size - seg, size - 2 * seg, seg), color);
    }
}
