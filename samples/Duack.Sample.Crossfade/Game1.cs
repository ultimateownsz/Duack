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

    public IReadOnlyList<string> Tracks => _tracks;
    public int CurrentTrackIndex => _current;
    public bool CrossfadeEnabled => _crossfadeEnabled;
    public int CrossfadePresetIndex => _crossfadePresetIndex;
    public bool IsPlaying => _manager.IsPlaying;
    public string CurrentTrackName => _tracks.Length == 0 ? "No tracks found" : _tracks[_current];
    public bool IsTransitioning => _manager.IsTransitioning;
    public float TransitionProgress => _manager.TransitionDurationSeconds <= 0f
        ? 1f
        : Math.Clamp(_manager.TransitionElapsedSeconds / _manager.TransitionDurationSeconds, 0f, 1f);
    public float TransitionElapsedSeconds => _manager.TransitionElapsedSeconds;
    public float TransitionDurationSeconds => _manager.TransitionDurationSeconds;
    public string TransitionFromTrack => _manager.TransitionFromTrack ?? CurrentTrackName;
    public string TransitionToTrack => _manager.TransitionToTrack ?? CurrentTrackName;

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

    public void PreviousTrack()
    {
        if (_tracks.Length == 0)
            return;

        _current = (_current - 1 + _tracks.Length) % _tracks.Length;
        _manager.Play(_tracks[_current]);
    }

    public void TogglePlayback()
    {
        if (_manager.IsPlaying)
            _manager.Stop();
        else if (_tracks.Length > 0)
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
        base.Draw(gameTime);
    }

    private static bool WasKeyPressed(KeyboardState cur, KeyboardState prev, Keys key) =>
        cur.IsKeyDown(key) && !prev.IsKeyDown(key);
}