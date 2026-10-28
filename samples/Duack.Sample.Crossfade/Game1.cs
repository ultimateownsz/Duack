using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Duack;
using Duack.KNI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Duack.Sample.Crossfade;

/// <summary>
/// Minimal crossfade showcase: keys 1/2/3 switch between tracks (triggering a crossfade),
/// C toggles crossfade on/off (hard cut vs equal-power ramp), Space plays the next track.
/// A corner indicator shows green (ramp) / red (hard cut).
/// </summary>
public class Game1 : Game, IDisposable
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch? _spriteBatch;
    private Texture2D _pixel = null!;

    private AudioLibrary _library = null!;
    private MusicPlaylistController _controller = null!;
    private MusicManager _manager = null!;
    private AudioConfig _config = null!;
    private float _originalCrossfade;
    private bool _crossfadeEnabled = true;
    private readonly float[] _crossfadePresets = new[] { 4f, 8f, 12f, 16f };
    private int _crossfadePresetIndex = 0;

    private string[] _tracks = Array.Empty<string>();
    private int _current;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    public IReadOnlyList<string> Tracks => _tracks;
    public int CurrentTrackIndex => _current;
    public bool CrossfadeEnabled => _crossfadeEnabled;
    public int CrossfadePresetIndex => _crossfadePresetIndex;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        base.Initialize();

        AudioLibraryManifest manifest;
        try
        {
            Stream? stream = null;
            foreach (var p in new[] { "Audio/audio-manifest.json", "Content/Audio/audio-manifest.json" })
            {
                try { stream = TitleContainer.OpenStream(p); break; } catch { }
            }
            if (stream == null) throw new FileNotFoundException("audio-manifest.json not found");
            using (stream)
            using (var reader = new StreamReader(stream))
                manifest = AudioLibraryManifestJson.Deserialize(reader.ReadToEnd());
        }
        catch
        {
            manifest = new AudioLibraryManifest { Sfx = new System.Collections.Generic.List<string>(), Tracks = new System.Collections.Generic.List<string>() };
        }
        _library = AudioLibrary.FromManifest(manifest);
        _tracks = _library.Tracks.ToArray();

        var playlists = new PlaylistConfig();
        // one single-track playlist per discovered track
        foreach (var t in _tracks)
        {
            var def = new PlaylistDefinition();
            def.Tracks.Add(new PlaylistTrack { Name = t, Context = t });
            playlists.Playlists[t] = def;
        }

        _config = new AudioConfigBuilder()
            .WithMasterVolume(1f)
            .WithMusicMasterVolume(1f)
            .WithCrossfadeSeconds(4.0f)
            .Build();
        _crossfadePresetIndex = 0;
        _originalCrossfade = _crossfadePresets[_crossfadePresetIndex];
        _config.Music.CrossfadeSeconds = _originalCrossfade;

        var backend = new MediaPlayerMusicBackend(new KniMediaPlayerHost(), contentRoot: "Content/Audio/Tracks", extension: ".ogg");
        _controller = new MusicPlaylistController(playlists);
        _manager = new MusicManager(backend, _controller, _config.Music, masterVolume: _config.MasterVolume);
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
        if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
        _manager.Update(dt);

        var keyboard = Keyboard.GetState();
        for (int i = 0; i < _tracks.Length && i < 9; i++)
        {
            var key = Keys.D1 + i;
            if (WasKeyPressed(keyboard, _previousKeyboard, key))
            {
                _current = i;
                _manager.Play(_tracks[i]);
            }
        }
        if (WasKeyPressed(keyboard, _previousKeyboard, Keys.Space) && _tracks.Length > 0)
        {
            _current = (_current + 1) % _tracks.Length;
            _manager.Play(_tracks[_current]);
        }
        if (WasKeyPressed(keyboard, _previousKeyboard, Keys.C))
        {
            _crossfadeEnabled = !_crossfadeEnabled;
            _config.Music.CrossfadeSeconds = _crossfadeEnabled ? _originalCrossfade : 0f;
        }

        // Clickable crossfade toggle button + duration presets
        var mouse = Mouse.GetState();
        var vp = GraphicsDevice.Viewport;
        var cx = vp.Width / 2;

        var toggleBtn = new Rectangle(cx - 260, vp.Height - 110, 200, 48);
        if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released &&
            toggleBtn.Contains(mouse.Position))
        {
            _crossfadeEnabled = !_crossfadeEnabled;
            _config.Music.CrossfadeSeconds = _crossfadeEnabled ? _originalCrossfade : 0f;
        }

        var presetPanel = new Rectangle(cx - 40, vp.Height - 110, 300, 48);
        for (int i = 0; i < _crossfadePresets.Length; i++)
        {
            var b = new Rectangle(cx - 30 + i * 72, vp.Height - 102, 60, 32);
            if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released &&
                b.Contains(mouse.Position))
            {
                _crossfadePresetIndex = i;
                _originalCrossfade = _crossfadePresets[_crossfadePresetIndex];
                if (_crossfadeEnabled)
                    _config.Music.CrossfadeSeconds = _originalCrossfade;
            }
        }
        // Click track rows
        for (int i = 0; i < _tracks.Length; i++)
        {
            var rect = new Rectangle(vp.Width / 2 - 240, 130 + i * 40, 480, 32);
            if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released &&
                rect.Contains(mouse.Position))
            {
                _current = i;
                _manager.Play(_tracks[i]);
            }
        }
        _previousMouse = mouse;

        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    public void SelectTrack(int index)
    {
        if ((uint)index >= (uint)_tracks.Length)
            return;

        _current = index;
        _manager.Play(_tracks[index]);
    }

    public void NextTrack()
    {
        if (_tracks.Length == 0)
            return;

        _current = (_current + 1) % _tracks.Length;
        _manager.Play(_tracks[_current]);
    }

    public void ToggleCrossfade()
    {
        _crossfadeEnabled = !_crossfadeEnabled;
        _config.Music.CrossfadeSeconds = _crossfadeEnabled ? _originalCrossfade : 0f;
    }

    public void SelectCrossfadePreset(int index)
    {
        if ((uint)index >= (uint)_crossfadePresets.Length)
            return;

        _crossfadePresetIndex = index;
        _originalCrossfade = _crossfadePresets[index];
        if (_crossfadeEnabled)
            _config.Music.CrossfadeSeconds = _originalCrossfade;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(15, 17, 21));
        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp);

        var vp = GraphicsDevice.Viewport;
        var cx = vp.Width / 2;

        // Player panel
        DrawRect(new Rectangle(cx - 260, 100, 520, 220), new Color(27, 31, 39));
        DrawRectOutline(new Rectangle(cx - 260, 100, 520, 220), new Color(68, 74, 86));

        // Track list
        for (int i = 0; i < _tracks.Length; i++)
        {
            var rect = new Rectangle(cx - 240, 130 + i * 40, 480, 32);
            var bg = i == _current ? new Color(46, 125, 50) : new Color(35, 39, 47);
            DrawRect(rect, bg);
            DrawRectOutline(rect, new Color(88, 94, 108));
            // "play" marker
            if (i == _current) DrawRect(new Rectangle(rect.X + 12, rect.Y + 10, 12, 12), Color.White);
        }

        // Crossfade toggle button
        var toggleBtn = new Rectangle(cx - 260, vp.Height - 110, 200, 48);
        DrawRect(toggleBtn, _crossfadeEnabled ? new Color(46, 125, 50) : new Color(180, 60, 60));
        DrawRectOutline(toggleBtn, new Color(150, 156, 170));
        DrawRect(new Rectangle(toggleBtn.X + 16, toggleBtn.Y + 16, 16, 16), Color.White * (_crossfadeEnabled ? 1f : 0.5f));

        // Duration presets panel
        var presetPanel = new Rectangle(cx - 40, vp.Height - 110, 300, 48);
        DrawRect(presetPanel, new Color(27, 31, 39));
        DrawRectOutline(presetPanel, new Color(88, 94, 108));

        for (int i = 0; i < _crossfadePresets.Length; i++)
        {
            var b = new Rectangle(cx - 30 + i * 72, vp.Height - 102, 60, 32);
            var active = i == _crossfadePresetIndex;
            DrawRect(b, active ? new Color(59, 130, 246) : new Color(55, 59, 68));
            DrawRectOutline(b, active ? new Color(147, 197, 253) : new Color(110, 116, 128));
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawRectOutline(Rectangle r, Color c)
    {
        DrawRect(new Rectangle(r.X, r.Y, r.Width, 2), c);
        DrawRect(new Rectangle(r.X, r.Bottom - 2, r.Width, 2), c);
        DrawRect(new Rectangle(r.X, r.Y, 2, r.Height), c);
        DrawRect(new Rectangle(r.Right - 2, r.Y, 2, r.Height), c);
    }

    private void DrawRect(Rectangle r, Color c) => _spriteBatch!.Draw(_pixel, r, c);

    private static bool WasKeyPressed(KeyboardState cur, KeyboardState prev, Keys key) =>
        cur.IsKeyDown(key) && !prev.IsKeyDown(key);
}